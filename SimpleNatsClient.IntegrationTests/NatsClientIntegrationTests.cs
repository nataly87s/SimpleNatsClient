using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Extensions;
using Xunit;

namespace SimpleNatsClient.IntegrationTests
{
    public class NatsClientIntegrationTests
    {
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(100);
       
        [Fact]
        public async Task PublishSubscribe()
        {
            const string subject = "some_subject";
            const string payload = "some payload";
            using (var subscribeClient = NatsClient.Connect(new NatsConnectionOptions()))
            {
                var subscription = subscribeClient.GetSubscription(subject, 1);
                var messagesTask = subscription.ToArray().Timeout(_timeout).ToTask();
                
                using (var publishClient = NatsClient.Connect(new NatsConnectionOptions()))
                {
                    await publishClient.Publish(subject, payload);
                    await publishClient.Publish(subject, "some other payload");
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
            using (var subscribeClient = NatsClient.Connect(new NatsConnectionOptions()))
            {
                var subscription = subscribeClient.GetSubscription(subject, 1)
                    .SelectMany(message => 
                        Observable.FromAsync( cb => subscribeClient.Publish(message.ReplyTo, reply, cb)));

                using (subscription.Subscribe())
                using (var requestClient = NatsClient.Connect(new NatsConnectionOptions()))
                {
                    var requestResult = await requestClient.Request(subject, _timeout);
                    Assert.Equal(reply, Encoding.UTF8.GetString(requestResult.Payload));
                }
            }
        }
    }
}
