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
    public sealed class TorrentSettings : IEquatable<TorrentSettings>
    {
        /// <summary>
        /// If set to false then the <see cref="IDhtEngine"/> registered with the <see cref="ClientEngine" /> will
        /// never be used to locate additional peers. Defaults to true.
        /// </summary>
        public bool AllowDht { get; } = true;

        /// <summary>
        /// If set to true, and there are no other seeders for this torrent, then Initial Seeding mode may be used to
        /// prioritise sharing pieces which are not already available in the swarm.
        /// </summary>
        public bool AllowInitialSeeding { get; }

        /// <summary>
        /// If set to false then Peer Exchange messages will never be used to locate additional peers. Defaults to true.
        /// </summary>
        public bool AllowPeerExchange { get; } = true;

        /// <summary>
        /// If set to true all files in a multi-file torrent will be placed inside a containing directory.
        /// The directory name will be derived from <see cref="MagnetLink.Name"/> or <see cref="Torrent.Name"/>.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool CreateContainingDirectory { get; } = true;

        /// <summary>
        /// The maximum number of concurrent open connections for this torrent. Defaults to 60.
        /// </summary>
        public int MaximumConnections { get; } = 60;

        /// <summary>
        /// The maximum download rate, in bytes per second, for this torrent. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadRate { get; }

        /// <summary>
        /// The maximum upload rate, in bytes per second, for this torrent. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadRate { get; }

        /// <summary>
        /// The BitTorrent specification requires that clients which initiate an outgoing connection to
        /// a remote peer must close that connection if the remote peer reports a different 'peer_id' than
        /// it previously reported to the tracker. Several prominant BitTorrent clients/libraries, such as
        /// libtorrent, randomise their peer id. Additionally, if the announce request requests a compact
        /// response, the peer id will not be known anyway. Defaults to <see langword="false"/>.
        /// </summary>
        public bool RequirePeerIdToMatch { get; }

        /// <summary>
        /// The number of peers which can be uploaded to concurrently for this torrent. A value of 0 means unlimited. defaults to 8.
        /// </summary>
        public int UploadSlots { get; } = 8;

        /// <summary>
        /// When considering peers that have given us data, the inactivity manager will wait TimeToWaiTUntilIdle plus (Number of bytes we've been sent / ConnectionRetentionFactor) seconds
        /// before they are eligible for disconnection.  Default value is 2000.  A value of 0 prevents the inactivity manager from disconnecting peers that have sent data.
        /// </summary>
        internal int ConnectionRetentionFactor => 1024;

        /// <summary>
        /// The number of peers we should maintain in our internal lists. If we are allowed maintain 100 connections,
        /// we will store 150 peer ip/port combos in memory. This should ensure we hit our maximum allowed connections.
        /// </summary>
        internal int MaximumPeerDetails => MaximumConnections + 50;

        /// <summary>
        /// The time, in seconds, the inactivity manager should wait until it can consider a peer eligible for disconnection.  Peers are disconnected only if they have not provided
        /// any data.  Default is 600.  A value of 0 disables the inactivity manager.
        /// </summary>
        internal TimeSpan TimeToWaitUntilIdle => TimeSpan.FromMinutes (10);

        public TorrentSettings ()
        {

        }

        internal TorrentSettings (bool allowDht, bool allowInitialSeeding, bool allowPeerExchange, int maximumConnections, int maximumDownloadRate, int maximumUploadRate, int uploadSlots, bool createContainingDirectory, bool requirePeerIdToMatch)
        {
            AllowDht = allowDht;
            AllowInitialSeeding = allowInitialSeeding;
            AllowPeerExchange = allowPeerExchange;
            CreateContainingDirectory = createContainingDirectory;
            MaximumConnections = maximumConnections;
            MaximumDownloadRate = maximumDownloadRate;
            MaximumUploadRate = maximumUploadRate;
            RequirePeerIdToMatch = requirePeerIdToMatch;
            UploadSlots = uploadSlots;
        }

        public override bool Equals (object? obj)
            => Equals (obj as TorrentSettings);

        public bool Equals (TorrentSettings? other)
        {
            return !(other is null)
                && AllowDht == other.AllowDht
                && AllowInitialSeeding == other.AllowInitialSeeding
                && AllowPeerExchange == other.AllowPeerExchange
                && CreateContainingDirectory == other.CreateContainingDirectory
                && MaximumConnections == other.MaximumConnections
                && MaximumDownloadRate == other.MaximumDownloadRate
                && MaximumUploadRate == other.MaximumUploadRate
                && RequirePeerIdToMatch == other.RequirePeerIdToMatch
                && UploadSlots == other.UploadSlots;
        }

        public override int GetHashCode ()
        {
            return AllowInitialSeeding.GetHashCode ()
                ^ MaximumConnections
                ^ MaximumDownloadRate
                ^ MaximumUploadRate
                ^ UploadSlots;
        }
    }
}
