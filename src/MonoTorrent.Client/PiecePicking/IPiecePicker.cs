using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    interface IPiecePicker
    {
        bool IsInteresting(PeerConnectionID id);
        RequestMessage PickPiece(PeerConnectionID id, Peers otherPeers);
        BitField MyBitField { get; }
        int CurrentRequestCount();
        void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage message);
        void RemoveRequests(PeerConnectionID id);
        void ReceivedPieceMessage(PeerConnectionID id, byte[] buffer, int dataOffset, int writeIndex, int blockLength, PieceMessage message);
    }
}
