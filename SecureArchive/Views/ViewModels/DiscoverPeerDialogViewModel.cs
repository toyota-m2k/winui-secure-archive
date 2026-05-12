using io.github.toyota32k.toolkit.net;
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

namespace SecureArchive.Views.ViewModels {
    internal class DiscoverPeerDialogViewModel {
        IMainThreadService _mainThreadService;

        public DiscoverPeerDialogViewModel(IMainThreadService mainThreadService) {
            _mainThreadService = mainThreadService;
        }

        // Browser に渡す discovery 結果コレクション (参照は不変)。
        public ObservableCollection<DiscoveredPeer> DiscoveredPeers { get; } = new();
        public ReactivePropertySlim<bool> NoPeer { get; } = new(true);
        public ReactivePropertySlim<DiscoveredPeer?> SelectedPeer { get; } = new ();
        public ReactiveCommand CancelDiscoveringCommand { get; } = new();

        private TaskCompletionSource<DiscoveredPeer?>? _tcs = null;

        private int findPeerIndex(DiscoveredPeer peer) {
            for(int i=0; i<DiscoveredPeers.Count; i++) {
                if(string.Compare(DiscoveredPeers[i].InstanceName, peer.InstanceName, StringComparison.CurrentCultureIgnoreCase)==0) {
                    return i;
                }
            }
            return -1;
        }

        public void Release() {
            _tcs?.TrySetResult(null);
            _tcs = null;
        }

        public async Task<DiscoveredPeer?> Discover() {
            if (_tcs != null) return await _tcs.Task;

            var tcs = new TaskCompletionSource<DiscoveredPeer?>();
            _tcs = tcs;
            using (new MdnsBrowser(info => {
                _mainThreadService.Run(() => {
                    var i = findPeerIndex(info.Peer);
                    if (info.Type == MdnsBrowser.UpdateInfo.UpdateType.AddOrUpdate) {
                        if (i >= 0) {
                            DiscoveredPeers[i] = info.Peer;
                        }
                        else {
                            DiscoveredPeers.Add(info.Peer);
                        }
                    }
                    else if (info.Type == MdnsBrowser.UpdateInfo.UpdateType.Remove) {
                        if (i >= 0) {
                            DiscoveredPeers.RemoveAt(i);
                        }
                    }
                    NoPeer.Value = DiscoveredPeers.IsEmpty();
                });
            }))
            using (SelectedPeer.Subscribe(peer => {
                if (peer != null) {
                    tcs.TrySetResult(peer);
                }
            }))
            using (CancelDiscoveringCommand.Subscribe(() => {
                tcs.TrySetResult(null);
            })) {
                return await tcs.Task;
            }
        }
    }
}
