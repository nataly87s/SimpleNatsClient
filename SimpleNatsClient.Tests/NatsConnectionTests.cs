using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Messages;
using Xunit;

namespace SimpleNatsClient.Tests
{
    public class NatsConnectionTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(200);

        [Fact(DisplayName = "should send connect message after recieving info message")]
        public async Task ConnectAfterInfo()
        {
            var serverInfo = new ServerInfo();
            var tcpConnection = new MockTcpConnection(serverInfo);

            var options = new NatsConnectionOptions();
            var cancellationTokenSource = new CancellationTokenSource(_timeout);
            using (var connection = await NatsConnection.Connect((h, p) => tcpConnection, options, cancellationTokenSource.Token))
            {
                var wrote = await tcpConnection.OnWrite.Timeout(_timeout).FirstAsync();
                var connectionMessage = Encoding.UTF8.GetString(wrote);

                Assert.StartsWith("CONNECT ", connectionMessage);

                var sendOptions = JObject.Parse(connectionMessage.Substring(8));
                Assert.Equal(JObject.FromObject(options), sendOptions);

                Assert.Equal(JObject.FromObject(serverInfo), JObject.FromObject(connection.ServerInfo));
                
                Assert.False(tcpConnection.IsSsl, "should not use ssl tcp connection");
            }

            Assert.True(tcpConnection.IsDisposed, "should dispose tcp connection");
        }

        [Fact(DisplayName = "should use ssl")]
        public async Task ConnectWithSsl()
        {
            var serverInfo = new ServerInfo{ SslRequired = true };
            var tcpConnection = new MockTcpConnection(serverInfo);

            var options = new NatsConnectionOptions { SslRequired = true };
            var cancellationTokenSource = new CancellationTokenSource(_timeout);
            using (var connection = await NatsConnection.Connect((h, p) => tcpConnection, options, cancellationTokenSource.Token))
            {
                var wrote = await tcpConnection.OnWrite.Timeout(_timeout).FirstAsync();
                var connectionMessage = Encoding.UTF8.GetString(wrote);

                Assert.StartsWith("CONNECT ", connectionMessage);

                var sendOptions = JObject.Parse(connectionMessage.Substring(8));
                Assert.Equal(JObject.FromObject(options), sendOptions);

                Assert.Equal(JObject.FromObject(serverInfo), JObject.FromObject(connection.ServerInfo));
                
                Assert.True(tcpConnection.IsSsl, "should use ssl tcp connection");
            }

            Assert.True(tcpConnection.IsDisposed, "should dispose tcp connection");
        }
        
        [Fact(DisplayName = "should read messages from server")]
        public async Task ReadMessages()
        {
            const string subject = "some_subject";
            const string subscription = "some_subscription";
            const string replyTo = "reply_to";
            const string expectedMessage = "expected message\r\nwith new lines";
            var size = Encoding.UTF8.GetByteCount(expectedMessage);
            var tcpConnection = new MockTcpConnection();
            tcpConnection.ReadBuffer.Enqueue(Encoding.UTF8.GetBytes($"MSG {subject} {subscription} {replyTo} {size}\r\n{expectedMessage}\r\n"));

            var options = new NatsConnectionOptions();
            using (var connection = new NatsConnection((h, p) => tcpConnection, options))
            {
                var messageTask = connection.Messages.OfType<Message<IncomingMessage>>()
                    .Timeout(_timeout)
                    .FirstAsync()
                    .ToTask();
                var cancellationTokenSource = new CancellationTokenSource(_timeout);
                await connection.Connect(cancellationTokenSource.Token);
                
                var message = await messageTask;
                var incomingMessage = message.Data;
                Assert.Equal(subject, incomingMessage.Subject);
                Assert.Equal(subscription, incomingMessage.SubscriptionId);
                Assert.Equal(replyTo, incomingMessage.ReplyTo);
                Assert.Equal(size, incomingMessage.Size);
                Assert.Equal(expectedMessage, Encoding.UTF8.GetString(incomingMessage.Payload));
            }

            Assert.True(tcpConnection.IsDisposed, "should dispose tcp connection");
        }

        [Fact(DisplayName = "should write to server")]
        public async Task WriteMessages()
        {
            var tcpConnection = new MockTcpConnection();
            var options = new NatsConnectionOptions();
            var expectedMessage = Encoding.UTF8.GetBytes("some message");
            var cancellationTokenSource = new CancellationTokenSource(_timeout);
            using (var connection = await NatsConnection.Connect((h, p) => tcpConnection, options, cancellationTokenSource.Token))
            {
                await connection.Write(expectedMessage, CancellationToken.None);
                var wrote = await tcpConnection.OnWrite.Timeout(_timeout).Take(2).LastAsync();

                Assert.Equal(expectedMessage, wrote);
            }

            Assert.True(tcpConnection.IsDisposed, "should dispose tcp connection");
        }

        [Fact(DisplayName = "should reply to ping request")]
        public async Task PingPong()
        {
            var tcpConnection = new MockTcpConnection();
            tcpConnection.ReadBuffer.Enqueue(Encoding.UTF8.GetBytes("PING\r\n"));

            var options = new NatsConnectionOptions();
            var cancellationTokenSource = new CancellationTokenSource(_timeout);
            using (await NatsConnection.Connect((h, p) => tcpConnection, options, cancellationTokenSource.Token))
            {
                var wrote = await tcpConnection.OnWrite.Timeout(_timeout).Take(2).LastAsync();
                var pongMessage = Encoding.UTF8.GetString(wrote);

                Assert.Equal("PONG\r\n", pongMessage);
            }

            Assert.True(tcpConnection.IsDisposed, "should dispose tcp connection");
        }
    }
}