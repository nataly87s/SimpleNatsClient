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
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(500);

        [Fact(DisplayName = "should publish message")]
        public async Task PublishTest()
        {
            const string subject = "some_subject";
            const string message = "some message";
            var size = Encoding.UTF8.GetByteCount(message);
            var expectedData = Encoding.UTF8.GetBytes($"PUB {subject} {size}\r\n{message}\r\n");
            var mockNatsConnection = new Mock<INatsConnection>();
            mockNatsConnection.Setup(x => x.OnConnect).Returns(Observable.Return(new ServerInfo()));

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
            var resetEvent = new ManualResetEvent(false);

            var messageSubject = new Subject<Message<IncomingMessage>>();

            var mockNatsConnection = new Mock<INatsConnection>();
            mockNatsConnection
                .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, _) =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, $"^SUB {subject} ([^ \r\n]+)\r\n$");
                    if (match.Success)
                    {
                        subscriptionId = match.Groups[1].Value;
                        resetEvent.Set();
                    }

                    return Task.CompletedTask;
                });

            mockNatsConnection.Setup(x => x.Messages).Returns(messageSubject);
            mockNatsConnection.Setup(x => x.OnConnect).Returns(Observable.Return(new ServerInfo()));
            mockNatsConnection.Setup(x => x.ConnectionState).Returns(NatsConnectionState.Connected);

            using (var client = new NatsClient(mockNatsConnection.Object))
            {
                var subscription = client.GetSubscription(subject)
                    .FirstAsync()
                    .Timeout(_timeout)
                    .ToTask();

                resetEvent.WaitOne(_timeout);
                var incomingMessage = new IncomingMessage(subject, subscriptionId, string.Empty, encodedPayload.Length, encodedPayload);
                messageSubject.OnNext(Message.From("MSG", incomingMessage));

                var message = await subscription;
                Assert.Equal(incomingMessage, message);
            }

            mockNatsConnection.Verify(x =>
                x.Write(
                    It.Is<byte[]>(data => Encoding.UTF8.GetString(data).StartsWith($"SUB {subject} ")),
                    It.IsAny<CancellationToken>()), Times.Once);

            mockNatsConnection.Verify(x =>
                x.Write(
                    It.Is<byte[]>(data => Encoding.UTF8.GetString(data).Contains($"UNSUB {subscriptionId}\r\n")),
                    It.IsAny<CancellationToken>()));
        }

        [Fact(DisplayName = "should resubscribe after client reconnects")]
        public void ResubscribeTest()
        {
            const int reconnectCount = 3;
            const string subject = "some_subject";
            string subscriptionId = null;
            var resetEvent = new AutoResetEvent(false);

            var messageSubject = new Subject<Message<IncomingMessage>>();
            var mockNatsConnection = new Mock<INatsConnection>();

            mockNatsConnection
                .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, _) =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, $"^SUB {subject} ([^ \r\n]+)\r\n$");
                    if (match.Success)
                    {
                        subscriptionId = match.Groups[1].Value;
                        resetEvent.Set();
                    }

                    return Task.CompletedTask;
                });

            var onConnectMock = new Subject<ServerInfo>();
            mockNatsConnection.Setup(x => x.Messages).Returns(messageSubject);
            mockNatsConnection.Setup(x => x.OnConnect).Returns(onConnectMock);
            mockNatsConnection.Setup(x => x.ConnectionState).Returns(NatsConnectionState.Connected);

            using (var client = new NatsClient(mockNatsConnection.Object))
            using (client.GetSubscription(subject).Subscribe())
            {
                for (var i = 0; i < reconnectCount; i++)
                {
                    onConnectMock.OnNext(new ServerInfo());
                    resetEvent.WaitOne(_timeout);
                }
            }

            mockNatsConnection.Verify(x =>
                x.Write(
                    It.Is<byte[]>(data => Encoding.UTF8.GetString(data).StartsWith($"SUB {subject} ")),
                    It.IsAny<CancellationToken>()), Times.Exactly(reconnectCount));

            mockNatsConnection.Verify(x =>
                x.Write(
                    It.Is<byte[]>(data => Encoding.UTF8.GetString(data).Contains($"UNSUB {subscriptionId}\r\n")),
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory(DisplayName = "should auto unsubscribe")]
        [InlineData(1, 2)]
        [InlineData(3, 4)]
        [InlineData(3, 2)]
        public async Task AutoUnsubscribeTest(int messageCount, int take)
        {
            const string subject = "some_subject";
            const string paload = "some payload";
            var encodedPayload = Encoding.UTF8.GetBytes(paload);
            string subscriptionId = null;
            var resetEvent = new ManualResetEvent(false);

            var messageSubject = new Subject<Message<IncomingMessage>>();

            var mockNatsConnection = new Mock<INatsConnection>();
            mockNatsConnection
                .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, _) =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, $"^SUB {subject} ([^ \r\n].+)\r\n");
                    if (match.Success)
                    {
                        subscriptionId = match.Groups[1].Value;
                        resetEvent.Set();
                    }

                    return Task.CompletedTask;
                });

            mockNatsConnection.Setup(x => x.Messages).Returns(messageSubject);
            mockNatsConnection.Setup(x => x.OnConnect).Returns(Observable.Return(new ServerInfo()));
            mockNatsConnection.Setup(x => x.ConnectionState).Returns(NatsConnectionState.Connected);

            using (var client = new NatsClient(mockNatsConnection.Object))
            {
                var subscription = client.GetSubscription(subject, messageCount)
                    .Take(take)
                    .ToArray()
                    .Timeout(_timeout)
                    .ToTask();

                resetEvent.WaitOne(_timeout);

                var otherMessage = new IncomingMessage(subject, "other_subscription", string.Empty, encodedPayload.Length, encodedPayload);
                messageSubject.OnNext(Message.From("MSG", otherMessage));
                var incomingMessage = new IncomingMessage(subject, subscriptionId, string.Empty, encodedPayload.Length, encodedPayload);
                for (var i = 0; i < messageCount + 1; i++)
                {
                    messageSubject.OnNext(Message.From("MSG", incomingMessage));
                }

                var messages = await subscription;
                Assert.Equal(Enumerable.Repeat(incomingMessage, Math.Min(take, messageCount)), messages);
            }

            mockNatsConnection.Verify(x =>
                x.Write(
                    It.Is<byte[]>(data => Encoding.UTF8.GetString(data).Contains($"UNSUB {subscriptionId} {messageCount}\r\n")),
                    It.IsAny<CancellationToken>()));

            mockNatsConnection.Verify(
                x => x.Write(
                    It.Is<byte[]>(data => Encoding.UTF8.GetString(data).Contains($"UNSUB {subscriptionId}\r\n")),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(take < messageCount ? 1 : 0));
        }

        [Fact]
        public async Task RequestTest()
        {
            const string subject = "some_subject";
            const string expectedReply = "some reply";
            var encodedReply = Encoding.UTF8.GetBytes(expectedReply);
            string inbox = null, subscriptionId = null;
            var resetEvent = new ManualResetEvent(false);

            var messageSubject = new Subject<Message<IncomingMessage>>();

            var mockNatsConnection = new Mock<INatsConnection>();
            mockNatsConnection
                .Setup(x => x.Write(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, _) =>
                {
                    var message = Encoding.UTF8.GetString(data);
                    var match = Regex.Match(message, $"^PUB {subject} ([^ \r\n]+) [0-9]+\r\n.*");
                    if (match.Success)
                    {
                        inbox = match.Groups[1].Value;
                        resetEvent.Set();
                    }
                    else
                    {
                        match = Regex.Match(message, "^SUB _INBOX.[^ \r\n]+ ([^ \r\n]+)\r\n$");
                        if (match.Success)
                        {
                            subscriptionId = match.Groups[1].Value;
                        }
                    }

                    return Task.CompletedTask;
                });

            mockNatsConnection.Setup(x => x.Messages).Returns(messageSubject);
            mockNatsConnection.Setup(x => x.OnConnect).Returns(Observable.Return(new ServerInfo()));

            using (var client = new NatsClient(mockNatsConnection.Object))
            {
                var requestTask = client.Request(subject, _timeout);

                resetEvent.WaitOne(_timeout);
                Assert.False(string.IsNullOrEmpty(subscriptionId), "Client published request before subscribing for result");

                var incomingMessage = new IncomingMessage(inbox, subscriptionId, string.Empty, encodedReply.Length, encodedReply);
                messageSubject.OnNext(Message.From("MSG", incomingMessage));

                var reply = await requestTask;
                Assert.Equal(incomingMessage, reply);
            }
        }
    }
}