using Newtonsoft.Json;

namespace SimpleNatsClient.Connection
{
    public class NatsConnectionOptions
    {
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
        public string Version { get; set; } = "0.1.0";
        
        [JsonProperty("protocol")]
        public int Protocol { get; set; }
    }
}