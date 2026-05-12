using io.github.toyota32k.toolkit.net;
using Newtonsoft.Json;
using Reactive.Bindings;
using SecureArchive.DI;
using SecureArchive.Utils.Server.mdns;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using Windows.Networking;

namespace SecureArchive.Views.ViewModels {
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
                if (ServiceName!=null) {
                    return $"{ServiceName} ({Address}) [{scheme}]";
                } else {
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

        public string ToJson() {
            return JsonConvert.SerializeObject(this);
        }
        public static PeerHost? FromJson(string? json) {
            if (json==null) return null;
            return JsonConvert.DeserializeObject<PeerHost>(json);
        }

        public PeerHost(string address, string? serviceName, string? hostname, bool isHttps, string? fingerprint) {
            Address = address;
            ServiceName = serviceName;
            Hostname = hostname;
            IsHttps = isHttps;
            Fingerprint = fingerprint;
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
    }
    internal class DiscoverPeerDialogViewModel {
        IMainThreadService _mainThreadService;

        public DiscoverPeerDialogViewModel(IMainThreadService mainThreadService) {
            _mainThreadService = mainThreadService;
        }

        // Browser に渡す discovery 結果コレクション (参照は不変)。
        public ObservableCollection<PeerHost> DiscoveredPeers { get; } = new();
        public ReactivePropertySlim<bool> NoPeer { get; } = new(true);
        public ReactivePropertySlim<PeerHost?> SelectedPeer { get; } = new ();
        public ReactiveCommand CancelDiscoveringCommand { get; } = new();

        private TaskCompletionSource<PeerHost?>? _tcs = null;

        private int findPeerIndex(DiscoveredPeer peer) {
            for(int i=0; i<DiscoveredPeers.Count; i++) {
                if(string.Compare(DiscoveredPeers[i].ServiceName, peer.InstanceName, StringComparison.CurrentCultureIgnoreCase)==0) {
                    return i;
                }
            }
            return -1;
        }

        public void Release() {
            _tcs?.TrySetResult(null);
            _tcs = null;
        }

        public async Task<PeerHost?> Discover() {
            if (_tcs != null) return await _tcs.Task;

            var browser = new MdnsBrowser(info => {
                _mainThreadService.Run(() => {
                    var i = findPeerIndex(info.Peer);
                    if (info.Type == MdnsBrowser.UpdateInfo.UpdateType.AddOrUpdate) {
                        if (i >= 0) {
                            DiscoveredPeers[i] = PeerHost.FromDiscoveredPeer(info.Peer);
                        }
                        else {
                            DiscoveredPeers.Add(PeerHost.FromDiscoveredPeer(info.Peer));
                        }
                    }
                    else if (info.Type == MdnsBrowser.UpdateInfo.UpdateType.Remove) {
                        if (i >= 0) {
                            DiscoveredPeers.RemoveAt(i);
                        }
                    }
                    NoPeer.Value = DiscoveredPeers.IsEmpty();
                });
            });
            var tcs = new TaskCompletionSource<PeerHost?>();
            _tcs = tcs;
            using (browser)
            using (SelectedPeer.Subscribe(peer => {
                if (peer != null) {
                    tcs.TrySetResult(peer);
                }
            }))
            using (CancelDiscoveringCommand.Subscribe(() => {
                tcs.TrySetResult(null);
            })) {
                browser.Start();
                return await tcs.Task;
            }
        }
    }
}
