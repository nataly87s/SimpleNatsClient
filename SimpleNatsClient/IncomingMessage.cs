using System.Linq;

namespace SimpleNatsClient
{
    public class IncomingMessage
    {
        private static readonly byte[] EmptyPayload = new byte[0];

        public string Subject { get; }
        public string SubscriptionId { get; }
        public string ReplyTo { get; }
        public int Size { get; }
        public byte[] Payload { get; }

        public IncomingMessage(string data, byte[] payload)
        {
            Payload = payload ?? EmptyPayload;

            // SUBJECT SID[ REPLYTO] SIZE
            var subjectEnd = data.IndexOf(' ');
            Subject = data.Substring(0, subjectEnd);
            var subscriptionIdEnd = data.IndexOf(' ', subjectEnd + 1);
            SubscriptionId = data.Substring(subjectEnd + 1, subscriptionIdEnd - subjectEnd - 1);

            var sizeOnlyIfMissing = data.IndexOf(' ', subscriptionIdEnd + 1);
            if (sizeOnlyIfMissing < 0)
            {
                Size = int.Parse(data.Substring(subscriptionIdEnd + 1));
            }
            else
            {
                ReplyTo = data.Substring(subscriptionIdEnd + 1, sizeOnlyIfMissing - subscriptionIdEnd - 1);
                Size = int.Parse(data.Substring(sizeOnlyIfMissing + 1));
            }
        }

        public IncomingMessage(string subject, string subscriptionId, string replyTo, int size, byte[] payload)
        {
            Subject = subject;
            SubscriptionId = subscriptionId;
            ReplyTo = replyTo;
            Size = size;
            Payload = payload ?? EmptyPayload;
        }
    }
}