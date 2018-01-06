using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SimpleNatsClient.Connection;

namespace SimpleNatsClient.Messages
{
    public static class MessageDeserializer
    {
        public static Message Deserialize(string message, byte[] payload)
        {
            var index = message.IndexOf(' ');
            if (index < 0) return new Message(message);

            var op = message.Substring(0, index);
            var extraData = message.Substring(index + 1);

            switch (op)
            {
                case "INFO":
                    var serverInfo = JsonConvert.DeserializeObject<ServerInfo>(extraData);
                    return Message.From(op, serverInfo);
                case "MSG":
                    var incomingMessage = new IncomingMessage(extraData, payload);
                    return Message.From(op, incomingMessage);
                default:
                    return Message.From(op, extraData);
            }
        }
    }
}