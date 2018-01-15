using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleNatsClient.Connection
{
    internal delegate Task<ITcpConnection> TcpConnectionProvider(string host, int port);
    internal interface ITcpConnection : IDisposable
    {
        Task MakeSsl(RemoteCertificateValidationCallback remoteCertificateValidationCallback, X509Certificate2Collection certificates);
        Task Write(byte[] buffer, CancellationToken cancellationToken);
        Task<int> Read(byte[] buffer, CancellationToken cancellationToken);
    }
}