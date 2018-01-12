using Newtonsoft.Json;

namespace SimpleNatsClient.Connection
{
    public class ServerInfo
    {
        [JsonProperty("server_id")]
        public string ServerId { get; set; }

        [JsonProperty("version")]
        public string ServerVersion { get; set; }

        [JsonProperty("go")]
        public string GoVersion { get; set; }

        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public int Port { get; set; }

        [JsonProperty("auth_required")]
        public bool AuthRequired { get; set; }

        [JsonProperty("ssl_required")]
        public bool SslRequired { get; set; }

        [JsonProperty("max_payload")]
        public int MaxPayloadSize { get; set; }

        [JsonProperty("connect_urls")]
        public string[] ConnectUrls { get; set; }
    }
}