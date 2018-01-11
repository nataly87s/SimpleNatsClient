using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;
using Xunit;

namespace SimpleNatsClient.Tests
{
    public class NatsParserTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);

        [Fact(DisplayName = "should emit message")]
        public async Task EmitsMessage()
        {
            const string message = "some message";
            var buffer = Encoding.UTF8.GetBytes(message + "\r\n");

            // Arrange
            var parser = new NatsParser();
            var parsedMessageTask = parser.Messages.FirstAsync()
                .Timeout(_timeout).ToTask();

            // Act
            parser.Parse(buffer, 0, buffer.Length);
            var parsedMessage = await parsedMessageTask;

            // Assert;
            Assert.Equal(message, parsedMessage.Message);
            Assert.Null(parsedMessage.Payload);
        }

        [Fact(DisplayName = "should emit all messages")]
        public async Task EmitMultipleMessages()
        {
            const string firstMessage = "some message";
            const string secondMessage = "second message";
            var buffer = Encoding.UTF8.GetBytes(firstMessage + "\r\n" + secondMessage + "\r\n");

            // Arrange
            var parser = new NatsParser();
            var parsedMessagesTask = parser.Messages.Take(2).ToArray()
                .Timeout(_timeout).ToTask();

            // Act
            parser.Parse(buffer, 0, buffer.Length);
            var parsedMessages = await parsedMessagesTask;

            // Assert;
            Assert.Equal(2, parsedMessages.Length);
            Assert.Equal(firstMessage, parsedMessages[0].Message);
            Assert.Equal(secondMessage, parsedMessages[1].Message);
        }

        [Fact(DisplayName = "should add payload to MSG messages")]
        public async Task ParseMsgMessage()
        {
            const string message = "MSG subject 12";
            const string payload = "Hello\r\nNats!";
            var buffer = Encoding.UTF8.GetBytes(message + "\r\n" + payload + "\r\n");

            // Arrange
            var parser = new NatsParser();
            var parsedMessageTask = parser.Messages.FirstAsync()
                .Timeout(_timeout).ToTask();

            // Act
            parser.Parse(buffer, 0, buffer.Length);
            var parsedMessage = await parsedMessageTask;

            // Assert;
            Assert.Equal(message, parsedMessage.Message);
            Assert.Equal(payload, Encoding.UTF8.GetString(parsedMessage.Payload));
        }

        [Fact(DisplayName = "should parse only selected part")]
        public async Task ParseSelected()
        {
            const string message = "some message";
            var extraBytes = Encoding.UTF8.GetBytes("garbage");
            var messageBytes = Encoding.UTF8.GetBytes(message + "\r\n");
            var buffer = extraBytes.Concat(messageBytes).Concat(extraBytes).ToArray();

            // Arrange
            var parser = new NatsParser();
            var parsedMessageTask = parser.Messages.FirstAsync()
                .Timeout(_timeout).ToTask();

            // Act
            parser.Parse(buffer, extraBytes.Length, messageBytes.Length);
            var parsedMessage = await parsedMessageTask;

            // Assert;
            Assert.Equal(message, parsedMessage.Message);
            Assert.Null(parsedMessage.Payload);
        }

        [Fact(DisplayName = "should parse message on multiple buffers")]
        public async Task ParseMultipleBuffers()
        {
            const string message = "some message";
            var messageBytes = Encoding.UTF8.GetBytes(message + "\r\n");            
            var firstPart = new ArraySegment<byte>(messageBytes, 0, messageBytes.Length / 2).ToArray();
            var secondPart = new ArraySegment<byte>(messageBytes, firstPart.Length, messageBytes.Length - firstPart.Length).ToArray();
            
            // Arrange
            var parser = new NatsParser();
            var parsedMessageTask = parser.Messages.FirstAsync()
                .Timeout(_timeout).ToTask();

            // Act
            parser.Parse(firstPart, 0, firstPart.Length);
            parser.Parse(secondPart, 0, secondPart.Length);
            var parsedMessage = await parsedMessageTask;

            // Assert;
            Assert.Equal(message, parsedMessage.Message);
            Assert.Null(parsedMessage.Payload);

        }

        [Theory(DisplayName = "should reset message")]
        [InlineData("partial message")]
        [InlineData("MSG subject 20\r\npartial payload")]
        public async Task Reset(string partialMessage)
        {
            var partialBuffer = Encoding.UTF8.GetBytes(partialMessage);
            
            const string message = "some message";
            var buffer = Encoding.UTF8.GetBytes(message + "\r\n");

            // Arrange
            var parser = new NatsParser();
            var parsedMessageTask = parser.Messages.FirstAsync()
                .Timeout(_timeout).ToTask();

            // Act
            parser.Parse(partialBuffer, 0, partialBuffer.Length);
            parser.Reset();
            parser.Parse(buffer, 0, buffer.Length);
            var parsedMessage = await parsedMessageTask;

            // Assert;
            Assert.Equal(message, parsedMessage.Message);
            Assert.Null(parsedMessage.Payload);
        }
    }
}