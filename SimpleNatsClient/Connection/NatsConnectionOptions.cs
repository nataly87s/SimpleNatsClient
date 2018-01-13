using System.Reflection;
using Newtonsoft.Json;

namespace SimpleNatsClient.Connection
{
    public class NatsConnectionOptions
    {
        private static readonly string _version = Assembly.GetAssembly(typeof(NatsClient)).GetName().Version.ToString();

        [JsonProperty("verbose")]
        public bool Verbose { get; set; }

        [JsonProperty("pedantic")]
        public bool Pedantic { get; set; }

        [JsonProperty("ssl_required")]
        public bool SslRequired { get; set; }

        [JsonProperty("auth_token")]
        public string AuthToken { get; set; }

        [JsonProperty("user")]
        public string UserName { get; set; }

        [JsonProperty("pass")]
        public string Password { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = "SimpleNatsClient";

        [JsonProperty("lang")]
        public string Language { get; set; } = "csharp";

        [JsonProperty("version")]
        public string Version { get; set; } = _version;

        [JsonProperty("protocol")]
        public int Protocol { get; set; } = 1;
    }
}