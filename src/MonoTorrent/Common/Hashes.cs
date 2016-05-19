using System;

namespace MonoTorrent.Common
{
    public class Hashes
    {
        #region Constants

        /// <summary>
        ///     Hash code length (in bytes)
        /// </summary>
        internal static readonly int HashCodeLength = 20;

        #endregion

        #region Constructors

        internal Hashes(byte[] hashData, int count)
        {
            this.hashData = hashData;
            Count = count;
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        ///     Number of Hashes (equivalent to number of Pieces)
        /// </summary>
        public int Count { get; }

        #endregion Properties

        #region Private Fields

        private readonly byte[] hashData;

        #endregion Private Fields

        #region Methods

        /// <summary>
        ///     Determine whether a calculated hash is equal to our stored hash
        /// </summary>
        /// <param name="hash">Hash code to check</param>
        /// <param name="hashIndex">Index of hash/piece to verify against</param>
        /// <returns>true iff hash is equal to our stored hash, false otherwise</returns>
        public bool IsValid(byte[] hash, int hashIndex)
        {
            if (hash == null)
                throw new ArgumentNullException("hash");

            if (hash.Length != HashCodeLength)
                throw new ArgumentException(string.Format("Hash must be {0} bytes in length", HashCodeLength), "hash");

            if (hashIndex < 0 || hashIndex > Count)
                throw new ArgumentOutOfRangeException("hashIndex",
                    string.Format("hashIndex must be between 0 and {0}", Count));

            var start = hashIndex*HashCodeLength;
            for (var i = 0; i < HashCodeLength; i++)
                if (hash[i] != hashData[i + start])
                    return false;

            return true;
        }

        /// <summary>
        ///     Returns the hash for a specific piece
        /// </summary>
        /// <param name="hashIndex">Piece/hash index to return</param>
        /// <returns>byte[] (length HashCodeLength) containing hashdata</returns>
        public byte[] ReadHash(int hashIndex)
        {
            if (hashIndex < 0 || hashIndex >= Count)
                throw new ArgumentOutOfRangeException("hashIndex");

            // Read out our specified piece's hash data
            var hash = new byte[HashCodeLength];
            Buffer.BlockCopy(hashData, hashIndex*HashCodeLength, hash, 0, HashCodeLength);

            return hash;
        }

        #endregion Methods
    }
}