using System;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json;

namespace SimpleNatsClient.Connection
{
    public class NatsConnectionOptions
    {
        [JsonIgnore]
        public string Hostname { get; set; } = "localhost";

        [JsonIgnore]
        public int Port { get; set; } = 4222;

        [JsonIgnore]
        public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(5);

        [JsonIgnore]
        public TimeSpan PingPongInterval { get; set; } = TimeSpan.FromSeconds(5);

        [JsonIgnore]
        public int MaxConnectRetry { get; set; } = 10;

        [JsonIgnore]
        public TimeSpan ConnectRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

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
        public string Language => "csharp";

        private static readonly string _version = Assembly.GetAssembly(typeof(NatsClient)).GetName().Version.ToString();

        [JsonProperty("version")]
        public string Version => _version;

        [JsonProperty("protocol")]
        public int Protocol => 1;

        [JsonIgnore]
        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; } = RemoteCertificateValidation;

        [JsonIgnore]
        public X509Certificate2Collection Certificates { get; set; }

        private static bool RemoteCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}