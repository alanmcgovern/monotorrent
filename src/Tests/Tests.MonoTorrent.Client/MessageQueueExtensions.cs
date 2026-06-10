using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.Libtorrent;
using MonoTorrent.Messages;

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
                peer.ExtensionSupports.Add (MessageEncoder.Extended.MetadataExchangeSupport);
            }
            return peer;
        }

        public static ReadOnlyMemory<byte> TryDequeue (this MessageQueue queue)
            => queue.TryDequeue (out Memory<byte> message, out ByteBufferPool.Releaser releaser) ? message : null;
    }
}
