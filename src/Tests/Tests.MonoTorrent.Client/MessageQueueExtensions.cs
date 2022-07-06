using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.Libtorrent;

namespace MonoTorrent.Client
{
    static class MessageQueueExtensions
    {
        public static PeerId AddConnectedPeer (this TorrentManager manager, bool supportsLTMetdata = false)
        {
            var peer = PeerId.CreateNull (manager.Bitfield.Length, manager.InfoHashes.V1OrV2);
            manager.Peers.ConnectedPeers.Add (peer);
            if (supportsLTMetdata) {
                peer.SupportsFastPeer = true;
                peer.SupportsLTMessages = true;
                peer.ExtensionSupports.Add (LTMetadata.Support);
            }
            return peer;
        }

        public static PeerMessage TryDequeue (this MessageQueue queue)
            => queue.TryDequeue (out PeerMessage message, out PeerMessage.Releaser releaser) ? message : null;
    }
}
