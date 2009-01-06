//
// LoggingPicker.cs
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
using System.Text;
using MonoTorrent.Client.Messages;
using MonoTorrent.Common;
using MonoTorrent.Client.Messages.Standard;

namespace MonoTorrent.Client
{
    class LoggingPicker : PiecePicker
    {
        class Request : IComparable<Request>
        {
            public int PieceIndex;
            public int StartOffset;
            public int RequestLength;
            public PeerId RequestedOff;
            public bool Verified;

            public int CompareTo(Request other)
            {
                int difference;
                if ((difference = PieceIndex.CompareTo(other.PieceIndex)) != 0)
                    return difference;
                if ((difference = StartOffset.CompareTo(other.StartOffset)) != 0)
                    return difference;
                return RequestLength.CompareTo(other.RequestLength);
            }
        }

        SortList<Request> requests = new SortList<Request>();

        public LoggingPicker(PiecePicker picker)
            : base(picker)
        {

        }

        public override MessageBundle PickPiece(PeerId id, BitField peerBitfield, List<PeerId> otherPeers, int count, int startIndex, int endIndex)
        {
            MessageBundle bundle = base.PickPiece(id, peerBitfield, otherPeers, count, startIndex, endIndex);
            if (bundle != null)
            {
                foreach (RequestMessage m in bundle.Messages)
                {
                    Request r = new Request();
                    r.PieceIndex = m.PieceIndex;
                    r.RequestedOff = id;
                    r.RequestLength = m.RequestLength;
                    r.StartOffset = m.StartOffset;
                    List<Request> current = requests.FindAll(delegate(Request req) { return req.CompareTo(r) == 0; });
                    if (current.Count > 0)
                    {
                        foreach (Request request in current)
                        {
                            if (request.Verified)
                            {
                                if (id.TorrentManager.Bitfield[request.PieceIndex])
                                {
                                    Console.WriteLine("Double request: {0}", m);
                                    Console.WriteLine("From: {0} and {1}", id.PeerID, r.RequestedOff.PeerID);
                                }
                                else
                                {
                                    // The piece failed a hashcheck, so ignore it this time
                                    requests.Remove(request);
                                }
                            }
                        }
                    }
                    requests.Add(r);
                }
            }

            return bundle;
        }

        public override bool ValidatePiece(PeerId peer, int pieceIndex, int startOffset, int length, out Piece piece)
        {
            bool validatedOk = base.ValidatePiece(peer, pieceIndex, startOffset, length, out piece);

            List<Request> list = requests.FindAll(delegate(Request r)
            {
                return r.PieceIndex == pieceIndex &&
                       r.RequestLength == length &&
                       r.StartOffset == startOffset;
            });

            if (list.Count == 0)
            {
                Console.WriteLine("Piece was not requested from anyone: {1}-{2}", peer.PeerID, pieceIndex, startOffset);
            }
            else if (list.Count == 1)
            {
                if (list[0].Verified)
                {
                    if (validatedOk)
                        Console.WriteLine("The piece should not have validated");
                    Console.WriteLine("Piece already verified: Orig: {0} Current: {3} <> {1}-{2}",
                                       list[0].RequestedOff.PeerID, pieceIndex, startOffset, peer.PeerID);
                }
            }
            else
            {
                List<Request> alreadyVerified = list.FindAll(delegate(Request r) { return r.Verified; });
                if (alreadyVerified.Count > 0)
                {
                    if (validatedOk)
                        Console.WriteLine("The piece should not have validated 2");
                    Console.WriteLine("Piece has already been verified {0} times", alreadyVerified.Count);
                }
            }

            foreach (Request request in list)
            {
                if (request.RequestedOff == peer)
                    if (!request.Verified)
                    {
                        if (!validatedOk)
                            Console.WriteLine("The piece should have validated");
                        request.Verified = true;
                    }
                    else
                    {
                        if (validatedOk)
                            Console.WriteLine("The piece should not have validated 3");
                        Console.WriteLine("This peer has already sent and verified this piece. {0} <> {1}-{2}", peer.PeerID, pieceIndex, startOffset);
                    }
            }

            return validatedOk;
        }
    }
}
