using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Extensions;
using SimpleNatsClient.Messages;
using Xunit;

namespace SimpleNatsClient.Tests
{
    public class NatsClientTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(50);

        [Fact(DisplayName = "should publish message")]
        public async Task PublishTest()
        {
            const string subject = "some_subject";
            const string message = "some message";
            var size = Encoding.UTF8.GetByteCount(message);
            var expectedData = Encoding.UTF8.GetBytes($"PUB {subject} {size}\r\n{message}\r\n");
            var mockNatsConnection = new Mock<INatsConnection>();

            using (var client = new NatsClient(mockNatsConnection.Object))
            {
                await client.Publish(subject, message);
            }

            mockNatsConnection.Verify(x =>
                x.Write(It.Is<byte[]>(data => expectedData.SequenceEqual(data)), It.IsAny<CancellationToken>()));
        }

        [Fact(DisplayName = "should get subscription")]
        public async Task GetSubscriptionTest()
        {
            const string subject = "some_subject";
            const string paload = "some payload";
            var encodedPayload = Encoding.UTF8.GetBytes(paload);
            string subscriptionId = null;

            var messageSubject = new Subject<Message<IncomingMessage>>();
            
            var mockNatsConnection = new Mock<INatsConnection>();
            
            mockNatsConnection
                .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, _) =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, $"^SUB {subject} ([^ \r\n].+)\r\n$");
                    if (match.Success)
                    {
                        subscriptionId = match.Groups[1].Value;
                    }
                    return Task.CompletedTask;
                });

            mockNatsConnection.Setup(x => x.Messages).Returns(messageSubject);

            using (var client = new NatsClient(mockNatsConnection.Object))
            {
                var subscription = client.GetSubscription(subject)
                    .FirstAsync()
                    .Timeout(_timeout)
                    .ToTask();
                
                var incomingMessage = new IncomingMessage(subject, subscriptionId, string.Empty, encodedPayload.Length, encodedPayload);
                messageSubject.OnNext(Message.From("MSG", incomingMessage));

                var message = await subscription;
                Assert.Equal(incomingMessage, message);
            }

            mockNatsConnection.Verify(x => x.Write(It.Is<byte[]>(data => Encoding.UTF8.GetBytes($"UNSUB {subscriptionId}\r\n").SequenceEqual(data)), It.IsAny<CancellationToken>()));
        }

        [Theory(DisplayName = "should auto unsubscribe")]
        [InlineData(1)]
        [InlineData(5)]
        public async Task GetSubscription_MessageCount_Test(int count)
        {
            const string subject = "some_subject";
            const string paload = "some payload";
            var encodedPayload = Encoding.UTF8.GetBytes(paload);
            string subscriptionId = null;

            var messageSubject = new Subject<Message<IncomingMessage>>();
            
            var mockNatsConnection = new Mock<INatsConnection>();
            
            mockNatsConnection
                .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, _) =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, $"^SUB {subject} ([^ \r\n].+)\r\n$");
                    if (match.Success)
                    {
                        subscriptionId = match.Groups[1].Value;
                    }
                    return Task.CompletedTask;
                });

            mockNatsConnection.Setup(x => x.Messages).Returns(messageSubject);

            using (var client = new NatsClient(mockNatsConnection.Object))
            {
                var subscription = client.GetSubscription(subject, count)
                    .ToArray()
                    .Timeout(_timeout)
                    .ToTask();

                var otherMessage = new IncomingMessage(subject, "other_subscription", string.Empty, encodedPayload.Length, encodedPayload);
                messageSubject.OnNext(Message.From("MSG", otherMessage));
                var incomingMessage = new IncomingMessage(subject, subscriptionId, string.Empty, encodedPayload.Length, encodedPayload);
                for (var i = 0; i < count + 1; i++)
                {
                    messageSubject.OnNext(Message.From("MSG", incomingMessage));
                }

                var messages = await subscription;                
                Assert.Equal(Enumerable.Repeat(incomingMessage, count), messages);
            }

            mockNatsConnection.Verify(x => x.Write(It.Is<byte[]>(data => Encoding.UTF8.GetBytes($"UNSUB {subscriptionId} {count}\r\n").SequenceEqual(data)), It.IsAny<CancellationToken>()));
        }
    }
}