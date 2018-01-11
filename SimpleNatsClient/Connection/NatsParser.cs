using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace SimpleNatsClient.Connection
{
    public class NatsParser
    {
        private enum State
        {
            Message,
            Payload
        }

        private readonly StringBuilder _builder;
        
        private State _state = State.Message;
        private string _nextMessage;
        private byte[] _nextPayload;
        private int _expectedPayloadSize;
        
        private readonly Subject<(string Message, byte[] Payload)> _messages = new Subject<(string Message, byte[] Payload)>();
        public IObservable<(string Message, byte[] Payload)> Messages => _messages;

        public NatsParser() : this(512) {}
        public NatsParser(int capacity)
        {
            _builder = new StringBuilder(capacity);
        }
        
        public void Parse(byte[] buffer, int offset, int count)
        {
            switch (_state)
            {
                case State.Message: 
                    ParseMessage(buffer, offset, count);
                    break;
                case State.Payload:
                    ParsePayload(buffer, offset, count);
                    break;
            }
        }

        private void ParseMessage(byte[] buffer, int offset, int count)
        {
            for (var i = offset; i < offset + count; i++)
            {
                var current = buffer[i];
                if (current == '\n')
                {
                    if (_builder.Length == 0) continue;
                    
                    var message = _builder.ToString();
                    _builder.Clear();

                    if (message.StartsWith("MSG "))
                    {
                        _state = State.Payload;
                        _nextMessage = message;
                        _expectedPayloadSize = int.Parse(message.Split(' ').Last());
                        _nextPayload = new byte[_expectedPayloadSize];
                        Parse(buffer, i + 1, count - i - 1);
                        return;
                    }
                    
                    _messages.OnNext((message, null));
                }
                else if (current != '\r')
                {
                    _builder.Append((char)current);
                }
            }
        }

        private void ParsePayload(byte[] buffer, int offset, int count)
        {
            Array.Copy(buffer, offset, _nextPayload, _nextPayload.Length - _expectedPayloadSize, Math.Min(count, _expectedPayloadSize));
            offset += _expectedPayloadSize;
            _expectedPayloadSize -= count;
            
            if (_expectedPayloadSize > 0) return;
            
            _state = State.Message;
            _messages.OnNext((_nextMessage, _nextPayload));
            _nextMessage = null;
            _nextPayload = null;
            
            if (_expectedPayloadSize == 0) return;

            count = 0 - _expectedPayloadSize;
            _expectedPayloadSize = 0;
            Parse(buffer, offset, count);
        }

        public void Reset()
        {
            _builder.Clear();
            _state = State.Message;
            _nextMessage = null;
            _nextPayload = null;
            _expectedPayloadSize = 0;
        }
    }
}