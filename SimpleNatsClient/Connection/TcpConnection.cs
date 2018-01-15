using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Disposables;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleNatsClient.Connection
{
    internal class TcpConnection : ITcpConnection
    {
        private readonly string _hostname;
        private readonly int _port;
        private Stream _stream;
        private bool _isSsl;
        private readonly CompositeDisposable _disposable = new CompositeDisposable();

        public TcpConnection(string hostname = "localhost", int port = 4222)
        {
            _hostname = hostname;
            _port = port;
        }

        public async Task Connect()
        {
            var client = new TcpClient();
            _disposable.Add(client);
            await client.ConnectAsync(_hostname, _port);
            _stream = client.GetStream();
            _disposable.Add(_stream);
        }

        public async Task MakeSsl(RemoteCertificateValidationCallback remoteCertificateValidationCallback, X509Certificate2Collection certificates)
        {
            if (_isSsl) return;
            
            var sslStream = new SslStream(_stream, false, remoteCertificateValidationCallback, null, EncryptionPolicy.RequireEncryption);
            await sslStream.AuthenticateAsClientAsync(_hostname, certificates, SslProtocols.Tls12, true);
            _isSsl = true;
            _disposable.Add(sslStream);
            _stream = sslStream;
        }
        
        public async Task Write(byte[] buffer, CancellationToken cancellationToken)
        {
            await _stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            await _stream.FlushAsync(cancellationToken);
        }

        public async Task<int> Read(byte[] buffer, CancellationToken cancellationToken)
        {
            return await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        }
        
        public void Dispose()
        {
            _disposable.Dispose();
        }
    }
}