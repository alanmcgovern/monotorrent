//
// TorrentSettingsBuilder.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2020 Alan McGovern
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
    public class TorrentSettingsBuilder
    {
        int maximumConnections;
        int maximumDownloadSpeed;
        int maximumUploadSpeed;
        int uploadSlots;

        /// <summary>
        /// If set to false then the <see cref="DhtEngine"/> registered with the <see cref="ClientEngine" /> will
        /// never be used to locate additional peers. Defaults to true.
        /// </summary>
        public bool AllowDht { get; set; }

        /// <summary>
        /// If set to true, and there are no other seeders for this torrent, then Initial Seeding mode may be used to
        /// prioritise sharing pieces which are not already available in the swarm.
        /// </summary>
        public bool AllowInitialSeeding { get; set; }

        /// <summary>
        /// If set to false then Peer Exchange messages will never be used to locate additional peers. Defaults to true.
        /// </summary>
        public bool AllowPeerExchange { get; set; }

        /// <summary>
        /// If set to true all files in a multi-file torrent will be placed inside a containing directory.
        /// The directory name will be derived from <see cref="MagnetLink.Name"/> or <see cref="Torrent.Name"/>.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool CreateContainingDirectory { get; set; }

        /// <summary>
        /// The maximum number of concurrent open connections for this torrent. Defaults to 60.
        /// </summary>
        public int MaximumConnections {
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
        public TimeSpan WebSeedDelay { get; set; }

        /// <summary>
        /// The download speed under which a torrent will start using web seeds.
        /// </summary>
        public int WebSeedSpeedTrigger { get; set; }

        public TorrentSettingsBuilder ()
            : this (new TorrentSettings ())
        {

        }

        public TorrentSettingsBuilder (TorrentSettings settings)
        {
            AllowDht = settings.AllowDht;
            AllowInitialSeeding = settings.AllowInitialSeeding;
            AllowPeerExchange = settings.AllowPeerExchange;
            CreateContainingDirectory = settings.CreateContainingDirectory;
            MaximumConnections = settings.MaximumConnections;
            MaximumDownloadSpeed = settings.MaximumDownloadSpeed;
            MaximumUploadSpeed = settings.MaximumUploadSpeed;
            UploadSlots = settings.UploadSlots;
            WebSeedDelay = settings.WebSeedDelay;
            WebSeedSpeedTrigger = settings.WebSeedSpeedTrigger;
        }

        public TorrentSettings ToSettings ()
        {
            return new TorrentSettings (
                AllowDht,
                AllowInitialSeeding,
                AllowPeerExchange,
                MaximumConnections,
                MaximumDownloadSpeed,
                MaximumUploadSpeed,
                UploadSlots,
                WebSeedDelay,
                WebSeedSpeedTrigger,
                CreateContainingDirectory
            );
        }

        static int CheckZeroOrPositive (int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be zero or greater");
            return value;
        }
    }
}
