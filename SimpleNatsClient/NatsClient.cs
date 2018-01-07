using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Extensions;
using SimpleNatsClient.Messages;

namespace SimpleNatsClient
{
    public class NatsClient : IDisposable
    {
        private static readonly byte[] NewLine = Encoding.UTF8.GetBytes("\r\n");

        private readonly INatsConnection _connection;

        public NatsClient(INatsConnection connection)
        {
            _connection = connection;
        }

        public async Task Publish(string subject, byte[] data, CancellationToken cancellationToken = default (CancellationToken))
        {
            var message = $"PUB {subject} {data.Length}";
            var encodedMessage = Encoding.UTF8.GetBytes(message);

            var encoded = new byte[message.Length + (2 * NewLine.Length) + data.Length];
            encodedMessage.CopyTo(encoded, 0);
            NewLine.CopyTo(encoded, encodedMessage.Length);
            data.CopyTo(encoded, encodedMessage.Length + NewLine.Length);
            NewLine.CopyTo(encoded, encoded.Length - NewLine.Length);

            await _connection.Write(encoded, cancellationToken);
        }

        public IObservable<IncomingMessage> GetSubscription(string subject)
        {
            var sid = Guid.NewGuid().ToString();            
            return Observable.FromAsync(() => _connection.Write($"SUB {subject} {sid}"))
                .SelectMany(_ => GetSubscription(subject, sid))                
                .Finally(async () => await _connection.Write($"UNSUB {sid}"));
        }

        public IObservable<IncomingMessage> GetSubscription(string subject, int messageCount)
        {
            var sid = Guid.NewGuid().ToString();            
            return Observable.FromAsync(() => _connection.Write($"SUB {subject} {sid}"))
                .SelectMany(_ => _connection.Write($"UNSUB {sid} {messageCount}").ToObservable())
                .SelectMany(_ => GetSubscription(subject, sid))
                .Take(messageCount);
        }

        private IObservable<IncomingMessage> GetSubscription(string subject, string sid)
        {
            return _connection.Messages.OfType<Message<IncomingMessage>>()
                .Select(x => x.Data)
                .Where(x => x.Subject == subject && x.SubscriptionId == sid);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}