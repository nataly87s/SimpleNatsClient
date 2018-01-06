using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleNatsClient.Connection
{
    internal class TcpConnection : ITcpConnection
    {
        private readonly TcpClient _client;
        private readonly Stream _stream;

        public TcpConnection(string hostname = "localhost", int port = 4222)
        {
            _client = new TcpClient(hostname, port);
            _stream = _client.GetStream();
        }

        public async Task Write(byte[] buffer, CancellationToken cancellationToken = default (CancellationToken))
        {
            await _stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }

        public async Task<int> Read(byte[] buffer, CancellationToken cancellationToken = default (CancellationToken))
        {
            return await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        }
        
        public void Dispose()
        {
            _stream.Dispose();
            _client.Dispose();
        }
    }
}