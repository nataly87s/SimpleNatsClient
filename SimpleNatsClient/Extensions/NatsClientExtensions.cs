using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleNatsClient.Extensions
{
    public static class NatsClientExtensions
    {
        public static async Task Publish(this NatsClient @this, string subject, string data, CancellationToken cancellationToken = default (CancellationToken))
        {
            await @this.Publish(subject, Encoding.UTF8.GetBytes(data), cancellationToken);
        }
    }
}