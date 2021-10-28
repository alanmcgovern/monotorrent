using System;

using MonoTorrent.Client.Connections;

namespace MonoTorrent.Client
{
    static class FactoriesExtensions
    {
        public static IPeerConnection CreatePeerConnection(this Factories factories, Uri uri)
        {
            try {
                if (factories.PeerConnectionCreators.TryGetValue (uri.Scheme, out var creator))
                    return creator (uri);
            } catch {

            }
            return null;
        }
    }
}
