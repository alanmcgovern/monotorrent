//
// InitialSeedUnchoker.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2009 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;

using MonoTorrent.Messages.Peer;

namespace MonoTorrent.Client
{
    class ChokeData
    {
        public DateTime LastChoked;
        public PeerId Peer;
        public BitField CurrentPieces;
        public int SharedPieces;
        public DateTime LastUnchoked;
        public int TotalPieces;

        public double ShareRatio => (SharedPieces + 1.0) / (TotalPieces + 1.0);

        public ChokeData (PeerId peer)
        {
            LastChoked = DateTime.Now;
            Peer = peer;
            CurrentPieces = new BitField (peer.BitField.Length);
        }
    }

    class SeededPiece
    {
        public int Index;
        public PeerId Peer;
        public int BlocksSent;
        public DateTime SeededAt;
        public int TotalBlocks;

        public SeededPiece (PeerId peer, int index, int totalBlocks)
        {
            Index = index;
            Peer = peer;
            SeededAt = DateTime.Now;
            TotalBlocks = totalBlocks;
        }
    }

    class InitialSeedUnchoker : Unchoker
    {
        readonly List<SeededPiece> advertisedPieces;
        readonly BitField bitfield;
        readonly List<ChokeData> peers;
        readonly BitField temp;

        bool PendingUnchoke => peers.Exists (d => d.Peer.AmChoking && d.Peer.IsInterested);

        public bool Complete => bitfield.AllTrue;

        public int MaxAdvertised => 4;

        internal int PeerCount => peers.Count;

        public InitialSeedUnchoker (TorrentManager manager)
            : base (manager)
        {
            advertisedPieces = new List<SeededPiece> ();
            bitfield = new BitField (manager.Bitfield.Length);
            peers = new List<ChokeData> ();
            temp = new BitField (bitfield.Length);
        }

        public override void Choke (PeerId id)
        {
            base.Choke (id);

            advertisedPieces.RemoveAll (p => p.Peer == id);

            // Place the peer at the end of the list so the rest of the peers
            // will get an opportunity to unchoke before this peer gets tried again
            ChokeData data = peers.Find (d => d.Peer == id)!;
            peers.Remove (data);
            peers.Add (data);
        }

        public void PeerConnected (PeerId id)
        {
            bitfield.Or (id.BitField);
            peers.Add (new ChokeData (id));
        }

        public void PeerDisconnected (PeerId id)
        {
            peers.RemoveAll (d => d.Peer == id);
            advertisedPieces.RemoveAll (piece => piece.Peer == id);
        }

        public void ReceivedHave (PeerId peer, int pieceIndex)
        {
            bitfield[pieceIndex] = true;

            // If a peer reports they have a piece that *isn't* the peer
            // we uploaded it to, then the peer we uploaded to has shared it
            foreach (ChokeData data in peers) {
                if (data.CurrentPieces[pieceIndex] && data.Peer != peer) {
                    data.CurrentPieces[pieceIndex] = false;
                    data.SharedPieces++;
                    // Give him another piece if no-one else is waiting.
                    TryAdvertisePiece (data);
                    break;
                }
            }

            foreach (SeededPiece piece in advertisedPieces) {
                if (piece.Index == pieceIndex) {
                    advertisedPieces.Remove (piece);
                    return;
                }
            }
        }

        public void ReceivedNotInterested (PeerId id)
        {
            advertisedPieces.RemoveAll (piece => piece.Peer == id);
        }

        public void SentBlock (PeerId peer, int pieceIndex)
        {
            SeededPiece? piece = advertisedPieces.Find (p => p.Peer == peer && p.Index == pieceIndex);
            if (piece == null)
                return;

            piece.SeededAt = DateTime.Now;
            piece.BlocksSent++;
            if (piece.TotalBlocks == piece.BlocksSent)
                advertisedPieces.Remove (piece);
        }

        void TryAdvertisePiece (ChokeData data)
        {
            // If we are seeding to this peer and we have a peer waiting to unchoke
            // don't advertise more data
            if (!data.Peer.AmChoking && PendingUnchoke)
                return;

            int advertised = advertisedPieces.FindAll (p => p.Peer == data.Peer).Count;
            int max = MaxAdvertised;
            if (Manager.UploadingTo < Manager.Settings.UploadSlots)
                max = MaxAdvertised;
            else if (data.ShareRatio < 0.25)
                max = 1;
            else if (data.ShareRatio < 0.35)
                max = 2;
            else if (data.ShareRatio < 0.50)
                max = 3;
            else
                max = MaxAdvertised;

            if (advertised >= max)
                return;

            // List of pieces *not* in the swarm
            temp.From (bitfield).Not ();

            // List of pieces that he wants that aren't in the swarm
            temp.NAnd (data.Peer.BitField);

            // Ignore all the pieces we've already started sharing
            foreach (SeededPiece p in advertisedPieces)
                temp[p.Index] = false;

            int index = 0;
            while (advertised < max) {
                // Get the index of the first piece we can send him
                index = temp.FirstTrue (index, temp.Length - 1);
                // Looks like he's not interested in us...
                if (index == -1)
                    return;

                advertised++;
                data.TotalPieces++;
                data.CurrentPieces[index] = true;
                advertisedPieces.Add (new SeededPiece (data.Peer, index, Manager.Torrent!.PieceLength / Constants.BlockSize));
                (var message, var releaser) = PeerMessage.Rent<HaveMessage> ();
                message.Initialize (index);
                data.Peer.MessageQueue.Enqueue (message, releaser);
                index++;
            }
        }

        void TryChoke (ChokeData data)
        {
            // Already choked
            if (data.Peer.AmChoking)
                return;

            if (!data.Peer.IsInterested) {
                // Choke him if he's not interested
                Choke (data.Peer);
            } else if (!advertisedPieces.Exists (p => p.Peer == data.Peer)) {
                // If we have no free slots and peers are waiting, choke after 30 seconds.
                // FIXME: Choke as soon as the next piece completes *or* a larger time limit *and*
                // at least one piece has uploaded.
                data.LastChoked = DateTime.Now;
                Choke (data.Peer);
            }
        }

        void TryUnchoke (ChokeData data)
        {
            // Already unchoked
            if (!data.Peer.AmChoking)
                return;

            // Don't unchoke if he's not interested
            if (!data.Peer.IsInterested)
                return;

            // Don't unchoke if we are have maxed our slots
            if (Manager.UploadingTo >= Manager.Settings.UploadSlots)
                return;

            data.LastUnchoked = DateTime.Now;
            Unchoke (data.Peer);
        }

        public override void UnchokeReview ()
        {
            if (PendingUnchoke) {
                var dupePeers = new List<ChokeData> (peers);
                foreach (ChokeData data in dupePeers)
                    TryChoke (data);

                dupePeers = new List<ChokeData> (peers);
                // See if there's anyone interesting to unchoke
                foreach (ChokeData data in dupePeers)
                    TryUnchoke (data);
            }

            // Make sure our list of pieces available in the swarm is up to date
            foreach (ChokeData data in peers)
                bitfield.Or (data.Peer.BitField);

            advertisedPieces.RemoveAll (p => bitfield[p.Index]);

            // Send have messages to anyone that needs them
            foreach (ChokeData data in peers)
                TryAdvertisePiece (data);
        }
    }
}
