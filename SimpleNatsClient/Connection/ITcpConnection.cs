using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleNatsClient.Connection
{
    public interface ITcpConnection : IDisposable
    {
        Task Write(byte[] buffer, CancellationToken cancellationToken = default (CancellationToken));
        Task<int> Read(byte[] buffer, CancellationToken cancellationToken = default (CancellationToken));
    }
}