//
// Hashes.cs
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

namespace MonoTorrent
{
    public class Hashes
    {
        #region Constants
        /// <summary>
        /// Hash code length (in bytes)
        /// </summary>
        internal static readonly int HashCodeLength = 20;
        #endregion


        #region Private Fields

        readonly byte[] hashData;

        #endregion Private Fields


        #region Properties

        /// <summary>
        /// Number of Hashes (equivalent to number of Pieces)
        /// </summary>
        public int Count { get; }

        #endregion Properties


        #region Constructors

        internal Hashes (byte[] hashData, int count)
        {
            this.hashData = hashData;
            Count = count;
        }

        #endregion Constructors


        #region Methods

        /// <summary>
        /// Determine whether a calculated hash is equal to our stored hash
        /// </summary>
        /// <param name="hash">Hash code to check</param>
        /// <param name="hashIndex">Index of hash/piece to verify against</param>
        /// <returns>true iff hash is equal to our stored hash, false otherwise</returns>
        public bool IsValid (byte[] hash, int hashIndex)
        {
            if (hash == null)
                throw new ArgumentNullException (nameof (hash));

            if (hash.Length != HashCodeLength)
                throw new ArgumentException ($"Hash must be {HashCodeLength} bytes in length", nameof (hash));

            if (hashIndex < 0 || hashIndex >= Count)
                throw new ArgumentOutOfRangeException (nameof (hashIndex), $"hashIndex must be between 0 and {Count}");

            int start = hashIndex * HashCodeLength;
            for (int i = 0; i < HashCodeLength; i++)
                if (hash[i] != hashData[i + start])
                    return false;

            return true;
        }

        /// <summary>
        /// Returns the hash for a specific piece
        /// </summary>
        /// <param name="hashIndex">Piece/hash index to return</param>
        /// <returns>byte[] (length HashCodeLength) containing hashdata</returns>
        public byte[] ReadHash (int hashIndex)
        {
            if (hashIndex < 0 || hashIndex >= Count)
                throw new ArgumentOutOfRangeException (nameof (hashIndex));

            // Read out our specified piece's hash data
            byte[] hash = new byte[HashCodeLength];
            Buffer.BlockCopy (hashData, hashIndex * HashCodeLength, hash, 0, HashCodeLength);

            return hash;
        }

        #endregion Methods
    }
}
