//
// AnnounceMessage.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
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

using MonoTorrent.BEncoding;
using MonoTorrent.Client.Tracker;

namespace MonoTorrent.Client.Messages.UdpTracker
{
    class AnnounceMessage : UdpTrackerMessage
    {
        public override int ByteLength => 8 + 4 + 4 + 20 + 20 + 8 + 8 + 8 + 4 + 4 + 4 + 4 + 2;

        public long ConnectionId { get; set; }

        public long Downloaded { get; set; }

        public InfoHash InfoHash { get; set; }

        public uint IP { get; set; }

        public uint Key { get; set; }

        public long Left { get; set; }

        public int NumWanted { get; set; }

        public BEncodedString PeerId { get; set; }

        public ushort Port { get; set; }

        public TorrentEvent TorrentEvent { get; set; }

        public long Uploaded { get; set; }

        public AnnounceMessage ()
            : this (0, 0, null)
        {

        }

        public AnnounceMessage (int transactionId, long connectionId, AnnounceParameters parameters)
            : base (1, transactionId)
        {
            ConnectionId = connectionId;
            if (parameters == null)
                return;

            Downloaded = parameters.BytesDownloaded;
            InfoHash = parameters.InfoHash;
            IP = 0;
            Key = (uint) DateTime.Now.GetHashCode (); // FIXME: Don't do this! It should be constant
            Left = parameters.BytesLeft;
            NumWanted = 50;
            PeerId = parameters.PeerId;
            Port = (ushort) parameters.Port;
            TorrentEvent = parameters.ClientEvent;
            Uploaded = parameters.BytesUploaded;
        }

        public override void Decode (byte[] buffer, int offset, int length)
        {
            ConnectionId = ReadLong (buffer, ref offset);
            if (Action != ReadInt (buffer, ref offset))
                ThrowInvalidActionException ();
            TransactionId = ReadInt (buffer, ref offset);
            InfoHash = new InfoHash (ReadBytes (buffer, ref offset, 20));
            PeerId = new BEncodedString (ReadBytes (buffer, ref offset, 20));
            Downloaded = ReadLong (buffer, ref offset);
            Left = ReadLong (buffer, ref offset);
            Uploaded = ReadLong (buffer, ref offset);
            TorrentEvent = (TorrentEvent) ReadInt (buffer, ref offset);
            IP = (uint) ReadInt (buffer, ref offset);
            Key = (uint) ReadInt (buffer, ref offset);
            NumWanted = ReadInt (buffer, ref offset);
            Port = (ushort) ReadShort (buffer, ref offset);
        }

        public override int Encode (byte[] buffer, int offset)
        {
            int written = offset;

            written += Write (buffer, written, ConnectionId);
            written += Write (buffer, written, Action);
            written += Write (buffer, written, TransactionId);
            written += Write (buffer, written, InfoHash.Hash, 0, InfoHash.Hash.Length);
            written += Write (buffer, written, PeerId.TextBytes);
            written += Write (buffer, written, Downloaded);
            written += Write (buffer, written, Left);
            written += Write (buffer, written, Uploaded);
            written += Write (buffer, written, (int) TorrentEvent);
            written += Write (buffer, written, IP);
            written += Write (buffer, written, Key);
            written += Write (buffer, written, NumWanted);
            written += Write (buffer, written, Port);

            return written - offset;
        }
    }
}
