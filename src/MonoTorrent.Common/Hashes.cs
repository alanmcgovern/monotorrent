using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Common
{
    public class Hashes
    {
        #region Private Fields

        private int count;
        private byte[] hashData;

        #endregion Private Fields


        #region Properties

        public int Count
        {
            get { return this.count; }
        }

        #endregion Properties


        #region Constructors

        public Hashes(byte[] hashData, int count)
        {
            this.hashData = hashData;
            this.count = count;
        }

        #endregion Constructors


        #region Methods

        public bool IsValid(byte[] hash, int hashIndex)
        {
            int start = hashIndex * 20;
            for (int i = 0; i < 20; i++)
                if (hash[i] != this.hashData[i + start])
                    return false;

            return true;
        }

        #endregion Methods
    }
}
