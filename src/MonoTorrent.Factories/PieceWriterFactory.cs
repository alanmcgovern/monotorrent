//
// HttpRequestFactory.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2021 Alan McGovern
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

namespace MonoTorrent.PieceWriter
{
    public static class PieceWriterFactory
    {
        static Func<IPieceWriter, long, ByteBufferPool, IBlockCache> blockCacheCreator = (writer, capacity, buffer) => new MemoryCache (buffer, capacity, writer);
        static Func<int, IPieceWriter> pieceWriterCreator = maxOpenFiles => new DiskWriter (maxOpenFiles);

        public static void Register (Func<IPieceWriter, IBlockCache> creator)
            => blockCacheCreator = (writer, capacity, cache) => creator (writer);

        public static void Register (Func<IPieceWriter, long, IBlockCache> creator)
            => blockCacheCreator = (writer, capacity, cache) => creator (writer, capacity);

        public static void Register (Func<IPieceWriter, long, ByteBufferPool, IBlockCache> creator)
            => blockCacheCreator = creator ?? throw new ArgumentNullException (nameof (creator));

        public static void Register (Func<IPieceWriter> creator)
            => pieceWriterCreator = maxOpenFiles => creator ();

        public static void Register(Func<int, IPieceWriter> creator)
            => pieceWriterCreator = creator ?? throw new ArgumentNullException (nameof (creator));

        public static IBlockCache CreateBlockCache (IPieceWriter writer, long capacity, ByteBufferPool bufferPool)
            => blockCacheCreator (writer, capacity, bufferPool);

        public static IPieceWriter CreatePieceWriter (int maximumOpenFiles)
            => pieceWriterCreator (maximumOpenFiles);
    }
}
