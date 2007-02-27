//
// IBEncodedValue.cs
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

namespace MonoTorrent.BEncoding
{
    /// <summary>
    /// Base interface for all BEncoded values.
    /// </summary>
    public interface IBEncodedValue
    {
        /// <summary>
        /// Encodes the BEncodedValue into a byte array
        /// </summary>
        /// <returns>Byte array containing the BEncoded Data</returns>
        byte[] Encode();



        /// <summary>
        /// Encodes the BEncodedValue into the supplied buffer
        /// </summary>
        /// <param name="buffer">The buffer to encode the information to</param>
        /// <param name="offset">The offset in the buffer to start writing the data</param>
        /// <returns></returns>
        int Encode(byte[] buffer, int offset);



        /// <summary>
        /// Decodes the data in the supplied BinaryReader into BEncodedValues
        /// </summary>
        /// <param name="reader">The StreamReader containing the data</param>
        void Decode(BinaryReader reader);


        /// <summary>
        /// Returns the size of the byte[] needed to encode this BEncodedValue
        /// </summary>
        /// <returns></returns>
        int LengthInBytes();
    }
}