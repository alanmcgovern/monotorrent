using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client.PeerMessages;

namespace MonoTorrent.Client
{
    internal class EndGamePieceManager
    {
        /*
        List<Piece> endGamePieces;
        private RequestMessage EndGamePicker(PeerConnectionID id, Peers otherPeers)
        {
            lock (this.myBitfield)
            {
                if (this.endGamePieces == null)
                    CalculateEndGamePieces(id);

                for (int i = 0; i < this.endGamePieces.Count; i++)
                {
                    if (!id.Peer.Connection.BitField[this.endGamePieces[i].Index])
                        continue;

                    Piece p = this.endGamePieces[i];
                    for (int j = 0; j < p.Blocks.Length; j++)
                    {
                        if (p[j].Received)
                            continue;

                        p[j].Requested = true;
                        return p[j].CreateRequest();
                    }
                }
            }

            return null;    // He has nothing we want
        }


        private void CalculateEndGamePieces(PeerConnectionID id)
        {
            this.endGameActive = true;
            this.endGamePieces = new List<Piece>();
            for (int i = 0; i < this.myBitfield.Length; i++)
                if (!this.myBitfield[i])
                {
                    this.myBitfield[i] = true;
                    this.endGamePieces.Add(new Piece(i, id.TorrentManager.Torrent));
                }

            foreach (KeyValuePair<PeerConnectionID, List<Piece>> keypair in this.requests)
                foreach (Piece p in keypair.Value)
                    this.endGamePieces.Add(p);
        }


        private void ReceivedEndGameMessage(PeerConnectionID id, byte[] recieveBuffer, int offset, int writeIndex, int p, PieceMessage message)
        {
            Piece piece = null;
            Block receivedBlock = null;

            lock (this.requests)
            {
                for (int i = 0; i < this.endGamePieces.Count; i++)
                {
                    if (this.endGamePieces[i].Index != message.PieceIndex)
                        continue;

                    piece = this.endGamePieces[i];
                    for (int j = 0; j < piece.Blocks.Length; j++)
                        if (piece[j].StartOffset == message.StartOffset)
                            receivedBlock = piece[j];

                    if (receivedBlock == null)
                        return;

                    id.Peer.Connection.AmRequestingPiecesCount--;
                    if (receivedBlock.RequestLength != message.BlockLength)
                        throw new Exception("Request length should match block length");

                    receivedBlock.Received = true;
                    id.TorrentManager.FileManager.Write(recieveBuffer, offset, writeIndex, p);


                    if (!piece.AllBlocksReceived)
                        return;

                    bool result = ToolBox.ByteMatch(id.TorrentManager.Torrent.Pieces[piece.Index], id.TorrentManager.FileManager.GetHash(piece.Index));
                    this.myBitfield[message.PieceIndex] = result;

                    id.TorrentManager.HashedPiece(new PieceHashedEventArgs(piece.Index, result));

                    if (result)
                    {
                        id.Peer.Connection.IsInterestingToMe = this.IsInteresting(id);
                        id.TorrentManager.PieceCompleted(piece.Index);
                        this.endGamePieces.Remove(piece);
                    }
                    else
                    {
                        for (int k = 0; k < piece.Blocks.Length; k++)
                        {
                            piece[k].Requested = false;
                            piece[k].Received = false;
                        }
                    }
                }
            }
        }
*/
    }
}
