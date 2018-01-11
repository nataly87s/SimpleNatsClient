using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
        private static readonly byte[] Pong = Encoding.UTF8.GetBytes("PONG\r\n");
        private const string Ping = "PING";
        
        private readonly CompositeDisposable _disposable;
        private ITcpConnection _tcpConnection;
        private readonly NatsParser _parser = new NatsParser();
        public ServerInfo ServerInfo { get; private set; }

        public IObservable<Message> Messages { get; }

        internal NatsConnection(NatsConnectionOptions options)
        {
            var messages = _parser.Messages
                .Select(tuple => MessageDeserializer.Deserialize(tuple.Message, tuple.Payload))
                .Publish();

            Messages = messages;
            
            var connect = messages.OfType<Message<ServerInfo>>()
                .Do(m => ServerInfo = m.Data)
                .Select(_ => Observable.FromAsync(async ct =>
                {
                    if (options.SslRequired)
                    {
                        await _tcpConnection.MakeSsl(options.RemoteCertificateValidationCallback, options.Certificates);
                    }
                    await this.Write($"CONNECT {JsonConvert.SerializeObject(options)}", ct);
                }))
                .Switch()
                .Subscribe();

            var pingpong = messages.Where(m => m.Op == Ping)
                .Subscribe(async _ => await Write(Pong, CancellationToken.None));
            
            _disposable = new CompositeDisposable(
                connect,
                pingpong,
                messages.Connect()
            );
        }

        internal void Connect(ITcpConnection tcpConnection)
        {
            _tcpConnection = tcpConnection;
            var buffer = new byte[512];
            var reader = Observable.FromAsync(async ct =>
            {
                var count = await _tcpConnection.Read(buffer, ct);
                if (count > 0) _parser.Parse(buffer, 0, count);
            }).Repeat().Subscribe();

            _disposable.Add(tcpConnection);
            _disposable.Add(reader);
        }
        
        public async Task Write(byte[] buffer, CancellationToken cancellationToken)
        {
            if (_tcpConnection == null)
            {
                throw new InvalidOperationException("Not connected to NATS server");
            }
            await _tcpConnection.Write(buffer, cancellationToken);
        }

        public void Dispose()
        {
            _disposable.Dispose();
        }

        internal static NatsConnection Connect(ITcpConnection tcpConnection, NatsConnectionOptions options)
        {
            var connection = new NatsConnection(options);
            connection.Connect(tcpConnection);
            return connection;
        }

        public static NatsConnection Connect(NatsConnectionOptions options)
        {
            var tcpConnection = new TcpConnection(options.Hostname, options.Port);
            return Connect(tcpConnection, options);
        }
    }
}