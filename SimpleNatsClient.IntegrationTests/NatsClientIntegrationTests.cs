using System;
using System.Linq;
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
            string[] payloads = {"some payload", "some other payload"};

            var cancellationToken = new CancellationTokenSource(_timeout).Token;
            using (var subscribeClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
            {
                var messagesTask = subscribeClient.GetSubscription(subject)
                    .Take(payloads.Length)
                    .ToArray().ToTask(cancellationToken);

                using (var publishClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
                {
                    foreach (var payload in payloads)
                    {
                        await publishClient.Publish(subject, payload, cancellationToken);
                        await Task.Delay(1, cancellationToken); // TODO: check why the test fails without the delay
                    }
                }

                var messages = await messagesTask;

                Assert.Equal(payloads.Length, messages.Length);
                Assert.All(messages, m => Assert.Equal(subject, m.Subject));
                Assert.Equal(payloads, messages.Select(m => Encoding.UTF8.GetString(m.Payload)));
            }
        }

        [Fact]
        public async Task AutoUnsubscribe()
        {
            const string subject = "some_subject";
            string[] payloads = {"some payload", "some other payload"};
            var cancellationToken = new CancellationTokenSource(_timeout).Token;
            using (var subscribeClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
            {
                var messagesTask = subscribeClient.GetSubscription(subject, 1)
                    .ToArray()
                    .ToTask(cancellationToken);

                using (var publishClient = await NatsClient.Connect(new NatsConnectionOptions(), cancellationToken))
                {
                    foreach (var payload in payloads)
                    {
                        await publishClient.Publish(subject, payload, cancellationToken);
                        await Task.Delay(1, cancellationToken);
                    }
                }

                var messages = await messagesTask;
                Assert.Single(messages);
                Assert.Equal(subject, messages[0].Subject);
                Assert.Equal(payloads[0], Encoding.UTF8.GetString(messages[0].Payload));
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
                    .SelectMany(message =>
                        Observable.FromAsync(ct => subscribeClient.Publish(message.ReplyTo, reply, ct)));

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