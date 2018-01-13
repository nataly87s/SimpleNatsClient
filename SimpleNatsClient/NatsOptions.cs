using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SimpleNatsClient.Connection;

namespace SimpleNatsClient
{
    public class NatsOptions
    {
        public NatsOptions() : this(new NatsConnectionOptions()) { }

        public NatsOptions(NatsConnectionOptions connectionOptions)
        {
            ConnectionOptions = connectionOptions;
        }

        public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan PingPongInterval { get; set; } = TimeSpan.FromSeconds(5);

        public int MaxConnectRetry { get; set; } = 10;

        public TimeSpan ConnectRetryDelay { get; set; } = TimeSpan.FromSeconds(5);

        public NatsConnectionOptions ConnectionOptions { get; }

        public RemoteCertificateValidationCallback RemoteCertificateValidationCallback { get; set; } = RemoteCertificateValidation;

        public X509Certificate2Collection Certificates { get; set; }

        private static bool RemoteCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}