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
            Payload = payload ?? new byte[0];
            var parts = data.Split(' ');
            Subject = parts[0];
            SubscriptionId = parts[1];
            ReplyTo = parts.Length == 4 ? parts[2] : string.Empty;
            Size = int.Parse(parts.Last());
        }

        public IncomingMessage(string subject, string subscriptionId, string replyTo, int size, byte[] payload)
        {
            Subject = subject;
            SubscriptionId = subscriptionId;
            ReplyTo = replyTo;
            Size = size;
            Payload = payload ?? new byte[0];
        }
    }
}