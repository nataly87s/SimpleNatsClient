using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Extensions;
using Xunit;

namespace SimpleNatsClient.IntegrationTests
{
    public class NatsClientIntegrationTests
    {
        private const int SECONDARY_PORT = 4223;
        private static readonly bool _isCi = Environment.GetEnvironmentVariable("CI") == "true";
        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(500);        
        private static readonly NatsOptions _natsOptions = new NatsOptions
        {
            ConnectRetryDelay = TimeSpan.FromMilliseconds(10),
            MaxConnectRetry = 3,
        };

        private static async Task<NatsClient> Connect(int port = 4222)
        {
            var hostname = _isCi ? $"nats-{port - 4222}" : "localhost";
            var cancellationToken = new CancellationTokenSource(_timeout).Token;
            return await NatsClient.Connect(hostname, port, _natsOptions, cancellationToken);
        }
        
        [Fact]
        public async Task PublishSubscribe()
        {
            const string subject = "some_subject";
            string[] payloads = {"some payload", "some other payload"};

            using (var subscribeClient = await Connect(SECONDARY_PORT))
            {
                var messagesTask = subscribeClient.GetSubscription(subject)
                    .Take(payloads.Length)
                    .ToArray()
                    .Timeout(_timeout)
                    .ToTask();

                using (var publishClient = await Connect())
                {
                    foreach (var payload in payloads)
                    {
                        await publishClient.Publish(subject, payload, new CancellationTokenSource(_timeout).Token);
                        await Task.Delay(1); // TODO: check why the test fails without the delay
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
            using (var subscribeClient = await Connect(SECONDARY_PORT))
            {
                var messagesTask = subscribeClient.GetSubscription(subject, 1)
                    .ToArray()
                    .Timeout(_timeout)
                    .ToTask();

                using (var publishClient = await Connect())
                {
                    foreach (var payload in payloads)
                    {
                        await publishClient.Publish(subject, payload, new CancellationTokenSource(_timeout).Token);
                        await Task.Delay(1);
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
            using (var subscribeClient = await Connect(SECONDARY_PORT))
            {
                var subscription = subscribeClient.GetSubscription(subject, 1)
                    .SelectMany(message =>
                        Observable.FromAsync(ct => subscribeClient.Publish(message.ReplyTo, reply, ct)));

                using (subscription.Subscribe())
                using (var requestClient = await Connect())
                {
                    var requestResult = await requestClient.Request(subject, new CancellationTokenSource(_timeout).Token);
                    Assert.Equal(reply, Encoding.UTF8.GetString(requestResult.Payload));
                }
            }
        }
    }
}