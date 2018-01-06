using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;

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

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}