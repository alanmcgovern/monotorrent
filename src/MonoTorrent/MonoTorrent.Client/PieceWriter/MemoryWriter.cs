using System;
using System.Collections.Generic;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Common;

namespace MonoTorrent.Client.PieceWriter
{
    public class MemoryWriter : IPieceWriter
    {
        public void Dispose()
        {
        }
        private int bufferSize;
        private List <BufferedIO> memoryBuffer;
        public int BufferSize = 8 * 1024 * 1024;

        public int Used
        {
            get
            {
                int count = 0;
                memoryBuffer.ForEach(delegate(BufferedIO i) { count += i.Buffer.Count; });
                return count;
            }
        }

        public MemoryWriter()
        {
            memoryBuffer = new List<BufferedIO>();
        }


        public int Read(FileManager manager, byte[] buffer, int bufferOffset, long offset, int count)
        {/*
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > manager.FileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            int bytesRead = 0;
            int totalRead = 0;

            for (i = 0; i < manager.Files.Length; i++)       // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < manager.Files[i].Length)              // the start of the data we want to read
                    break;

                offset -= manager.Files[i].Length;           // Offset now contains the index of the data we want
            }                                                   // to read from fileStream[i].

            while (totalRead < count)                           // We keep reading until we have read 'count' bytes.
            {
                if (i == manager.Files.Length)
                    break;

                lock (manager.Files[i])
                {
                    TorrentFileStream s = GetStream(manager, manager.Files[i], FileAccess.Read);
                    s.Seek(offset, SeekOrigin.Begin);
                    offset = 0; // Any further files need to be read from the beginning
                    bytesRead = s.Read(buffer, bufferOffset + totalRead, count - totalRead);
                    totalRead += bytesRead;
                    i++;
                }
            }
            monitor.BytesSent(totalRead, TransferType.Data);
            return totalRead;*/
            return 1;
        }

        public void Write(BufferedIO io, byte[] buffer, int bufferOffset, long offset, int count)
        {/*
            FileManager manager = io.Id.TorrentManager.FileManager;
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (offset < 0 || offset + count > manager.FileSize)
                throw new ArgumentOutOfRangeException("offset");

            int i = 0;
            long bytesWritten = 0;
            long totalWritten = 0;
            long bytesWeCanWrite = 0;

            for (i = 0; i < manager.Files.Length; i++)          // This section loops through all the available
            {                                                   // files until we find the file which contains
                if (offset < manager.Files[i].Length)           // the start of the data we want to write
                    break;

                offset -= manager.Files[i].Length;              // Offset now contains the index of the data we want
            }                                                   // to write to fileStream[i].

            while (totalWritten < count)                        // We keep writing  until we have written 'count' bytes.
            {
                lock (manager.Files[i])
                {
                    TorrentFileStream stream = GetStream(manager, manager.Files[i], FileAccess.ReadWrite);
                    stream.Seek(offset, SeekOrigin.Begin);

                    // Find the maximum number of bytes we can write before we reach the end of the file
                    bytesWeCanWrite = manager.Files[i].Length - offset;

                    // Any further files need to be written from the beginning of the file
                    offset = 0;

                    // If the amount of data we are going to write is larger than the amount we can write, just write the allowed
                    // amount and let the rest of the data be written with the next filestream
                    bytesWritten = ((count - totalWritten) > bytesWeCanWrite) ? bytesWeCanWrite : (count - totalWritten);

                    // Write the data
                    stream.Write(buffer, bufferOffset + (int)totalWritten, (int)bytesWritten);

                    // Any further data should be written to the next available file
                    totalWritten += bytesWritten;
                    i++;
                }
            }

            monitor.BytesReceived((int)totalWritten, TransferType.Data);*/
        }
    }
}
