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
using MonoTorrent.Client.PeerMessages;
using MonoTorrent.Common;
using MonoTorrent.Client;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Contains the logic for choosing what piece to download next
    /// </summary>
    public class PieceManager
    {
        #region Public Constants

        public const int MaxRequests = 12;
        public const int MaxEndGameRequests = 2;

        #endregion


        #region Events
        /// <summary>
        /// Event that's fired every time a Piece changes
        /// </summary>
        internal event EventHandler<PieceEventArgs> OnPieceChanged;
        #endregion


        #region Member Variables
        private PiecePickerBase piecePicker;
        #endregion


        #region Constructors
        public PieceManager(BitField bitfield, TorrentFile[] files)
        {
            this.piecePicker = new StandardPicker(bitfield, files);
        }
        #endregion


        #region Methods
        internal bool IsInteresting(PeerConnectionID id)
        {
            // If i have completed the torrent, then no-one is interesting
			if (id.TorrentManager.Bitfield.AllTrue)
				return false;

			// If the peer is a seeder, then he is definately interesting
			if (id.Peer.IsSeeder)
				return true;

            // Otherwise we need to do a full check
            return this.piecePicker.IsInteresting(id);
        }


        public bool InEndGameMode
        {
            get { return this.piecePicker is EndGamePicker; }
        }


        internal BitField MyBitField
        {
            get { return this.piecePicker.MyBitField; }
        }


        internal int CurrentRequestCount()
        {
            return this.piecePicker.CurrentRequestCount();
        }


        internal IPeerMessageInternal PickPiece(PeerConnectionID id, List<PeerConnectionID> otherPeers)
        {
            if ((this.MyBitField.Length - this.MyBitField.TrueCount < 15) && this.piecePicker is StandardPicker)
                this.piecePicker = new EndGamePicker(this.MyBitField, id.TorrentManager.Torrent, ((StandardPicker)this.piecePicker).Requests);

            return this.piecePicker.PickPiece(id, otherPeers);
        }


        internal void ReceivedRejectRequest(PeerConnectionID id, RejectRequestMessage msg)
        {
            this.piecePicker.ReceivedRejectRequest(id, msg);
        }


        internal void RemoveRequests(PeerConnectionID id)
        {
            this.piecePicker.RemoveRequests(id);
        }


        internal PieceEvent ReceivedPieceMessage(PeerConnectionID id, byte[] buffer, PieceMessage message)
        {
            return this.piecePicker.ReceivedPieceMessage(id, buffer, message);
        }
        #endregion

        internal void ReceivedChokeMessage(PeerConnectionID id)
        {
            // If fast peers isnt supported, we remove all pending request messages
            if (!(id.Peer.Connection.SupportsFastPeer && ClientEngine.SupportsFastPeer))
            {
                // Remove any pending request messages from the send queue as there's no point in sending them
                IPeerMessageInternal message;
                int length = id.Peer.Connection.QueueLength;
                for (int i = 0; i < length; i++)
                    if ((message = id.Peer.Connection.DeQueue()) is RequestMessage)
                        continue;
                    else
                        id.Peer.Connection.EnQueue(message);
            }

            this.piecePicker.ReceivedChokeMessage(id);
        }
    }
}