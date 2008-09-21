//
// TorrentSettings.cs
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

namespace MonoTorrent.Client
{
    [Serializable]
    public class TorrentSettings : ICloneable
    {
        #region Member Variables

        public bool InitialSeedingEnabled
        {
            get { return this.initialSeedingEnabled; }
            set { this.initialSeedingEnabled = value; }
        }
        private bool initialSeedingEnabled;

        public int MaxDownloadSpeed
        {
            get { return this.maxDownloadSpeed; }
            set { this.maxDownloadSpeed = value; }
        }
        private int maxDownloadSpeed;

        public int MaxUploadSpeed
        {
            get { return this.maxUploadSpeed; }
            set { this.maxUploadSpeed = value; }
        }
        private int maxUploadSpeed;

        public int MaxConnections
        {
            get { return this.maxConnections; }
            set { this.maxConnections = value; }
        }
        private int maxConnections;

        public int UploadSlots
        {
            get { return this.uploadSlots; }
            set
            {
                if (value < 2)
                    throw new ArgumentOutOfRangeException("You must use at least 2 upload slots");
                this.uploadSlots = value;
            }
        }
        private int uploadSlots;

        internal int MinimumTimeBetweenReviews
		{
			get { return this.minimumTimeBetweenReviews; }
			set
			{
				this.minimumTimeBetweenReviews = value;
			}
		}
		private int minimumTimeBetweenReviews = 30;

		internal int PercentOfMaxRateToSkipReview
		{
			get { return this.percentOfMaxRateToSkipReview; }
			set
			{
                if(value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException();
				this.percentOfMaxRateToSkipReview = value;
			}
		}
		private int percentOfMaxRateToSkipReview = 90;

        // FIXME: This value needs to be obeyed if it's changed
        // while the torrent is running
        public bool UseDht
        {
            get { return useDht; }
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
            : this(DefaultUploadSlots, DefaultMaxConnections, DefaultDownloadSpeed, DefaultUploadSpeed, DefaultInitialSeedingEnabled)
        {
        }

        public TorrentSettings(int uploadSlots)
            : this(uploadSlots, DefaultMaxConnections, DefaultDownloadSpeed, DefaultUploadSpeed, DefaultInitialSeedingEnabled)
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

        public TorrentSettings(int uploadSlots, int maxConnections, int maxDownloadSpeed, int maxUploadSpeed, bool initialSeedingEnabled)
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
            return (TorrentSettings)this.MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            TorrentSettings settings = obj as TorrentSettings;
            return (settings == null) ? false : this.initialSeedingEnabled == settings.initialSeedingEnabled && 
                                                this.maxConnections == settings.maxConnections &&
                                                this.maxDownloadSpeed == settings.maxDownloadSpeed &&
                                                this.maxUploadSpeed == settings.maxUploadSpeed &&
                                                this.uploadSlots == settings.uploadSlots;
        }

        public override int GetHashCode()
        {
            return this.initialSeedingEnabled.GetHashCode() ^
                   this.maxConnections ^ 
                   this.maxDownloadSpeed ^ 
                   this.maxUploadSpeed ^ 
                   this.uploadSlots;
        }

        #endregion Methods
    }
}
