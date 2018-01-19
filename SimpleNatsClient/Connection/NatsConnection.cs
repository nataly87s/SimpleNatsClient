using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimpleNatsClient.Extensions;
using SimpleNatsClient.Messages;

namespace SimpleNatsClient.Connection
{
    public class NatsConnection : INatsConnection
    {
        private const string PongOp = "PONG";
        private static readonly byte[] Pong = Encoding.UTF8.GetBytes($"{PongOp}\r\n");
        private const string PingOp = "PING";
        private static readonly byte[] Ping = Encoding.UTF8.GetBytes($"{PingOp}\r\n");

        private readonly CompositeDisposable _disposable;
        private readonly Subject<ServerInfo> _onConnect = new Subject<ServerInfo>();
        private readonly NatsParser _parser = new NatsParser();
        private readonly TcpConnectionProvider _tcpConnectionProvider;
        private readonly NatsOptions _options;
        private readonly (string Hostname, int Port)[] _servers;
        private ITcpConnection _tcpConnection;
        private IDisposable _readFromStream;

        public ServerInfo ServerInfo { get; private set; }

        public NatsConnectionState ConnectionState { get; private set; } = NatsConnectionState.Disconnected;

        public IObservable<Message> Messages { get; }

        public IObservable<ServerInfo> OnConnect => ConnectionState == NatsConnectionState.Connected
            ? _onConnect.StartWith(ServerInfo)
            : _onConnect;

        internal NatsConnection((string Hostname, int Port)[] servers, TcpConnectionProvider tcpConnectionProvider, NatsOptions options)
        {
            if (servers == null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            if (servers.Length == 0)
            {
                throw new ArgumentException("server list should contain at least one server", nameof(servers));
            }

            _servers = servers;
            _tcpConnectionProvider = tcpConnectionProvider;
            _options = options;

            var messages = _parser.Messages
                .Select(tuple => MessageDeserializer.Deserialize(tuple.Message, tuple.Payload))
                .Retry()
                .Publish();

            Messages = messages;

            var connect = messages.OfType<Message<ServerInfo>>()
                .Do(m => ServerInfo = m.Data)
                .Select(m => Observable.FromAsync(async ct =>
                {
                    if (ConnectionState == NatsConnectionState.Connected) return;
                    ConnectionState = NatsConnectionState.Connecting;
                    if (options.ConnectionOptions.SslRequired)
                    {
                        await _tcpConnection.MakeSsl(options.RemoteCertificateValidationCallback, options.Certificates);
                    }
                    await this.Write($"CONNECT {JsonConvert.SerializeObject(options.ConnectionOptions)}", ct);
                    ConnectionState = NatsConnectionState.Connected;
                    _onConnect.OnNext(m.Data);
                }))
                .Switch()
                .Retry()
                .Subscribe();

            var pingpong = messages.Where(m => m.Op == PingOp)
                .SelectMany(_ => Observable.FromAsync(ct => Write(Pong, ct)))
                .Retry()
                .Subscribe();

            var reconnect = OnConnect
                .Select(_ => Observable.FromAsync(ct => Write(Ping, ct))
                    .SelectMany(Messages.FirstAsync(m => m.Op == PongOp).Timeout(options.PingTimeout))
                    .DelaySubscription(options.PingPongInterval)
                    .Retry(1)
                    .Repeat()
                    .IgnoreElements()
                    .Select(__ => Unit.Default)
                    .Catch(Observable.FromAsync(Connect)))
                .Switch()
                .Catch()
                .Finally(Dispose)
                .Subscribe();

            _disposable = new CompositeDisposable(
                connect,
                pingpong,
                reconnect,
                messages.Connect(),
                _parser
            );
        }

        internal async Task Connect(CancellationToken cancellationToken)
        {
            if (_disposable.IsDisposed)
            {
                throw new ObjectDisposedException("NatsConnection");
            }

            ConnectionState = NatsConnectionState.Connecting;
            _readFromStream?.Dispose();
            _tcpConnection?.Dispose();
            _parser.Reset();

            var uniqServers = new HashSet<(string Hostname, int Port)>(_servers);
            
            if (_options.ConnectionOptions.Protocol == 1 && ServerInfo?.ConnectUrls != null)
            {
                uniqServers.UnionWith(ServerInfo.ConnectUrls.Select(x => x.Split(':'))
                    .Select(x => (x[0], int.Parse(x[1]))));
            }

            var servers = uniqServers.ToArray();
            
            var i = 0;
            var nextServer = 0;
            while (true)
            {
                try
                {
                    var server = servers[nextServer];
                    _tcpConnection = await _tcpConnectionProvider(server.Hostname, server.Port);
                    break;
                }
                catch
                {
                    if (i < _options.MaxConnectRetry)
                    {
                        i++;
                        nextServer = (nextServer + 1) % servers.Length;
                        await Task.Delay(_options.ConnectRetryDelay, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    Dispose();
                    throw;
                }
            }

            var buffer = new byte[512];
            _readFromStream = Observable.FromAsync(ct => _tcpConnection.Read(buffer, ct))
                .Where(count => count > 0)
                .Do(count => _parser.Parse(buffer, 0, count))
                .Repeat()
                .Catch()
                .Subscribe();

            await OnConnect.FirstAsync().ToTask(cancellationToken);
        }

        public async Task Write(byte[] buffer, CancellationToken cancellationToken)
        {
            if (_tcpConnection == null)
            {
                throw new InvalidOperationException("Not connected to NATS server");
            }
            if (_disposable.IsDisposed)
            {
                throw new ObjectDisposedException("NatsConnection");
            }

            await _tcpConnection.Write(buffer, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposable.IsDisposed) return;

            ConnectionState = NatsConnectionState.Disconnected;

            _tcpConnection?.Dispose();
            _readFromStream?.Dispose();

            _disposable.Dispose();
        }

        internal static async Task<NatsConnection> Connect((string Hostname, int Port)[] servers, TcpConnectionProvider tcpConnectionProvider, NatsOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var connection = new NatsConnection(servers, tcpConnectionProvider, options);
            await connection.Connect(cancellationToken);
            return connection;
        }

        public static Task<NatsConnection> Connect(NatsOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Connect("localhost", 4222, options, cancellationToken);
        }
        public static Task<NatsConnection> Connect(string hostname, int port, NatsOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Connect(new[] {(hostname, port)}, options, cancellationToken);
        }
        public static Task<NatsConnection> Connect((string Hostname, int Port)[] servers, NatsOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Connect(servers, async (h, p) =>
            {
                var connection = new TcpConnection(h, p);
                await connection.Connect();
                return connection;
            }, options, cancellationToken);
        }
    }
}