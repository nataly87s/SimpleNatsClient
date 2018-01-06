using System.Linq;

namespace SimpleNatsClient
{
    public class IncomingMessage
    {
        public string Subject { get; }
        public string SubscriptionId { get; }
        public string ReplyTo { get; }
        public int Size { get; }
        public byte[] Payload { get; }

        public IncomingMessage(string data, byte[] payload)
        {
            Payload = payload;
            var parts = data.Split(' ');
            Subject = parts[0];
            SubscriptionId = parts[1];
            ReplyTo = parts.Length == 4 ? parts[2] : string.Empty;
            Size = int.Parse(parts.Last());
        }
    }
}