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
using MonoTorrent.Dht;

namespace MonoTorrent.Client
{
    [Serializable]
    public class TorrentSettings : ICloneable
    {
        int maximumConnections = 60;
        int maximumDownloadSpeed;
        int maximumUploadSpeed;
        int uploadSlots = 8;

        /// <summary>
        /// If set to false then the <see cref="DhtEngine"/> registered with the <see cref="ClientEngine" /> will
        /// never be used to locate additional peers. Defaults to true.
        /// </summary>
        public bool AllowDht { get; set; } = true;

        /// <summary>
        /// If set to true, and there are no other seeders for this torrent, then Initial Seeding mode may be used to
        /// prioritise sharing pieces which are not already available in the swarm.
        /// </summary>
        public bool AllowInitialSeeding { get; set; }

        /// <summary>
        /// If set to false then Peer Exchange messages will never be used to locate additional peers. Defaults to true.
        /// </summary>
        public bool AllowPeerExchange { get; set; } = true;

        /// <summary>
        /// The number of peers we should maintain in our internal lists. If we are allowed maintain 100 connections,
        /// we will store 150 peer ip/port combos in memory. This should ensure we hit our maximum allowed connections.
        /// </summary>
        internal int MaximumPeerDetails {
            get => MaximumConnections + 50;
        }

        /// <summary>
        /// The maximum number of concurrent open connections for this torrent. Defaults to 60.
        /// </summary>
        public int MaximumConnections  {
            get => maximumConnections;
            set => maximumConnections = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum download speed, in bytes per second, for this torrent. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadSpeed {
            get => maximumDownloadSpeed;
            set => maximumDownloadSpeed = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum upload speed, in bytes per second, for this torrent. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadSpeed {
            get => maximumUploadSpeed;
            set => maximumUploadSpeed = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The number of peers which can be uploaded to concurrently for this torrent. A value of 0 means unlimited. defaults to 8.
        /// </summary>
        public int UploadSlots {
            get => uploadSlots;
            set => uploadSlots = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The delay before a torrent will start using web seeds.
        /// </summary>
        public TimeSpan WebSeedDelay { get; set; } = TimeSpan.FromMinutes (1);

        /// <summary>
        /// The download speed under which a torrent will start using web seeds.
        /// </summary>
        public int WebSeedSpeedTrigger { get; set; } = 15 * 1024;

        /// <summary>
        /// The choke/unchoke manager reviews how each torrent is making use of its upload slots.  If appropriate, it releases one of the available slots and uses it to try a different peer
        /// in case it gives us more data.  This value determines how long (in seconds) needs to expire between reviews.  If set too short, peers will have insufficient time to start
        /// downloading data and the choke/unchoke manager will choke them too early.  If set too long, we will spend more time than is necessary waiting for a peer to give us data.
        /// The default is 30 seconds.  A value of 0 disables the choke/unchoke manager altogether.
        /// </summary>
        internal TimeSpan MinimumTimeBetweenReviews { get; set; } = TimeSpan.FromSeconds (30);

        /// <summary>
        /// A percentage between 0 and 100; default 90.
        /// When downloading, the choke/unchoke manager doesn't make any adjustments if the download speed is greater than this percentage of the maximum download rate.
        /// That way it will not try to improve download speed when the only likley effect will be to reduce download speeds.
        /// When uploading, the choke/unchoke manager doesn't make any adjustments if the upload speed is greater than this percentage of the maximum upload rate.
        /// </summary>
        internal int PercentOfMaxRateToSkipReview { get; set; } = 90;

        /// <summary>
        /// The time, in seconds, the inactivity manager should wait until it can consider a peer eligible for disconnection.  Peers are disconnected only if they have not provided
        /// any data.  Default is 600.  A value of 0 disables the inactivity manager.
        /// </summary>
        internal TimeSpan TimeToWaitUntilIdle { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// When considering peers that have given us data, the inactivity manager will wait TimeToWaiTUntilIdle plus (Number of bytes we've been sent / ConnectionRetentionFactor) seconds
        /// before they are eligible for disconnection.  Default value is 2000.  A value of 0 prevents the inactivity manager from disconnecting peers that have sent data.
        /// </summary>
        internal long ConnectionRetentionFactor { get; set; } = 1024;

        object ICloneable.Clone()
            => Clone();

        public TorrentSettings Clone()
            => (TorrentSettings) MemberwiseClone();

        public override bool Equals(object obj)
        {
            return obj is TorrentSettings settings
                && AllowInitialSeeding == settings.AllowInitialSeeding
                && MaximumConnections == settings.MaximumConnections
                && MaximumDownloadSpeed == settings.MaximumDownloadSpeed
                && MaximumUploadSpeed == settings.MaximumUploadSpeed
                && UploadSlots == settings.uploadSlots;
        }

        public override int GetHashCode()
        {
            return AllowInitialSeeding.GetHashCode()
                ^ MaximumConnections
                ^ MaximumDownloadSpeed
                ^ MaximumUploadSpeed
                ^ UploadSlots;
        }

        static int CheckZeroOrPositive (int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be zero or greater");
            return value;
        }
    }
}
