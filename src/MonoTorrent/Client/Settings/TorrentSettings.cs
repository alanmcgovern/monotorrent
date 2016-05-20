using System;

namespace MonoTorrent.Client
{
    [Serializable]
    public class TorrentSettings : ICloneable
    {
        #region Member Variables

        public bool EnablePeerExchange
        {
            get { return enablePeerExchange; }
            set { enablePeerExchange = value; }
        }

        private bool enablePeerExchange = true;

        public bool InitialSeedingEnabled
        {
            get { return initialSeedingEnabled; }
            set { initialSeedingEnabled = value; }
        }

        private bool initialSeedingEnabled;

        public int MaxDownloadSpeed
        {
            get { return maxDownloadSpeed; }
            set { maxDownloadSpeed = value; }
        }

        private int maxDownloadSpeed;

        public int MaxUploadSpeed
        {
            get { return maxUploadSpeed; }
            set { maxUploadSpeed = value; }
        }

        private int maxUploadSpeed;

        public int MaxConnections
        {
            get { return maxConnections; }
            set { maxConnections = value; }
        }

        private int maxConnections;

        public int UploadSlots
        {
            get { return uploadSlots; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("You must use at least 1 upload slot");
                uploadSlots = value;
            }
        }

        private int uploadSlots;

        /// <summary>
        ///     The choke/unchoke manager reviews how each torrent is making use of its upload slots.  If appropriate, it releases
        ///     one of the available slots and uses it to try a different peer
        ///     in case it gives us more data.  This value determines how long (in seconds) needs to expire between reviews.  If
        ///     set too short, peers will have insufficient time to start
        ///     downloading data and the choke/unchoke manager will choke them too early.  If set too long, we will spend more time
        ///     than is necessary waiting for a peer to give us data.
        ///     The default is 30 seconds.  A value of 0 disables the choke/unchoke manager altogether.
        /// </summary>
        public int MinimumTimeBetweenReviews
        {
            get { return minimumTimeBetweenReviews; }
            set { minimumTimeBetweenReviews = value; }
        }

        private int minimumTimeBetweenReviews = 30;

        /// <summary>
        ///     A percentage between 0 and 100; default 90.
        ///     When downloading, the choke/unchoke manager doesn't make any adjustments if the download speed is greater than this
        ///     percentage of the maximum download rate.
        ///     That way it will not try to improve download speed when the only likley effect will be to reduce download speeds.
        ///     When uploading, the choke/unchoke manager doesn't make any adjustments if the upload speed is greater than this
        ///     percentage of the maximum upload rate.
        /// </summary>
        public int PercentOfMaxRateToSkipReview
        {
            get { return percentOfMaxRateToSkipReview; }
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException();
                percentOfMaxRateToSkipReview = value;
            }
        }

        private int percentOfMaxRateToSkipReview = 90;

        /// <summary>
        ///     The time, in seconds, the inactivity manager should wait until it can consider a peer eligible for disconnection.
        ///     Peers are disconnected only if they have not provided
        ///     any data.  Default is 600.  A value of 0 disables the inactivity manager.
        /// </summary>
        public TimeSpan TimeToWaitUntilIdle
        {
            get { return timeToWaitUntilIdle; }
            set
            {
                if (value.TotalSeconds < 0)
                    throw new ArgumentOutOfRangeException();
                timeToWaitUntilIdle = value;
            }
        }

        private TimeSpan timeToWaitUntilIdle = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     When considering peers that have given us data, the inactivity manager will wait TimeToWaiTUntilIdle plus (Number
        ///     of bytes we've been sent / ConnectionRetentionFactor) seconds
        ///     before they are eligible for disconnection.  Default value is 2000.  A value of 0 prevents the inactivity manager
        ///     from disconnecting peers that have sent data.
        /// </summary>
        public long ConnectionRetentionFactor
        {
            get { return connectionRetentionFactor; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException();
                connectionRetentionFactor = value;
            }
        }

        private long connectionRetentionFactor = 1024;

        // FIXME: This value needs to be obeyed if it's changed
        // while the torrent is running
        public bool UseDht
        {
            get { return useDht; }
            set { useDht = value; }
        }

        private bool useDht = true;

        #endregion

        #region Defaults

        private const int DefaultDownloadSpeed = 0;
        private const int DefaultMaxConnections = 60;
        private const int DefaultUploadSlots = 4;
        private const int DefaultUploadSpeed = 0;
        private const bool DefaultInitialSeedingEnabled = false;

        #endregion

        #region Constructors

        public TorrentSettings()
            : this(
                DefaultUploadSlots, DefaultMaxConnections, DefaultDownloadSpeed, DefaultUploadSpeed,
                DefaultInitialSeedingEnabled)
        {
        }

        public TorrentSettings(int uploadSlots)
            : this(
                uploadSlots, DefaultMaxConnections, DefaultDownloadSpeed, DefaultUploadSpeed,
                DefaultInitialSeedingEnabled)
        {
        }

        public TorrentSettings(int uploadSlots, int maxConnections)
            : this(uploadSlots, maxConnections, DefaultDownloadSpeed, DefaultUploadSpeed, DefaultInitialSeedingEnabled)
        {
        }

        public TorrentSettings(int uploadSlots, int maxConnections, int maxDownloadSpeed, int maxUploadSpeed)
            : this(uploadSlots, maxConnections, maxDownloadSpeed, maxUploadSpeed, DefaultInitialSeedingEnabled)
        {
        }

        public TorrentSettings(int uploadSlots, int maxConnections, int maxDownloadSpeed, int maxUploadSpeed,
            bool initialSeedingEnabled)
        {
            this.maxConnections = maxConnections;
            this.maxDownloadSpeed = maxDownloadSpeed;
            this.maxUploadSpeed = maxUploadSpeed;
            this.uploadSlots = uploadSlots;
            this.initialSeedingEnabled = initialSeedingEnabled;
        }

        #endregion

        #region Methods

        object ICloneable.Clone()
        {
            return Clone();
        }

        public TorrentSettings Clone()
        {
            return (TorrentSettings) MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            var settings = obj as TorrentSettings;
            return settings == null
                ? false
                : initialSeedingEnabled == settings.initialSeedingEnabled &&
                  maxConnections == settings.maxConnections &&
                  maxDownloadSpeed == settings.maxDownloadSpeed &&
                  maxUploadSpeed == settings.maxUploadSpeed &&
                  uploadSlots == settings.uploadSlots;
        }

        public override int GetHashCode()
        {
            return initialSeedingEnabled.GetHashCode() ^
                   maxConnections ^
                   maxDownloadSpeed ^
                   maxUploadSpeed ^
                   uploadSlots;
        }

        #endregion Methods
    }
}