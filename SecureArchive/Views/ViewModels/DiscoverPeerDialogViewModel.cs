using io.github.toyota32k.toolkit.net;
using Microsoft.Extensions.Hosting;
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
