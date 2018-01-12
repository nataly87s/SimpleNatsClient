using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Reactive;
using System.Reactive.Subjects;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SimpleNatsClient.Connection;

namespace SimpleNatsClient.Tests
{
    internal class MockTcpConnection : ITcpConnection
    {
        public bool IsSsl { get; private set; }
        public bool IsDisposed { get; private set; }
        public Queue<byte[]> Queue { get; } = new Queue<byte[]>();
        private byte[] _currentBuffer;

        public MockTcpConnection(ServerInfo serverInfo = null)
        {
            serverInfo = serverInfo ?? new ServerInfo();
            var infoMessage = $"INFO {JsonConvert.SerializeObject(serverInfo)}\r\n";
            Queue.Enqueue(Encoding.UTF8.GetBytes(infoMessage));
        }

        private readonly ReplaySubject<Unit> _readSubject = new ReplaySubject<Unit>();
        public IObservable<Unit> OnRead => _readSubject;

        private readonly ReplaySubject<byte[]> _writeSubject = new ReplaySubject<byte[]>();
        public IObservable<byte[]> OnWrite => _writeSubject;

        public void Dispose()
        {
            IsDisposed = true;
        }

        public Task MakeSsl(RemoteCertificateValidationCallback remoteCertificateValidationCallback,
            X509Certificate2Collection certificates)
        {
            IsSsl = true;
            return Task.CompletedTask;
        }

        public Task Write(byte[] buffer, CancellationToken cancellationToken)
        {
            _writeSubject.OnNext(buffer);
            return Task.CompletedTask;
        }

        public Task<int> Read(byte[] buffer, CancellationToken cancellationToken)
        {
            try
            {
                if (_currentBuffer == null && !Queue.TryDequeue(out _currentBuffer))
                {
                    return Task.Delay(5000, cancellationToken).ContinueWith(_ => 0, cancellationToken);
                }

                var length = Math.Min(_currentBuffer.Length, buffer.Length);
                Array.Copy(_currentBuffer, buffer, length);

                _currentBuffer = _currentBuffer.Length > buffer.Length
                    ? new ArraySegment<byte>(_currentBuffer, length, _currentBuffer.Length - length).ToArray()
                    : null;

                return Task.FromResult(length);
            }
            finally
            {
                _readSubject.OnNext(Unit.Default);
            }
        }
    }
}