using System;

namespace MonoTorrent.Common
{
    public class TorrentCreatorEventArgs : EventArgs
    {
        #region Constructors

        internal TorrentCreatorEventArgs(string file, long fileHashed, long fileTotal, long overallHashed,
            long overallTotal)
        {
            CurrentFile = file;
            FileBytesHashed = fileHashed;
            FileSize = fileTotal;
            OverallBytesHashed = overallHashed;
            OverallSize = overallTotal;
        }

        #endregion Constructors

        #region Member Variables

        #endregion Member Variables

        #region Properties

        /// <summary>
        ///     The number of bytes hashed from the current file
        /// </summary>
        public long FileBytesHashed { get; }

        /// <summary>
        ///     The size of the current file
        /// </summary>
        public long FileSize { get; }

        /// <summary>
        ///     The percentage of the current file which has been hashed (range 0-100)
        /// </summary>
        public double FileCompletion
        {
            get { return FileBytesHashed/(double) FileSize*100.0; }
        }

        /// <summary>
        ///     The number of bytes hashed so far
        /// </summary>
        public long OverallBytesHashed { get; }

        /// <summary>
        ///     The total number of bytes to hash
        /// </summary>
        public long OverallSize { get; }

        /// <summary>
        ///     The percentage of the data which has been hashed (range 0-100)
        /// </summary>
        public double OverallCompletion
        {
            get { return OverallBytesHashed/(double) OverallSize*100.0; }
        }

        /// <summary>
        ///     The path of the current file
        /// </summary>
        public string CurrentFile { get; }

        #endregion Properties
    }
}