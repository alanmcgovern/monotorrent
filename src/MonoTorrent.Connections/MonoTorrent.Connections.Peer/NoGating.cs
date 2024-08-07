using MonoTorrent.BEncoding;

using ReusableTasks;

namespace MonoTorrent.Connections.Peer
{
    public sealed class NoGating : IPeerConnectionGate
    {
        public ReusableTask<bool> TryAcceptHandshakeAsync (BEncodedString localPeerId, PeerInfo remotePeer, IPeerConnection connection, InfoHash infoHash)
        {
            return ReusableTask.FromResult (true);
        }
    }
}
