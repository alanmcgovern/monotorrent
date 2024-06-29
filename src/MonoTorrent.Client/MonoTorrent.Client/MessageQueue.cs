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
using System.Diagnostics.CodeAnalysis;

using MonoTorrent.Messages.Peer;
using MonoTorrent.Messages.Peer.FastPeer;

namespace MonoTorrent.Client
{
    class MessageQueue : IDisposable
    {
        bool Disposed { get; set; }

        bool Ready { get; set; }

        public bool ProcessingQueue { get; private set; } = true;
        public int QueueLength => SendQueue.Count;
        List<(PeerMessage message, PeerMessage.Releaser releaser)> SendQueue { get; } = new List<(PeerMessage message, PeerMessage.Releaser releaser)> ();

        internal bool BeginProcessing (bool force = false)
        {
            lock (SendQueue) {
                if (Disposed|| !Ready || ProcessingQueue || (SendQueue.Count == 0 && !force))
                    return false;

                ProcessingQueue = true;
                return true;
            }
        }

        internal bool TryDequeue ([NotNullWhen (true)] out PeerMessage? message, out PeerMessage.Releaser releaser)
        {
            if (Disposed)
                throw new ObjectDisposedException (nameof (MessageQueue));

            lock (SendQueue) {
                if (!Ready)
                    throw new InvalidOperationException ("Cannot dequeue messages before the queue has been marked 'ready'.");

                if (SendQueue.Count == 0) {
                    ProcessingQueue = false;
                    message = default;
                    releaser = default;
                    return false;
                }

                (message, releaser) = SendQueue[0];
                SendQueue.RemoveAt (0);
                return true;
            }
        }

        internal void Enqueue (PeerMessage message)
            => Enqueue (message, default);

        internal void Enqueue (PeerMessage message, PeerMessage.Releaser releaser)
        {
            lock (SendQueue)
                EnqueueAt (SendQueue.Count, message, releaser);
        }

        internal void EnqueueAt (int index, PeerMessage message, PeerMessage.Releaser releaser)
        {
            if (Disposed)
                throw new ObjectDisposedException (nameof (MessageQueue));

            lock (SendQueue) {
                if (SendQueue.Count == 0 || index >= SendQueue.Count) {
                    if (SendQueue.Count > 0 && (SendQueue[SendQueue.Count - 1].message is RequestBundle previous) && message is RequestBundle newBundle) {
                        if (previous.TryAppend (newBundle)) {
                            releaser.Dispose ();
                            return;
                        }
                    }
                    SendQueue.Add ((message, releaser));
                } else {
                    SendQueue.Insert (index, (message, releaser));
                }
            }
        }

        internal int RejectRequests (bool supportsFastPeer, ReadOnlySpan<int> amAllowedFastPieces)
        {
            if (Disposed)
                throw new ObjectDisposedException (nameof (MessageQueue));

            lock (SendQueue) {
                int rejectedCount = 0;
                for (int i = 0; i < SendQueue.Count; i++) {
                    if (!(SendQueue[i].message is PieceMessage msg))
                        continue;

                    // If the peer doesn't support fast peer, then we will never requeue the message
                    if (!supportsFastPeer) {
                        SendQueue[i].releaser.Dispose ();
                        SendQueue.RemoveAt (i);
                        i--;
                        rejectedCount++;
                        continue;
                    }

                    // If the peer supports fast peer, queue the message if it is an AllowedFast piece
                    // Otherwise send a reject message for the piece
                    if (amAllowedFastPieces.IndexOf (msg.PieceIndex) != -1)
                        continue;
                    else {
                        rejectedCount++;
                        SendQueue[i].releaser.Dispose ();

                        (var rejectMessage, var releaser) = PeerMessage.Rent<RejectRequestMessage> ();
                        rejectMessage.Initialize (msg);
                        SendQueue[i] = (rejectMessage, releaser);
                    }
                }
                return rejectedCount;
            }
        }

        internal bool TryCancelRequest (int pieceIndex, int startOffset, int requestLength)
        {
            if (Disposed)
                throw new ObjectDisposedException (nameof (MessageQueue));

            lock (SendQueue) {
                for (int i = 0; i < SendQueue.Count; i++) {
                    if (!(SendQueue[i].message is PieceMessage msg))
                        continue;

                    if (msg.PieceIndex == pieceIndex && msg.StartOffset == startOffset && msg.RequestLength == requestLength) {
                        SendQueue[i].releaser.Dispose ();
                        SendQueue.RemoveAt (i);
                        return true;
                    }
                }
            }
            return false;
        }

        internal void SetReady ()
        {
            if (Disposed)
                throw new ObjectDisposedException (nameof (MessageQueue));

            if (Ready)
                throw new InvalidOperationException ("Can only call ready once.");

            Ready = true;
            ProcessingQueue = false;
        }

        public void Dispose ()
        {
            if (Disposed)
                return;

            lock (SendQueue) {
                Disposed = true;
                for (int i = 0; i < SendQueue.Count; i++)
                    SendQueue[i].releaser.Dispose ();
                SendQueue.Clear ();
            }
        }
    }
}
