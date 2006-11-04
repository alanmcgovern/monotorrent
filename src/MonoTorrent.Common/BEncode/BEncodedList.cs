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
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MonoTorrent.Common
{
    /// <summary>
    /// Class representing a BEncoded list
    /// </summary>
    public class BEncodedList : IBEncodedValue, IList<IBEncodedValue>
    {
        #region Member Variables
        private List<IBEncodedValue> list;
        #endregion


        #region Constructors
        /// <summary>
        /// Create a new BEncoded List with default capacity
        /// </summary>
        public BEncodedList()
            : this(new List<IBEncodedValue>())
        {
        }

        /// <summary>
        /// Create a new BEncoded List with the supplied capacity
        /// </summary>
        /// <param name="capacity">The initial capacity</param>
        public BEncodedList(int capacity)
            : this(new List<IBEncodedValue>(capacity))
        {

        }

        /// <summary>
        /// Creates a new BEncoded list from the supplied List
        /// </summary>
        /// <param name="value">The list to use to create the BEncodedList</param>
        public BEncodedList(List<IBEncodedValue> value)
        {
            this.list = value;
        }
        #endregion


        #region Encode/Decode Methods
        /// <summary>
        /// Encodes the list to a byte[] using the supplied Encoding
        /// </summary>
        /// <returns></returns>
        public byte[] Encode()
        {
            byte[] buffer = new byte[this.LengthInBytes()];
            this.Encode(buffer, 0);
            return buffer;
        }


        /// <summary>
        /// Encodes the list to a byte[] using the supplied encoding
        /// </summary>
        /// <param name="buffer">The buffer to encode the list to</param>
        /// <param name="offset">The offset to start writing the data at</param>
        /// <returns></returns>
        public int Encode(byte[] buffer, int offset)
        {
            int written = 0;
            written += System.Text.Encoding.UTF8.GetBytes("l", 0, 1, buffer, offset);

            foreach (IBEncodedValue value in this.list)
                written += value.Encode(buffer, offset + written);

            written += System.Text.Encoding.UTF8.GetBytes("e", 0, 1, buffer, offset + written);
            return written;
        }

        /// <summary>
        /// Decodes a BEncodedList from the given StreamReader
        /// </summary>
        /// <param name="reader"></param>
        public void Decode(BinaryReader reader)
        {
            try
            {
                if (reader.ReadByte() != 'l')                            // Remove the leading 'l'
                    throw new BEncodingException("Invalid data found. Aborting");

                while ((reader.PeekChar() != -1) && ((char)reader.PeekChar() != 'e'))
                    list.Add(BEncode.Decode(reader));

                if (reader.ReadByte() != 'e')                            // Remove the trailing 'e'
                    throw new BEncodingException("Invalid data found. Aborting");
            }
            catch (BEncodingException ex)
            {
                throw new BEncodingException("Couldn't decode list", ex);
            }
            catch
            {
                throw new BEncodingException("Couldn't decode list");
            }
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Returns the size of the list in bytes using the supplied encoding
        /// </summary>
        /// <param name="e">The encoding to use</param>
        /// <returns></returns>
        public int LengthInBytes()
        {
            int length = 0;

            length += System.Text.Encoding.UTF8.GetByteCount("l");   // Lists start with 'l'
            foreach (IBEncodedValue item in this.list)
                length += item.LengthInBytes();

            length += System.Text.Encoding.UTF8.GetByteCount("e");   // Lists end with 'e'
            return length;
        }
        #endregion


        #region Overridden Methods
        public override bool Equals(object obj)
        {
            BEncodedList obj2 = obj as BEncodedList;

            if (obj2 == null)
                return false;

            for (int i = 0; i < this.list.Count; i++)
                if (this.list[i] != obj2.list[i])
                    return false;

            return true;
        }


        public override int GetHashCode()
        {
            return this.list.GetHashCode();
        }


        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(32);

            foreach (IBEncodedValue value in this.list)
                sb.Append(value.ToString());

            return sb.ToString();
        }
        #endregion


        #region IList methods
        public void Add(IBEncodedValue item)
        {
            this.list.Add(item);
        }

        public void Clear()
        {
            this.list.Clear();
        }

        public bool Contains(IBEncodedValue item)
        {
            return this.list.Contains(item);
        }

        public void CopyTo(IBEncodedValue[] array, int arrayIndex)
        {
            this.list.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return this.list.Count; }
        }

        public int IndexOf(IBEncodedValue item)
        {
            return this.list.IndexOf(item);
        }

        public void Insert(int index, IBEncodedValue item)
        {
            this.list.Insert(index, item);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(IBEncodedValue item)
        {
            return this.list.Remove(item);
        }

        public void RemoveAt(int index)
        {
            this.list.RemoveAt(index);
        }

        public IBEncodedValue this[int index]
        {
            get { return this.list[index]; }
            set { this.list[index] = value; }
        }

        public IEnumerator<IBEncodedValue> GetEnumerator()
        {
            return this.list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
        #endregion
    }
}