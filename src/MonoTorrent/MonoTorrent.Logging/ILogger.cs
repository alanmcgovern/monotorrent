using System;

using MonoTorrent.Client;

namespace MonoTorrent.Logging
{
    public interface ILogger
    {
        /*
        void IncomingConnectionEstablished (Uri uri);
        void OutgoingConnectionEstablished (Uri uri);
        void ConnectionBlocked (Uri uri);
        void ConnectionClosed (Uri uri);
        void PieceFailedHashCheck (TorrentManager manager, int pieceIndex);
        */

        void Log (LogType type, params object[] data);
    }
}
