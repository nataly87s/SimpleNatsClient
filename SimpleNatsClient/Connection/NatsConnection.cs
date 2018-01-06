using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

        public IConnectableObservable<Message> Messages { get; }

        public NatsConnection(NatsConnectionOptions options)
        {
            Messages = _parser.Messages
                .Select(tuple => MessageDeserializer.Deserialize(tuple.Message, tuple.Payload))
                .Publish();

            var connect = Messages.OfType<Message<ServerInfo>>()
                .Do(m => ServerInfo = m.Data)
                .Select(_ => Observable.FromAsync(ct => this.Write($"CONNECT {JsonConvert.SerializeObject(options)}", ct)))
                .Switch()
                .Subscribe();

            var pingpong = Messages.Where(m => m.Op == Ping)
                .Subscribe(async _ => await Write(Pong, CancellationToken.None));
            
            _disposable = new CompositeDisposable(
                connect,
                pingpong,
                Messages.Connect()
            );
        }

        public void Connect(ITcpConnection tcpConnection)
        {
            if (_tcpConnection != null)
            {
                throw new InvalidOperationException("Already connected to server");
            }
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

        public static NatsConnection Connect(ITcpConnection tcpConnection, NatsConnectionOptions options)
        {
            var connection = new NatsConnection(options);
            connection.Connect(tcpConnection);
            return connection;
        }
    }
}