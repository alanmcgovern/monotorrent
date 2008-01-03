using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace MonoTorrent.Client
{
    public class BufferedFileRead
    {
        public ManualResetEvent WaitHandle;
        public FileManager Manager;
        public byte[] Buffer;
        public int BufferOffset;
        public long PieceStartIndex;
        public int Count;


        public BufferedFileRead(FileManager manager, byte[] buffer, int bufferOffset, long pieceStartIndex, int count)
        {
            this.Manager = manager;
            this.Buffer = buffer;
            this.BufferOffset = bufferOffset;
            this.PieceStartIndex = pieceStartIndex;
            this.Count = count;
            this.WaitHandle = new ManualResetEvent(false);
        }
    }
}
