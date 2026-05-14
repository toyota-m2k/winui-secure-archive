using Newtonsoft.Json;
using SecureArchive.Utils.Server.mdns;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SecureArchive.Views.ViewModels {
    interface IHttpClient : IDisposable {
        TimeSpan Timeout { get; set; }
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken? cancellationToken=null);
        Task<HttpResponseMessage> GetAsync([StringSyntax(StringSyntaxAttribute.Uri)] string requestUri, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken? cancellationToken = null);
        Task<HttpResponseMessage> PutAsync([StringSyntax(StringSyntaxAttribute.Uri)] string requestUri, HttpContent? content, CancellationToken? cancellationToken = null);
        Task<HttpResponseMessage> PostAsync([StringSyntax(StringSyntaxAttribute.Uri)] string requestUri, HttpContent? content, CancellationToken? cancellationToken = null);
    }

    internal class PeerHost {
        [JsonProperty("address")]
        public string Address { get; }
        [JsonProperty("service")]
        public string? ServiceName { get; }      // InstanceName
        [JsonProperty("hostname")]
        public string? Hostname { get; }
        [JsonProperty("https")]
        public bool IsHttps { get; }
        [JsonProperty("fingerprint")]
        public string? Fingerprint { get; }

        public string DisplayLabel {
            get {
                var scheme = IsHttps ? "HTTPS" : "HTTP";
                if (ServiceName != null) {
                    return $"{ServiceName} ({Address}) [{scheme}]";
                }
                else {
                    return $"{Address} [{scheme}]";
                }
            }
        }

        public override bool Equals(object? obj) {
            if (obj is PeerHost other) {
                return Address == other.Address
                    && IsHttps == other.IsHttps
                    && Fingerprint == other.Fingerprint
                    && ServiceName == other.ServiceName
                    && Hostname == other.Hostname;
            }
            return false;
        }

        public override int GetHashCode() {
            return $"{Address}|{IsHttps}|{Fingerprint}|{ServiceName}|{Hostname}".GetHashCode();
        }

        public string ToJson() {
            return JsonConvert.SerializeObject(this);
        }
        public static PeerHost? FromJson(string? json) {
            if (json == null) return null;
            return JsonConvert.DeserializeObject<PeerHost>(json);
        }

        public string Scheme => IsHttps ? "https" : "http";
        public string BaseUrl => $"{Scheme}://{Address}/";

        public string MakeUrl(string path) {
            return $"{BaseUrl}{path.TrimStart('/')}";
        }

        public PeerHost(string address, string? serviceName, string? hostname, bool isHttps, string? fingerprint) {
            Address = address;
            ServiceName = serviceName;
            Hostname = hostname;
            IsHttps = isHttps;
            Fingerprint = NormalizeFp(fingerprint);
        }
        public static PeerHost PairedHost(string address, string serverName, string hostname, bool isHttps, string? fingerprint) {
            return new PeerHost(address, serverName, hostname, isHttps, fingerprint);
        }
        public static PeerHost? DirectHost(string? address, bool isHttps) {
            if (string.IsNullOrEmpty(address)) return null;
            return new PeerHost(address, null, null, isHttps, null);
        }
        public static PeerHost FromDiscoveredPeer(DiscoveredPeer peer) {
            return new PeerHost(peer.HostAddress, peer.InstanceName, peer.Hostname, peer.IsHttps, peer.Fingerprint);
        }

        /**
         * SHA-256 指紋を "AB:CD:..." 形式のコロン区切り大文字 16 進に正規化する。
         * 文字列に不要文字 ("-", スペース等) が混ざっていても 16 進文字だけ抽出。
         * 64 桁 (32 バイト) ぴったりでないものは入力をそのまま大文字化して返す (比較は呼び出し側で行う)。
         */
        public static string NormalizeFp(string? fp) {
            if (string.IsNullOrWhiteSpace(fp)) return "";
            var hex = new string(fp.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (hex.Length != 64) return hex;
            var sb = new StringBuilder(95);
            for (int i = 0; i < 64; i += 2) {
                if (i > 0) sb.Append(':');
                sb.Append(hex[i]).Append(hex[i + 1]);
            }
            return sb.ToString();
        }

        // HttpClient Wrapper
        private class PeerHostHttpClient : IHttpClient {
            private readonly PeerHost _peerHost;
            private readonly HttpClient _httpClient;

            public PeerHostHttpClient(PeerHost peerHost, IHttpClientFactory factory) {
                _peerHost = peerHost;
                _httpClient = factory.CreateClient(HTTP_CLIENT_FACTORY_NAME);
            }

            public void Dispose() {
                _httpClient.Dispose();
            }
            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken? cancellationToken) {
                request.Options.Set(KEY, _peerHost);
                return _httpClient.SendAsync(request, cancellationToken ?? CancellationToken.None);
            }

            public Task<HttpResponseMessage> GetAsync([StringSyntax("Uri")] string requestUri, HttpCompletionOption completionOption, CancellationToken? cancellationToken) {
                var uri = new Uri(requestUri, UriKind.RelativeOrAbsolute);
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                return SendAsync(req, cancellationToken);
            }

            public Task<HttpResponseMessage> PostAsync([StringSyntax("Uri")] string requestUri, HttpContent? content, CancellationToken? cancellationToken) {
                var uri = new Uri(requestUri, UriKind.RelativeOrAbsolute);
                var req = new HttpRequestMessage(HttpMethod.Post, uri) {
                    Content = content
                };
                return SendAsync(req, cancellationToken);
            }

            public Task<HttpResponseMessage> PutAsync([StringSyntax("Uri")] string requestUri, HttpContent? content, CancellationToken? cancellationToken) {
                var uri = new Uri(requestUri, UriKind.RelativeOrAbsolute);
                var req = new HttpRequestMessage(HttpMethod.Put, uri) {
                    Content = content
                };
                return SendAsync(req, cancellationToken);
            }
            public TimeSpan Timeout {
                get => _httpClient.Timeout;
                set => _httpClient.Timeout = value;
            }
        }
        public static readonly string HTTP_CLIENT_FACTORY_NAME = "PeerHostHttpClientFactory";
        public static readonly HttpRequestOptionsKey<PeerHost> KEY = new("PeerHost");
        public IHttpClient CreateHttpClient(IHttpClientFactory factory) {
            return new PeerHostHttpClient(this, factory);
        }
    }
}
