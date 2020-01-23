using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MonoTorrent.Client;
using MonoTorrent.Client.PiecePicking;

namespace MonoTorrent.Streaming
{
    public class DirectStream : Stream
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

        SlidingWindowPicker Picker { get; }

        FileStream Stream { get; set; }

        public DirectStream (TorrentManager manager, TorrentFile file)
        {
            Manager = manager;
            File = file;
            Picker = new SlidingWindowPicker (new StandardPicker ());
            Picker.HighPrioritySetStart = file.StartPieceIndex;
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose (disposing);
            if (disposing) {
                Stream?.Dispose ();
            }
        }

        public async Task Initialise ()
        {
            await Manager.ChangePickerAsync (Picker);
            await Manager.StartAsync ();
        }

        public override void Flush ()
            => throw new NotSupportedException ();

        public override int Read (byte[] buffer, int offset, int count)
            => ReadAsync (buffer, offset, count, CancellationToken.None).GetAwaiter ().GetResult ();

        public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            long torrentOffset = 0;
            foreach (var file in Manager.Torrent.Files) {
                if (file == File)
                    break;

                torrentOffset += file.Length;
            }

            var startPiece = (int) (torrentOffset + offset) / Manager.Torrent.PieceLength;
            var endPiece = (int) (torrentOffset + offset + count) / Manager.Torrent.PieceLength;
            while (Manager.State != TorrentState.Stopped && Manager.State != TorrentState.Error) {
                bool allAvailable = true;
                for (int i = startPiece; i <= endPiece && allAvailable; i++)
                    allAvailable &= Manager.Bitfield[i];

                if (allAvailable)
                    break;

                await Task.Delay (500, cancellationToken);
            }

            if (Stream == null)
                Stream = new FileStream (File.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return await Stream.ReadAsync (buffer, offset, count, cancellationToken);
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

            Picker.HighPrioritySetStart = (int) (position / Manager.Torrent.PieceLength);
            Stream.Seek (offset, origin);
            return position;
        }

        public override void SetLength (long value)
            => throw new NotSupportedException ();

        public override void Write (byte[] buffer, int offset, int count)
            => throw new NotSupportedException ();
    }
}
