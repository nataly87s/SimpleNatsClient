using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Messages;

namespace SimpleNatsClient.Connection
{
    public interface INatsConnection : IDisposable
    {
        ServerInfo ServerInfo { get; }
        IConnectableObservable<Message> Messages { get; }
        Task Write(byte[] buffer, CancellationToken cancellationToken);
    }
}