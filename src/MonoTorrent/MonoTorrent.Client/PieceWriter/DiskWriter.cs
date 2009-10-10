using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Common;
using System.IO;
using System.Threading;

namespace MonoTorrent.Client.PieceWriters
{
    public class DiskWriter : PieceWriter
    {
        private FileStreamBuffer streamsBuffer;

        public int OpenFiles
        {
            get { return streamsBuffer.Count; }
        }

        public DiskWriter()
            : this(10)
        {

        }

        public DiskWriter(int maxOpenFiles)
        {
            this.streamsBuffer = new FileStreamBuffer(maxOpenFiles);
        }

        public override void Close(TorrentFile file)
        {
            streamsBuffer.CloseStream(file.FullPath);
        }

        public override void Dispose()
        {
            streamsBuffer.Dispose();
            base.Dispose();
        }

        internal TorrentFileStream GetStream(TorrentFile file, FileAccess access)
        {
            return streamsBuffer.GetStream(file, access);
        }

        public override void Move(string oldPath, string newPath, bool ignoreExisting)
        {
            streamsBuffer.CloseStream(oldPath);
            if (ignoreExisting)
                File.Delete(newPath);
            File.Move(oldPath, newPath);
        }

        public override int Read(BufferedIO data)
        {
            if (data == null)
                throw new ArgumentNullException("buffer");
            
            long offset = data.Offset;
            int count = data.Count;
            IList<TorrentFile> files = data.Files;
            long fileSize = Toolbox.Accumulate<TorrentFile>(files, delegate(TorrentFile f) { return f.Length; });
            if (offset < 0 || offset + count > fileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int bytesRead = 0;
            int totalRead = 0;

            for (i = 0; i < files.Count; i++)          // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < files[i].Length)           // the start of the data we want to read
                    break;

                offset -= files[i].Length;              // Offset now contains the index of the data we want
            }                                                   // to read from fileStream[i].

            while (totalRead < count)                           // We keep reading until we have read 'count' bytes.
            {
                if (i == files.Count)
                    break;

                TorrentFileStream s = GetStream(files[i], FileAccess.Read);
                s.Seek(offset, SeekOrigin.Begin);
                offset = 0; // Any further files need to be read from the beginning
                bytesRead = s.Read(data.buffer.Array, data.buffer.Offset + totalRead, count - totalRead);
                totalRead += bytesRead;
                i++;
            }
            //monitor.BytesSent(totalRead, TransferType.Data);
            data.ActualCount += totalRead;
            return totalRead;
        }

        public override void Write(BufferedIO data)
        {
            byte[] buffer = data.buffer.Array;
            long offset = data.Offset;
            int count = data.Count;

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            long fileSize = 0;
            for (int j = 0; j < data.Files.Count; j++)
                fileSize += data.Files[j].Length;

            if (offset < 0 || offset + count > fileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            long bytesWritten = 0;
            long totalWritten = 0;
            long bytesWeCanWrite = 0;

            for (i = 0; i < data.Files.Count; i++)          // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < data.Files[i].Length)           // the start of the data we want to write
                    break;

                offset -= data.Files[i].Length;              // Offset now contains the index of the data we want
            }                                                   // to write to fileStream[i].

            while (totalWritten < count)                        // We keep writing  until we have written 'count' bytes.
            {
                TorrentFileStream stream = GetStream(data.Files[i], FileAccess.ReadWrite);
                stream.Seek(offset, SeekOrigin.Begin);

                // Find the maximum number of bytes we can write before we reach the end of the file
                bytesWeCanWrite = data.Files[i].Length - offset;

                // Any further files need to be written from the beginning of the file
                offset = 0;

                // If the amount of data we are going to write is larger than the amount we can write, just write the allowed
                // amount and let the rest of the data be written with the next filestream
                bytesWritten = ((count - totalWritten) > bytesWeCanWrite) ? bytesWeCanWrite : (count - totalWritten);

                // Write the data
                stream.Write(buffer, data.buffer.Offset + (int)totalWritten, (int)bytesWritten);

                // Any further data should be written to the next available file
                totalWritten += bytesWritten;
                i++;
            }
            ClientEngine.BufferManager.FreeBuffer(ref data.buffer);
            //monitor.BytesReceived((int)totalWritten, TransferType.Data);
        }

        public override bool Exists(TorrentFile file)
        {
            return File.Exists(file.FullPath);
        }

        public override void Flush(TorrentFile file)
        {
            Stream s = streamsBuffer.FindStream(file.FullPath);
            if (s != null)
                s.Flush();
        }
    }
}
