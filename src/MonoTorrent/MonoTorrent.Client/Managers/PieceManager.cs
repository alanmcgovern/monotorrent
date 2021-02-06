//
// PieceManager.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Linq;
using System.Threading.Tasks;

using MonoTorrent.Client.Connections;
using MonoTorrent.Client.Messages;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Contains the logic for choosing what piece to download next
    /// </summary>
    public class PieceManager
    {
        // For every 10 kB/sec upload a peer has, we request one extra piece above the standard amount
        internal const int BonusRequestPerKb = 10;

        // Default to a minimum of 8 blocks
        internal const int NormalRequestAmount = 8;

        TorrentManager Manager { get; }
        internal IPieceRequester Requester { get; private set; }
        internal BitField PendingHashCheckPieces { get; private set; }

        /// <summary>
        /// Returns true when every block has been requested at least once.
        /// </summary>
        internal bool InEndgameMode { get; private set; }

        internal PieceManager (TorrentManager manager)
        {
            Manager = manager;
            PendingHashCheckPieces = new BitField (1);
            Requester = new StandardRequestManager ();
        }

        internal bool PieceDataReceived (PeerId id, PieceMessage message, out bool pieceComplete, out IList<IPeer> peersInvolved)
        {
            if (Requester.Picker.ValidatePiece (id, new PieceRequest (message.PieceIndex, message.StartOffset, message.RequestLength), out pieceComplete, out peersInvolved)) {
                id.LastBlockReceived.Restart ();
                if (pieceComplete)
                    PendingHashCheckPieces[message.PieceIndex] = true;
                return true;
            } else {
                return false;
            }
        }

        internal bool IsInteresting (PeerId id)
        {
            // If i have completed the torrent, then no-one is interesting
            if (Manager.Complete)
                return false;

            // If the peer is a seeder, then he is definately interesting
            if ((id.Peer.IsSeeder = id.BitField.AllTrue))
                return true;

            // Otherwise we need to do a full check
            return Requester.Picker.IsInteresting (id, id.BitField);
        }

        internal void AddPieceRequests (PeerId id)
        {
            Requester.AddRequests (id, Manager.Peers.ConnectedPeers, Manager.Bitfield);
        }

        internal void ChangePicker(IPieceRequester requester)
        {
            if (Manager.State != TorrentState.Stopped)
                throw new InvalidOperationException ($"The {nameof (IPieceRequester)} must be set while the TorrentManager is in the Stopped state.");
            Requester = requester;
            Initialise ();
        }

        internal void Initialise ()
        {
            if (Manager.HasMetadata) {
                PendingHashCheckPieces = new BitField (Manager.Bitfield.Length);

                var ignorableBitfieds = new[] {
                    Manager.Bitfield,
                    PendingHashCheckPieces,
                    Manager.UnhashedPieces,
                };
                Requester.Initialise (Manager, ignorableBitfieds);
            }
        }

        public async Task<int> CurrentRequestCountAsync()
        {
            await ClientEngine.MainLoop;

            if (null != Requester.Picker)
                return Requester.Picker.CurrentRequestCount();
            else
                return 0;
        }
    }
}
