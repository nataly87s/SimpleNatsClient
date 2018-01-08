using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleNatsClient.Extensions
{
    public static class NatsClientExtensions
    {
        public static async Task Publish(this NatsClient @this, string subject, string data, CancellationToken cancellationToken = default(CancellationToken))
        {
            await @this.Publish(subject, Encoding.UTF8.GetBytes(data), cancellationToken);
        }

        public static async Task<IncomingMessage> Request(this NatsClient @this, string subject, TimeSpan timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            return await @this.Request(subject, cancellationTokenSource.Token);
        }
        
        public static async Task<IncomingMessage> Request(this NatsClient @this, string subject, string data, TimeSpan timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource(timeout);
            return await @this.Request(subject, data, cancellationTokenSource.Token);
        }

        public static async Task<IncomingMessage> Request(this NatsClient @this, string subject, string data, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await @this.Request(subject, Encoding.UTF8.GetBytes(data), cancellationToken);
        }
    }
}