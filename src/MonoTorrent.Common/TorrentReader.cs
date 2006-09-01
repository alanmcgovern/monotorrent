//
// System.String.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
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
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    /// <summary>
    /// 
    /// </summary>
    public class TorrentReader : IDisposable
    {
        private FileStream stream;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="access"></param>
        /// <param name="share"></param>
        public TorrentReader(string path, FileMode mode, FileAccess access, FileShare share)
        {
            this.stream = new FileStream(path, mode, access, share);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int PeekChar()
        {
            if (stream.Position == (stream.Length-1))
                return -1;

            int result = stream.ReadByte();
            stream.Seek(-1, SeekOrigin.Current);
            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int ReadByte()
        {
            return stream.ReadByte();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="array"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public int Read(byte[] array, int offset, int count)
        {
            return this.stream.Read(array, offset, count);
        }


        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.stream.Dispose();
        }
    }
}
