using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Extensions;
using Xunit;

namespace SimpleNatsClient.IntegrationTests
{
    public class NatsClientIntegrationTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(500);
       
        [Fact]
        public async Task PublishSubscribe()
        {
            const string subject = "some_subject";
            const string payload = "some payload";
            var cancellationToken = new CancellationTokenSource(_timeout).Token;
            using (var subscribeClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
            {
                var subscription = subscribeClient.GetSubscription(subject, 1);
                var messagesTask = subscription.ToArray().ToTask(cancellationToken);
                
                using (var publishClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
                {
                    await publishClient.Publish(subject, payload, cancellationToken);
                    await publishClient.Publish(subject, "some other payload", cancellationToken);
                }

                var messages = await messagesTask;
                Assert.Equal(1, messages.Length);
                Assert.Equal(subject, messages[0].Subject);
                Assert.Equal(payload, Encoding.UTF8.GetString(messages[0].Payload));
            }
        }
        
        [Fact]
        public async Task Request()
        {
            const string subject = "some_subject";
            const string reply = "some reply";
            var cancellationToken = new CancellationTokenSource(_timeout).Token;
            using (var subscribeClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
            {
                var subscription = subscribeClient.GetSubscription(subject, 1)
                    .SelectMany(message => Observable.FromAsync(ct => subscribeClient.Publish(message.ReplyTo, reply, ct)));

                using (subscription.Subscribe())
                using (var requestClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
                {
                    var requestResult = await requestClient.Request(subject, cancellationToken);
                    Assert.Equal(reply, Encoding.UTF8.GetString(requestResult.Payload));
                }
            }
        }
    }
}
