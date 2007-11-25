using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class ScrapeResponseEventArgs : TrackerResponseEventArgs
    {
        #region Fields

        private int complete;
        private int downloaded;
        private int incomplete;

        #endregion Fields


        #region Properties

        /// <summary>
        /// The number of seeders downloading the torrent
        /// </summary>
        public int Complete
        {
            get { return complete; }
        }

        /// <summary>
        /// The number of times the torrent was downloaded
        /// </summary>
        public int Downloaded
        {
            get { return downloaded; }
        }

        /// <summary>
        /// The number of peers downloading the torrent who are not seeders.
        /// </summary>
        public int Incomplete
        {
            get { return incomplete; }
        }

        #endregion Properties


        #region Constructors

        public ScrapeResponseEventArgs(Tracker tracker)
            : this(tracker, 0, 0, 0)
        {
        }

        public ScrapeResponseEventArgs(Tracker tracker, int complete, int downloaded, int incomplete)
            : base(tracker)
        {
            this.complete = complete;
            this.downloaded = downloaded;
            this.incomplete = incomplete;
        }

        #endregion Constructorss
    }
}
