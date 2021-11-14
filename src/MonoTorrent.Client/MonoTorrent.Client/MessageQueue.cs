//
// MessageQueue.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
using MonoTorrent.Messages.Peer.FastPeer;

namespace MonoTorrent.Client
{
    class MessageQueue
    {
        bool Ready { get; set; }

        public bool ProcessingQueue { get; private set; } = true;
        public int QueueLength => SendQueue.Count;
        List<PeerMessage> SendQueue { get; } = new List<PeerMessage> ();

        internal bool BeginProcessing (bool force = false)
        {
            lock (SendQueue) {
                if (!Ready || ProcessingQueue || (SendQueue.Count == 0 && !force))
                    return false;

                ProcessingQueue = true;
                return true;
            }
        }

        internal PeerMessage TryDequeue ()
        {
            lock (SendQueue) {
                if (!Ready)
                    throw new InvalidOperationException ("Cannot dequeue messages before the queue has been marked 'ready'.");

                if (SendQueue.Count == 0) {
                    ProcessingQueue = false;
                    return null;
                }

                PeerMessage message = SendQueue[0];
                SendQueue.RemoveAt (0);
                return message;
            }
        }

        internal void Enqueue (PeerMessage message)
        {
            lock (SendQueue)
                EnqueueAt (message, SendQueue.Count);
        }

        internal void EnqueueAt (PeerMessage message, int index)
        {
            lock (SendQueue) {
                if (SendQueue.Count == 0 || index >= SendQueue.Count)
                    SendQueue.Add (message);
                else
                    SendQueue.Insert (index, message);
            }
        }

        internal int RejectRequests (bool supportsFastPeer, List<int> amAllowedFastPieces)
        {
            lock (SendQueue) {
                int rejectedCount = 0;
                for (int i = 0; i < SendQueue.Count; i++) {
                    if (!(SendQueue[i] is PieceMessage msg))
                        continue;

                    // If the peer doesn't support fast peer, then we will never requeue the message
                    if (!supportsFastPeer) {
                        SendQueue.RemoveAt (i);
                        i--;
                        rejectedCount++;
                        continue;
                    }

                    // If the peer supports fast peer, queue the message if it is an AllowedFast piece
                    // Otherwise send a reject message for the piece
                    if (amAllowedFastPieces.Contains (msg.PieceIndex))
                        continue;
                    else {
                        rejectedCount++;
                        SendQueue[i] = new RejectRequestMessage (msg);
                    }
                }
                return rejectedCount;
            }
        }

        internal bool TryCancelRequest (int pieceIndex, int startOffset, int requestLength)
        {
            lock (SendQueue) {
                for (int i = 0; i < SendQueue.Count; i++) {
                    if (!(SendQueue[i] is PieceMessage msg))
                        continue;
                    if (msg.PieceIndex == pieceIndex && msg.StartOffset == startOffset && msg.RequestLength == requestLength) {
                        SendQueue.RemoveAt (i);
                        return true;
                    }
                }
            }
            return false;
        }

        internal void SetReady ()
        {
            if (Ready)
                throw new InvalidOperationException ("Can only call ready once.");

            Ready = true;
            ProcessingQueue = false;
        }
    }
}
