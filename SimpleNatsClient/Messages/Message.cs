namespace SimpleNatsClient.Messages
{
    public class Message
    {
        public string Op { get; }

        public Message(string op)
        {
            Op = op;
        }

        public override string ToString()
        {
            return $"[{Op}]";
        }

        public static Message<T> From<T>(string op, T data)
        {
            return new Message<T>(op, data);
        }
    }

    public class Message<T> : Message
    {
        public T Data { get; }

        public Message(string op, T data) : base(op)
        {
            Data = data;
        }
        
        public override string ToString()
        {
            return $"[{Op}] {Data}";
        }
    }
}