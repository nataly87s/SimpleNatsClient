using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Extensions;
using Xunit;

namespace SimpleNatsClient.Tests
{
    public class NatsClientTests
    {
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
    }
}