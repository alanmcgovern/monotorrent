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
using MonoTorrent.Trackers;

namespace MonoTorrent.Messages.UdpTracker
{
    public class AnnounceMessage : UdpTrackerMessage
    {
        public override int ByteLength => 8 + 4 + 4 + 20 + 20 + 8 + 8 + 8 + 4 + 4 + 4 + 4 + 2;

        public long ConnectionId { get; set; }

        public long Downloaded { get; set; }

        public InfoHash? InfoHash { get; set; }

        public uint IP { get; set; }

        public uint Key { get; set; }

        public long Left { get; set; }

        public int NumWanted { get; set; }

        public ReadOnlyMemory<byte> PeerId { get; set; }

        public ushort Port { get; set; }

        public TorrentEvent TorrentEvent { get; set; }

        public long Uploaded { get; set; }

        public AnnounceMessage ()
            : this (0, 0, null, null!, 0)
        {

        }

        public AnnounceMessage (int transactionId, long connectionId, AnnounceRequest? parameters, InfoHash infoHash, int port)
            : base (1, transactionId)
        {
            ConnectionId = connectionId;
            if (parameters == null)
                return;

            Downloaded = parameters.BytesDownloaded;
            InfoHash = infoHash.Truncate ();
            IP = 0;
            Key = (uint) parameters.Key;
            Left = parameters.BytesLeft;
            NumWanted = 50;
            PeerId = parameters.PeerId;
            Port = (ushort) port;
            TorrentEvent = parameters.ClientEvent;
            Uploaded = parameters.BytesUploaded;
        }

        public override void Decode (ReadOnlySpan<byte> buffer)
        {
            ConnectionId = ReadLong (ref buffer);
            if (Action != ReadInt (ref buffer))
                ThrowInvalidActionException ();
            TransactionId = ReadInt (ref buffer);
            InfoHash = InfoHash.FromMemory (ReadBytes (ref buffer, 20));
            PeerId = ReadBytes (ref buffer, 20);
            Downloaded = ReadLong (ref buffer);
            Left = ReadLong (ref buffer);
            Uploaded = ReadLong (ref buffer);
            TorrentEvent = (TorrentEvent) ReadInt (ref buffer);
            IP = ReadUInt (ref buffer);
            Key = ReadUInt (ref buffer);
            NumWanted = ReadInt (ref buffer);
            Port = ReadUShort (ref buffer);
        }

        public override int Encode (Span<byte> buffer)
        {
            int written = buffer.Length;

            Write (ref buffer, ConnectionId);
            Write (ref buffer, Action);
            Write (ref buffer, TransactionId);
            Write (ref buffer, InfoHash!.Span);
            Write (ref buffer, PeerId.Span);
            Write (ref buffer, Downloaded);
            Write (ref buffer, Left);
            Write (ref buffer, Uploaded);
            Write (ref buffer, (int) TorrentEvent);
            Write (ref buffer, IP);
            Write (ref buffer, Key);
            Write (ref buffer, NumWanted);
            Write (ref buffer, Port);

            return written - buffer.Length;
        }
    }
}
