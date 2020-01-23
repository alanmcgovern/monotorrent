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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client;
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

        public override long Length => File.Length;

        public override long Position {
            get => position;
            set => Seek (value, SeekOrigin.Begin);
        }

        TorrentFile File { get; }

        TorrentManager Manager { get; }

        StreamingPiecePicker Picker { get; }

        FileStream Stream { get; set; }

        public LocalStream (TorrentManager manager, TorrentFile file, StreamingPiecePicker picker)
        {
            Manager = manager;
            File = file;
            Picker = picker;
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            if (disposing) {
                Stream?.Dispose ();
            }
        }

        public override void Flush ()
            => throw new NotSupportedException ();

        public override int Read (byte[] buffer, int offset, int count)
            => ReadAsync (buffer, offset, count, CancellationToken.None).GetAwaiter ().GetResult ();

        public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // The torrent is treated as one big block of data, so this is the offset at which the current file's data starts at.
            var torrentFileStartOffset = (long)File.StartPieceIndex * (long)Manager.Torrent.PieceLength + File.StartPieceOffset;

            // Take our current position into account when calculating the start/end pieces of the data we're reading.
            var startPiece = (int) (torrentFileStartOffset + Position) / Manager.Torrent.PieceLength;
            var endPiece = (int) (torrentFileStartOffset + Position + count) / Manager.Torrent.PieceLength;
            while (Manager.State != TorrentState.Stopped && Manager.State != TorrentState.Error) {
                bool allAvailable = true;
                for (int i = startPiece; i <= endPiece && allAvailable; i++)
                    allAvailable &= Manager.Bitfield[i];

                if (allAvailable)
                    break;

                await Task.Delay (500, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested ();
            if (Stream == null) {
                Stream = new FileStream (File.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Stream.Seek (Position, SeekOrigin.Begin);
            }

            var read = await Stream.ReadAsync (buffer, offset, count, cancellationToken);
            position += read;
            var oldHighPriority = Picker.HighPriorityPieceIndex;
            Picker.ReadToPosition (File, position);
            Debug.WriteLine ($"Read to {position}. HighPriorityPiece: {oldHighPriority} -> {Picker.HighPriorityPieceIndex}");
            return read;
        }

        public override long Seek (long offset, SeekOrigin origin)
        {
            switch (origin) {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.Current:
                    position += offset;
                    break;
                case SeekOrigin.End:
                    position = Length - offset;
                    break;
                default:
                    throw new NotSupportedException ();
            }

            var oldHighPriority = Picker.HighPriorityPieceIndex;
            Picker.SeekToPosition (File, position);
            Debug.WriteLine ($"Seek to {position}. HighPriorityPiece: {oldHighPriority} -> {Picker.HighPriorityPieceIndex}");
            Stream?.Seek (offset, origin);
            return position;
        }

        public override void SetLength (long value)
            => throw new NotSupportedException ();

        public override void Write (byte[] buffer, int offset, int count)
            => throw new NotSupportedException ();
    }
}
