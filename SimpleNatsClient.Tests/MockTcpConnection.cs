using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using SimpleNatsClient.Connection;

namespace SimpleNatsClient.Tests
{
    public class MockTcpConnection : ITcpConnection
    {
        public bool IsDisposed { get; private set; }
        public byte[] ReadBuffer { get; set; }
        
        private readonly ReplaySubject<Unit> _readSubject = new ReplaySubject<Unit>();
        public IObservable<Unit> OnRead => _readSubject;
        
        private readonly ReplaySubject<byte[]> _writeSubject = new ReplaySubject<byte[]>();
        public IObservable<byte[]> OnWrite => _writeSubject;

        public void Dispose()
        {
            IsDisposed = true;
        }

        public Task Write(byte[] buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            _writeSubject.OnNext(buffer);
            return Task.CompletedTask;
        }

        public Task<int> Read(byte[] buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (ReadBuffer == null || ReadBuffer.Length == 0)
                {
                    return Task.Delay(5000, cancellationToken).ContinueWith(_ => 0, cancellationToken);
                }
                
                var length = Math.Min(ReadBuffer.Length, buffer.Length);
                Array.Copy(ReadBuffer, buffer, length);

                ReadBuffer = ReadBuffer.Length > buffer.Length
                    ? new ArraySegment<byte>(ReadBuffer, length, ReadBuffer.Length - length).ToArray()
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