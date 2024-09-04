//
// EngineSettings.cs
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

using MonoTorrent.Connections;
using MonoTorrent.Dht;
using MonoTorrent.PieceWriter;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    public sealed class EngineSettings : IEquatable<EngineSettings>
    {
        /// <summary>
        /// A prioritised list of encryption methods, including plain text, which can be used to connect to another peer.
        /// Connections will be attempted in the same order as they are in the list. Defaults to <see cref="EncryptionTypes.All"/>,
        /// which is <see cref="EncryptionType.RC4Header"/>, <see cref="EncryptionType.RC4Full"/> and <see cref="EncryptionType.PlainText"/>.
        /// </summary>
        public IList<EncryptionType> AllowedEncryption { get; } = EncryptionTypes.All;

        internal IList<IList<EncryptionType>> OutgoingConnectionEncryptionTiers { get; } = Array.Empty<IList<EncryptionType>> ();

        /// <summary>
        /// Have suppression reduces the number of Have messages being sent by only sending Have messages to peers
        /// which do not already have that piece. A peer will never request a piece they have already downloaded,
        /// so informing them that we have that piece is not beneficial. Defaults to <see langword="false" />.
        /// </summary>
        public bool AllowHaveSuppression { get; } = false;

        /// <summary>
        /// True if the engine should use LocalPeerDiscovery to search for local peers. Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowLocalPeerDiscovery { get; } = true;

        /// <summary>
        /// True if the engine should automatically forward ports using any compatible UPnP or NAT-PMP device.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowPortForwarding { get; } = true;

        /// <summary>
        /// If set to true dht nodes will be implicitly saved when there are no active <see cref="TorrentManager"/> instances in the engine.
        /// Dht nodes will be restored when the first <see cref="TorrentManager"/> is started. Otherwise dht nodes will not be cached between
        /// restarts and the <see cref="IDhtEngine"/> will have to bootstrap from scratch each time.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AutoSaveLoadDhtCache { get; } = true;

        /// <summary>
        /// If set to true FastResume data will be implicitly saved after <see cref="TorrentManager.StopAsync()"/> is invoked,
        /// and will be implicitly loaded before the <see cref="TorrentManager"/> is returned by <see cref="ClientEngine.AddAsync"/>
        /// Otherwise fast resume data will not be saved or restored and <see cref="TorrentManager"/>
        /// instances will have to perform a full hash check when they start.
        /// Defaults to <see langword="true"/>. 
        /// </summary>
        public bool AutoSaveLoadFastResume { get; } = true;

        /// <summary>
        /// This setting affects torrents downloaded using a <see cref="MagnetLink"/>. When enabled, metadata for the torrent will be loaded
        /// from <see cref="MetadataCacheDirectory"/>, if it exists, when the <see cref="MagnetLink"/> is added to the engine using
        /// <see cref="ClientEngine.AddAsync"/>. Additionally, metadata will be written to this directory if it is successfully retrieved
        /// from peers so future downloads can start immediately.
        /// Defaults to <see langword="true"/>. 
        /// </summary>
        public bool AutoSaveLoadMagnetLinkMetadata { get; } = true;

        /// <summary>
        /// The full path to the directory used to cache any data needed by the engine. Typically used to store a
        /// cache of the DHT table to improve bootstrapping speed, any metadata downloaded
        /// using a magnet link, or fast resume data for individual torrents.
        /// Defaults to a sub-directory of <see cref="Environment.CurrentDirectory"/> called 'cache'
        /// </summary>
        public string CacheDirectory { get; } = Path.Combine (Environment.CurrentDirectory, "cache");

        /// <summary>
        /// The delay between each retry when attempting to establish an outgoing connection attempt to a given peer.
        /// Typically an array of length 4 specifying a delay of of 10s, 30s, 60s and 120s. This allows 1 initial attempt
        /// and four retries. If a connection cannot be established after exhausting all retries, the peer's information
        /// will be discarded.
        /// </summary>
        public IList<TimeSpan> ConnectionRetryDelays { get; } = Array.AsReadOnly (new[] {
            TimeSpan.FromSeconds (10),
            TimeSpan.FromSeconds (30),
            TimeSpan.FromSeconds (60),
            TimeSpan.FromSeconds (120),
        });

        /// <summary>
        /// If a connection attempt does not complete within the given timeout, it will be cancelled so
        /// a connection can be attempted with a new peer. Defaults to 10 seconds. It is highly recommended
        /// to keep this value within a range of 7-15 seconds unless absolutely necessary.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; } = Debugger.IsAttached ? TimeSpan.FromSeconds (120) : TimeSpan.FromSeconds (10);

        /// <summary>
        /// Creates a cache which buffers data before it's written to the disk, or after it's been read from disk.
        /// Set to 0 to disable the cache.
        /// Defaults to 5MB.
        /// </summary>
        public int DiskCacheBytes { get; } = 5 * 1024 * 1024;

        /// <summary>
        /// Creates a cache which buffers data before it's written to the disk, or after it's been read from disk.
        /// Set to 0 to disable the cache.
        /// Defaults to 5MB.
        /// </summary>
        public CachePolicy DiskCachePolicy { get; } = CachePolicy.WritesOnly;

        /// <summary>
        /// The UDP port used for DHT communications. Set the port to 0 to choose a random available port.
        /// Set to null to disable DHT. Defaults to IPAddress.Any with port 0.
        /// </summary>
        public IPEndPoint? DhtEndPoint { get; } = new IPEndPoint (IPAddress.Any, 0);

        /// <summary>
        /// This is the full path to a sub-directory of <see cref="CacheDirectory"/>. If <see cref="AutoSaveLoadFastResume"/>
        /// is enabled then fast resume data will be written to this when <see cref="TorrentManager.StopAsync"/> or
        /// <see cref="ClientEngine.StopAllAsync"/> is invoked. If fast resume data is available, the data will be loaded
        /// from disk as part of <see cref="ClientEngine.AddAsync"/> or <see cref="ClientEngine.AddStreamingAsync"/>. If
        /// <see cref="TorrentManager.StartAsync"/> is invoked, any on-disk fast resume data will be deleted to eliminate
        /// the possibility of loading stale data later.
        /// </summary>
        public string FastResumeCacheDirectory => Path.Combine (CacheDirectory, "fastresume");

        /// <summary>
        /// When <see cref="EngineSettings.AutoSaveLoadFastResume"/> is true, this setting is used to control how fast
        /// resume data is maintained, otherwise it has no effect. You can prioritise accuracy (at the risk of requiring full hash checks if an actively downloading
        /// torrent does not cleanly enter the <see cref="TorrentState.Stopped"/> state) by choosing <see cref="FastResumeMode.Accurate"/>.
        /// You can prioritise torrent start speed (at the risk of re-downloading a small amount of data) by choosing <see cref="FastResumeMode.BestEffort"/>,
        /// in which case a recent, not not 100% accurate, copy of the fast resume data will be loaded whenever it is available. if an actively downloading Torrent does not
        /// cleanly enter the <see cref="TorrentState.Stopped"/> state.
        /// Defaults to <see cref="FastResumeMode.BestEffort"/>.
        /// </summary>
        public FastResumeMode FastResumeMode { get; } = FastResumeMode.BestEffort;

        /// <summary>
        /// Sets the preferred approach to creating new files.
        /// </summary>
        public FileCreationOptions FileCreationOptions { get; } = FileCreationOptions.PreferSparse;

        /// <summary>
        /// The list of HTTP(s) endpoints which the engine should bind to when a <see cref="TorrentManager"/> is set up
        /// to stream data from the torrent and <see cref="TorrentManager.StreamProvider"/> is non-null. Should be of
        /// the form "http://ip-address-or-hostname:port". Defaults to 'http://127.0.0.1:5555'.
        /// </summary>
        public string HttpStreamingPrefix { get; } = "http://127.0.0.1:5555/";

        /// <summary>
        /// The TCP port the engine should listen on for incoming connections. Set the port to 0 to use a random
        /// available port, set to null to disable incoming connections. Defaults to IPAddress.Any and IPAddress.AnyIPv6,
        /// both with port 0.
        /// </summary>
        public IDictionary<string, IPEndPoint> ListenEndPoints { get; } = new ReadOnlyDictionary<string, IPEndPoint> (new Dictionary<string, IPEndPoint> {
            {"ipv4", new IPEndPoint (IPAddress.Any, 0) },
            {"ipv6", new IPEndPoint (IPAddress.IPv6Any, 0) }
        });

        /// <summary>
        /// The maximum number of concurrent open connections overall. Defaults to 150.
        /// </summary>
        public int MaximumConnections { get; } = 150;

        /// <summary>
        /// The maximum download rate, in bytes per second, overall. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadRate { get; }

        /// <summary>
        /// The maximum number of concurrent connection attempts overall. Defaults to 8.
        /// </summary>
        public int MaximumHalfOpenConnections { get; } = 8;

        /// <summary>
        /// The maximum upload rate, in bytes per second, overall. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadRate { get; }

        /// <summary>
        /// The maximum number of files which can be opened concurrently. On platforms which limit the maximum
        /// filehandles for a process it can be beneficial to limit the number of open files to prevent
        /// running out of resources. A value of 0 means unlimited, but this is not recommended. Defaults to 196.
        /// </summary>
        public int MaximumOpenFiles { get; } = 196;

        /// <summary>
        /// The maximum disk read rate, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the hashing process from saturating
        /// the available drive bandwidth. Defaults to 0.
        /// </summary>
        public int MaximumDiskReadRate { get; }

        /// <summary>
        /// The maximum disk write rate, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the downloading process from saturating
        /// the available drive bandwidth. If the download rate exceeds the max write rate then the
        /// download will be throttled. Defaults to 0.
        /// </summary>
        public int MaximumDiskWriteRate { get; }

        /// <summary>
        /// If the IPAddress incoming peer connections are received on differs from the IPAddress the tracker
        /// Announce or Scrape requests are sent from, specify it here. Typically this should not be set.
        /// Defaults to <see langword="null" />
        /// </summary>
        public IDictionary<string, IPEndPoint> ReportedListenEndPoints { get; } = new ReadOnlyDictionary<string, IPEndPoint> (new Dictionary<string, IPEndPoint> ());

        /// <summary>
        /// When blocks have been requested from a peer, the connection to that peer will be closed and the
        /// requests will be cancelled if it takes longer than this time to receive a 16kB block. This
        /// value must be higher than <see cref="WebSeedConnectionTimeout"/> or the web seeds will be
        /// considered unhealthy before their connection timeout is exceeded.
        /// Defaults to 40 seconds.
        /// </summary>
        public TimeSpan StaleRequestTimeout { get; } = TimeSpan.FromSeconds (40);

        /// <summary>
        /// This is the full path to a sub-directory of <see cref="CacheDirectory"/>. If a magnet link is used
        /// to download a torrent, the downloaded metata will be cached here.
        /// </summary>
        public string MetadataCacheDirectory => Path.Combine (CacheDirectory, "metadata");

        /// <summary>
        /// If set to <see langword="true"/> then partially downloaded files will have ".!mt" appended to their filename. When the file is fully downloaded, the ".!mt" suffix will be removed.
        /// Defaults to <see langword="false"/> as this is a pre-release feature.
        /// </summary>
        public bool UsePartialFiles { get; } = false;

        /// <summary>
        /// The timeout used when connecting to a WebSeed's HTTP endpoint.
        /// Defaults to 30 seconds.
        /// </summary>
        public TimeSpan WebSeedConnectionTimeout { get; } = TimeSpan.FromSeconds (30);

        /// <summary>
        /// The delay before a torrent will start using web seeds.
        /// Defaults to 1 minute.
        /// </summary>
        public TimeSpan WebSeedDelay { get; } = TimeSpan.FromMinutes (1);

        /// <summary>
        /// The download speed under which a torrent will start using web seeds.
        /// Defaults to 15kB/sec.
        /// </summary>
        public int WebSeedSpeedTrigger { get; } = 15 * 1024;

        public EngineSettings ()
        {

        }

        internal EngineSettings (
            IList<EncryptionType> allowedEncryption, bool allowHaveSuppression, bool allowLocalPeerDiscovery, bool allowPortForwarding,
            bool autoSaveLoadDhtCache, bool autoSaveLoadFastResume, bool autoSaveLoadMagnetLinkMetadata, string cacheDirectory,
            TimeSpan connectionTimeout, IPEndPoint? dhtEndPoint, int diskCacheBytes, CachePolicy diskCachePolicy, FastResumeMode fastResumeMode,
            FileCreationOptions fileCreationMode, Dictionary<string, IPEndPoint> listenEndPoints,
            int maximumConnections, int maximumDiskReadRate, int maximumDiskWriteRate, int maximumDownloadRate, int maximumHalfOpenConnections,
            int maximumOpenFiles, int maximumUploadRate, IDictionary<string, IPEndPoint> reportedListenEndPoints, bool usePartialFiles,
            TimeSpan webSeedConnectionTimeout, TimeSpan webSeedDelay, int webSeedSpeedTrigger, TimeSpan staleRequestTimeout,
            string httpStreamingPrefix, IList<TimeSpan> connectionRetryDelays)
        {
            // Make sure this is immutable now
            AllowedEncryption = EncryptionTypes.MakeReadOnly (allowedEncryption.ToArray ());
            OutgoingConnectionEncryptionTiers = UpdateEncryptionTiers (AllowedEncryption);

            AllowHaveSuppression = allowHaveSuppression;
            AllowLocalPeerDiscovery = allowLocalPeerDiscovery;
            AllowPortForwarding = allowPortForwarding;
            AutoSaveLoadDhtCache = autoSaveLoadDhtCache;
            AutoSaveLoadFastResume = autoSaveLoadFastResume;
            AutoSaveLoadMagnetLinkMetadata = autoSaveLoadMagnetLinkMetadata;
            DhtEndPoint = dhtEndPoint;
            DiskCacheBytes = diskCacheBytes;
            DiskCachePolicy = diskCachePolicy;
            CacheDirectory = cacheDirectory;
            ConnectionRetryDelays = Array.AsReadOnly (connectionRetryDelays.ToArray ());
            ConnectionTimeout = connectionTimeout;
            FastResumeMode = fastResumeMode;
            FileCreationOptions = fileCreationMode;
            HttpStreamingPrefix = httpStreamingPrefix;
            ListenEndPoints = new ReadOnlyDictionary<string, IPEndPoint> (new Dictionary<string, IPEndPoint> (listenEndPoints));
            MaximumConnections = maximumConnections;
            MaximumDiskReadRate = maximumDiskReadRate;
            MaximumDiskWriteRate = maximumDiskWriteRate;
            MaximumDownloadRate = maximumDownloadRate;
            MaximumHalfOpenConnections = maximumHalfOpenConnections;
            MaximumOpenFiles = maximumOpenFiles;
            MaximumUploadRate = maximumUploadRate;
            ReportedListenEndPoints = new ReadOnlyDictionary<string, IPEndPoint> (new Dictionary<string, IPEndPoint> (reportedListenEndPoints));
            StaleRequestTimeout = staleRequestTimeout;
            UsePartialFiles = usePartialFiles;
            WebSeedConnectionTimeout = webSeedConnectionTimeout;
            WebSeedDelay = webSeedDelay;
            WebSeedSpeedTrigger = webSeedSpeedTrigger;
        }

        static IList<IList<EncryptionType>> UpdateEncryptionTiers (IList<EncryptionType> allowedEncryption)
        {
            var tiers = new List<IList<EncryptionType>> ();
            while (allowedEncryption.Count > 0) {
                // If both encrypted methods are consecutive, create a tier consisting of both. The encrypted handshake will take the first
                // one both sides support. Otherwise, create a tier with just that single method.
                //
                // This supports tiers like:
                //      PlainText, RC4Header, RC4Full       [two tiers]
                //      RC4Header, PlainText, RC4Full       [three tiers]
                //      RC4Full, RC4Header, PlainText       [two tiers]
                if (allowedEncryption.Count >= 2 && allowedEncryption[0] != EncryptionType.PlainText && allowedEncryption[1] != EncryptionType.PlainText) {
                    tiers.Add (Array.AsReadOnly (new[] { allowedEncryption[0], allowedEncryption[1] }));
                    allowedEncryption = allowedEncryption.Skip (2).ToArray ();
                } else {
                    tiers.Add (Array.AsReadOnly (new[] { allowedEncryption[0] }));
                    allowedEncryption = allowedEncryption.Skip (1).ToArray ();
                }
            }
            return tiers;
        }

        internal string GetDhtNodeCacheFilePath ()
            => Path.Combine (CacheDirectory, "dht_nodes.cache");

        /// <summary>
        /// Returns the full path to the <see cref="FastResume"/> file for the specified torrent. This is
        /// where data will be written to, or loaded from, when <see cref="AutoSaveLoadFastResume"/> is enabled. 
        /// </summary>
        /// <param name="infoHashes">The infohashes for the torrent</param>
        /// <returns></returns>
        public string GetFastResumePath (InfoHashes infoHashes)
            => Path.Combine (FastResumeCacheDirectory, $"{infoHashes.V1OrV2.ToHex ()}.fresume");

        internal string GetMetadataPath (InfoHashes infoHashes)
            => Path.Combine (MetadataCacheDirectory, $"{infoHashes.V1OrV2.ToHex ()}.torrent");

        internal string GetV2HashesPath (InfoHashes infoHashes)
            => Path.Combine (MetadataCacheDirectory, $"{infoHashes.V2!.ToHex ()}.v2hashes");

        public override bool Equals (object? obj)
            => Equals (obj as EngineSettings);

        public bool Equals (EngineSettings? other)
        {
            return !(other is null)
                   && AllowedEncryption.SequenceEqual (other.AllowedEncryption)
                   && AllowHaveSuppression == other.AllowHaveSuppression
                   && AllowLocalPeerDiscovery == other.AllowLocalPeerDiscovery
                   && AllowPortForwarding == other.AllowPortForwarding
                   && AutoSaveLoadDhtCache == other.AutoSaveLoadDhtCache
                   && AutoSaveLoadFastResume == other.AutoSaveLoadFastResume
                   && AutoSaveLoadMagnetLinkMetadata == other.AutoSaveLoadMagnetLinkMetadata
                   && CacheDirectory == other.CacheDirectory
                   && Equals (DhtEndPoint, other.DhtEndPoint)
                   && DiskCacheBytes == other.DiskCacheBytes
                   && DiskCachePolicy == other.DiskCachePolicy
                   && FastResumeMode == other.FastResumeMode
                   && HttpStreamingPrefix == other.HttpStreamingPrefix
                   && AreEquivalent (ListenEndPoints, other.ListenEndPoints)
                   && AreEquivalent (ReportedListenEndPoints, other.ReportedListenEndPoints)
                   && MaximumConnections == other.MaximumConnections
                   && MaximumDiskReadRate == other.MaximumDiskReadRate
                   && MaximumDiskWriteRate == other.MaximumDiskWriteRate
                   && MaximumDownloadRate == other.MaximumDownloadRate
                   && MaximumHalfOpenConnections == other.MaximumHalfOpenConnections
                   && MaximumOpenFiles == other.MaximumOpenFiles
                   && MaximumUploadRate == other.MaximumUploadRate
                   && StaleRequestTimeout == other.StaleRequestTimeout
                   && UsePartialFiles == other.UsePartialFiles
                   && WebSeedConnectionTimeout == other.WebSeedConnectionTimeout
                   && WebSeedDelay == other.WebSeedDelay
                   && WebSeedSpeedTrigger == other.WebSeedSpeedTrigger
                   ;
        }

        bool AreEquivalent (IDictionary<string, IPEndPoint> first, IDictionary<string, IPEndPoint> second)
        {
            if (first.Count != second.Count)
                return false;
            foreach (var v in first)
                if (!second.TryGetValue (v.Key, out var value) || !v.Value.Equals (value))
                    return false;
            return true;
        }

        public override int GetHashCode ()
        {
            return MaximumConnections +
                   MaximumDownloadRate +
                   MaximumUploadRate +
                   MaximumHalfOpenConnections +
                   CacheDirectory.GetHashCode ();
        }

        internal TimeSpan? GetConnectionRetryDelay (int failedConnectionAttempts)
        {
            // If we've never failed to connect to the peer, connect immediately.
            if (failedConnectionAttempts <= 0)
                return TimeSpan.Zero;

            // If this is the Nth retry (i.e. N previous failure) then we apply
            // the delay at array position N-1.
            if (failedConnectionAttempts - 1 < ConnectionRetryDelays.Count)
                return ConnectionRetryDelays[failedConnectionAttempts - 1];
            return null;
        }
    }
}
