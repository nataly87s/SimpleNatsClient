﻿using System;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;
using SimpleNatsClient.Extensions;
using SimpleNatsClient.Messages;

[assembly: InternalsVisibleTo("SimpleNatsClient.Tests")]

namespace SimpleNatsClient
{
    public class NatsClient : IDisposable
    {
        private static readonly byte[] NewLine = Encoding.UTF8.GetBytes("\r\n");

        private readonly IObservable<IncomingMessage> _inbox;
        private readonly string _inboxPrefix = $"_INBOX.{Guid.NewGuid():N}.";
        
        public INatsConnection Connection { get; }

        public NatsClient(INatsConnection connection)
        {
            Connection = connection;
            _inbox = Connection.OnConnect
                .Select(_ => GetSubscription(_inboxPrefix + "*"))
                .Switch()
                .Publish()
                .RefCount();
        }

        public string NewInbox()
        {
            return _inboxPrefix + Guid.NewGuid().ToString("N");
        }

        public Task Publish(string subject, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Publish(subject, null, null, cancellationToken);
        }

        public Task Publish(string subject, byte[] data, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Publish(subject, null, data, cancellationToken);
        }

        public async Task Publish(string subject, string replyTo, byte[] data, CancellationToken cancellationToken = default(CancellationToken))
        {
            var dataLength = data?.Length ?? 0;
            var message = string.IsNullOrEmpty(replyTo)
                ? $"PUB {subject} {dataLength}"
                : $"PUB {subject} {replyTo} {dataLength}";
            var encodedMessage = Encoding.UTF8.GetBytes(message);

            var encoded = new byte[message.Length + (2 * NewLine.Length) + dataLength];
            encodedMessage.CopyTo(encoded, 0);
            NewLine.CopyTo(encoded, encodedMessage.Length);
            if (dataLength > 0) data.CopyTo(encoded, encodedMessage.Length + NewLine.Length);
            NewLine.CopyTo(encoded, encoded.Length - NewLine.Length);

            await Connection.Write(encoded, cancellationToken);
        }

        public Task<IncomingMessage> Request(string subject, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Request(subject, null, cancellationToken);
        }

        public async Task<IncomingMessage> Request(string subject, byte[] data, CancellationToken cancellationToken = default(CancellationToken))
        {
            var inbox = NewInbox();
            var reply = _inbox.FirstAsync(x => x.Subject == inbox).ToTask(cancellationToken);
            await Publish(subject, inbox, data, cancellationToken);
            return await reply;
        }

        public IObservable<IncomingMessage> GetSubscription(string subject)
        {
            var sid = Guid.NewGuid().ToString("N");
            return Observable.FromAsync(ct => Connection.Write($"SUB {subject} {sid}", ct))
                .SelectMany(_ => GetMessagesForSubscription(sid))
                .Finally(async () =>
                {
                    try
                    {
                        await Connection.Write($"UNSUB {sid}");
                    } 
                    catch {}
                });
        }

        public IObservable<IncomingMessage> GetSubscription(string subject, int messageCount)
        {
            if (messageCount <= 0)
            {
                throw new ArgumentException("message count should be greater than 0", nameof(messageCount));
            }

            var sid = Guid.NewGuid().ToString("N");
            var currentCount = 0;
            return Observable.FromAsync(ct => Connection.Write($"SUB {subject} {sid}\r\nUNSUB {sid} {messageCount}", ct))
                .SelectMany(_ => GetMessagesForSubscription(sid))
                .Do(_ => currentCount++)
                .Take(messageCount)
                .Finally(async () =>
                {
                    if (currentCount < messageCount)
                    {
                        try
                        {
                            await Connection.Write($"UNSUB {sid}");
                        }
                        catch {}
                    }
                });
        }

        private IObservable<IncomingMessage> GetMessagesForSubscription(string sid)
        {
            return Connection.Messages.OfType<Message<IncomingMessage>>()
                .Select(x => x.Data)
                .Where(x => x.SubscriptionId == sid);
        }

        public void Dispose()
        {
            Connection.Dispose();
        }

        public static async Task<NatsClient> Connect(NatsConnectionOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            var natsConnection = await NatsConnection.Connect(options, cancellationToken);
            return new NatsClient(natsConnection);
        }
    }
}