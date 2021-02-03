//
// LocalStream.cs
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client;
using MonoTorrent.Client.Messages.Standard;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Streaming
{
    /// <summary>
    /// A seekable Stream which can be used to access a <see cref="TorrentFile"/> while it is downloading.
    /// If the stream seeks to a location which hasn't been downloaded yet, <see cref="Read(byte[], int, int)"/>
    /// will block until the data is available. <see cref="ReadAsync(byte[], int, int, CancellationToken)"/>
    /// will perform a non-blocking wait for the data.
    /// </summary>
    class LocalStream : Stream
    {
        long position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        internal bool Disposed { get; private set; }

        public override long Length => File.Length;

        public override long Position {
            get => position;
            set => Seek (value, SeekOrigin.Begin);
        }

        ITorrentFileInfo File { get; }

        TorrentManager Manager { get; }

        StreamingPiecePicker Picker { get; }

        public LocalStream (TorrentManager manager, ITorrentFileInfo file, StreamingPiecePicker picker)
        {
            Manager = manager;
            File = file;
            Picker = picker;
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);

            Disposed = true;
        }

        public override void Flush ()
            => throw new NotSupportedException ();

        public override int Read (byte[] buffer, int offset, int count)
            => ReadAsync (buffer, offset, count, CancellationToken.None).GetAwaiter ().GetResult ();

        public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfDisposed ();

            // The torrent is treated as one big block of data, so this is the offset at which the current file's data starts at.
            var torrentFileStartOffset = (long) File.StartPieceIndex * (long) Manager.Torrent.PieceLength + File.StartPieceOffset;

            // Clamp things so we cannot overread.
            if (Position + count > Length)
                count = (int) (Length - Position);

            // We've reached the end of the file, so return 0 to indicate EOF.
            if (count == 0)
                return 0;

            // Take our current position into account when calculating the start/end pieces of the data we're reading.
            var startPiece = (int) ((torrentFileStartOffset + Position) / Manager.Torrent.PieceLength);
            var endPiece = (int) ((torrentFileStartOffset + Position + count) / Manager.Torrent.PieceLength);
            if (Length % Manager.Torrent.PieceLength == 0 && endPiece == Manager.Torrent.Pieces.Count)
                endPiece--;

            while (Manager.State != TorrentState.Stopped && Manager.State != TorrentState.Error) {
                bool allAvailable = true;
                for (int i = startPiece; i <= endPiece && allAvailable; i++)
                    allAvailable &= Manager.Bitfield[i];

                if (allAvailable)
                    break;

                await Task.Delay (100, cancellationToken).ConfigureAwait (false);
                ThrowIfDisposed ();
            }

            cancellationToken.ThrowIfCancellationRequested ();

            // Always flush the data we wish to read to disk before we attempt to read it. If we attempt to read some data which crosses
            // the boundary between two, or more, blocks it's significantly easier to correctly read the data if it has been flushed.
            // Otherwise the internal memory cache would need a lot of complexity so it can fulfill a 64kB read when some parts are in
            // memory and other parts are not.
            //
            // We can add support for reads where data is partially in memory and partially on disk at a later stage.
            for (int i = startPiece; i <= endPiece; i++)
                await Manager.Engine.DiskManager.FlushAsync (Manager, i);

            // Now we can safely read an arbitrary amount of data using DiskManager.
            if (!await Manager.Engine.DiskManager.ReadAsync (Manager, Position + torrentFileStartOffset, buffer, count).ConfigureAwait (false))
                throw new InvalidOperationException ("Could not read the requested data from the torrent");
            ThrowIfDisposed ();

            position += count;
            Picker.ReadToPosition (File, position);
            return count;
        }

        public override long Seek (long offset, SeekOrigin origin)
        {
            ThrowIfDisposed ();
            long newPosition;
            switch (origin) {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = Length - offset;
                    break;
                default:
                    throw new NotSupportedException ();
            }

            // Clamp it to within reasonable bounds.
            newPosition = Math.Max (0, newPosition);
            newPosition = Math.Min (newPosition, Length);

            if (newPosition != position) {
                position = newPosition;
                MaybeAdjustCurrentPieceRequests ();
            }
            return position;
        }

        async void MaybeAdjustCurrentPieceRequests ()
        {
            await ClientEngine.MainLoop;
            if (!Picker.SeekToPosition (File, position))
                return;

            var allPeers = Manager.Peers.ConnectedPeers
            .OrderBy (t => -t.Monitor.DownloadSpeed)
            .ToArray ();

            // It's only worth cancelling requests for peers which support cancellation. This is part of
            // of the fast peer extensions. For peers which do not support cancellation all we can do is
            // close the connection or allow the existing request queue to drain naturally.
            //
            // FIXME: how could/should this be implemented in a custom IPiecePicker??
            var start = Picker.HighPriorityPieceIndex;
            var end = Math.Min (Manager.Bitfield.Length - 1, start + Picker.HighPriorityCount - 1);
            if (Manager.Bitfield.FirstFalse (start, end) != -1) {
                foreach (var peer in allPeers.Where (p => p.SupportsFastPeer)) {
                    if (Picker.HighPriorityPieceIndex > 0) {
                        foreach (var cancelled in Manager.PieceManager.Requester.Picker.CancelRequests (peer, 0, Picker.HighPriorityPieceIndex - 1))
                            peer.MessageQueue.Enqueue (new CancelMessage (cancelled.PieceIndex, cancelled.StartOffset, cancelled.RequestLength));
                    }

                    if (Picker.HighPriorityPieceIndex + Picker.HighPriorityCount < Manager.Bitfield.Length) {
                        foreach (var cancelled in Manager.PieceManager.Requester.Picker.CancelRequests (peer, Picker.HighPriorityPieceIndex + Picker.HighPriorityCount, Manager.Bitfield.Length - 1))
                            peer.MessageQueue.Enqueue (new CancelMessage (cancelled.PieceIndex, cancelled.StartOffset, cancelled.RequestLength));
                    }
                }
            }

            var fastestPeers = allPeers
                .Where (t => t.Monitor.DownloadSpeed > 50 * 1024)
                .ToArray ();

            // Queue up 12 pieces for each of our fastest peers. At a download
            // speed of 50kB/sec this should be 3 seconds of transfer for each peer.
            // We queue from peers which support cancellation first as their queue
            // is likely to be empty.
            foreach (var supportsFastPeer in new[] { true, false }) {
                for (int i = 0; i < 4; i++) {
                    foreach (var peer in fastestPeers.Where (p => p.SupportsFastPeer == supportsFastPeer)) {
                        // FIXME: make an API for this?
                        var original = peer.MaxPendingRequests;
                        peer.MaxPendingRequests = (i + 1) * 3;
                        Manager.PieceManager.AddPieceRequests (peer);
                        peer.MaxPendingRequests = original;
                    }
                }
            }

            // Then fill up the request queues for all peers
            foreach (var peer in fastestPeers)
                Manager.PieceManager.AddPieceRequests (peer);
        }

        public override void SetLength (long value)
            => throw new NotSupportedException ();

        public override void Write (byte[] buffer, int offset, int count)
            => throw new NotSupportedException ();

        void ThrowIfDisposed ()
        {
            if (Disposed)
                throw new ObjectDisposedException (nameof (LocalStream));
        }
    }
}
