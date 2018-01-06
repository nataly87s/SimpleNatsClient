using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;

namespace SimpleNatsClient.Extensions
{
    public static class NatsConnectionExtensions
    {
        public const string NewLine = "\r\n";
        
        public static async Task Write(this INatsConnection @this, string data)
        {
            await @this.Write(Encoding.UTF8.GetBytes(data + NewLine), CancellationToken.None);
        }
        
        public static async Task Write(this INatsConnection @this, string data, CancellationToken cancellationToken)
        {
            await @this.Write(Encoding.UTF8.GetBytes(data + NewLine), cancellationToken);
        }        
    }
}