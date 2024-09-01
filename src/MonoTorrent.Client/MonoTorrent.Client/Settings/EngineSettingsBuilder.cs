//
// EngineSettingsBuilder.cs
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
using System.Collections.Generic;
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
    public class EngineSettingsBuilder
    {
        TimeSpan connectionTimeout;
        int diskCacheBytes;
        string httpStreamingPrefix;
        int maximumConnections;
        int maximumDiskReadRate;
        int maximumDiskWriteRate;
        int maximumDownloadRate;
        int maximumHalfOpenConnections;
        int maximumOpenFiles;
        int maximumUploadRate;
        TimeSpan staleRequestTimeout;
        TimeSpan webSeedConnectionTimeout;
        TimeSpan webSeedDelay;
        int webSeedSpeedTrigger;


        /// <summary>
        /// A prioritised list of encryption methods, including plain text, which can be used to connect to another peer.
        /// Connections will be attempted in the same order as they are in the list. Defaults to <see cref="EncryptionType.RC4Header"/>,
        /// <see cref="EncryptionType.RC4Full"/> and <see cref="EncryptionType.PlainText"/>.
        /// </summary>
        public List<EncryptionType> AllowedEncryption { get; set; }

        /// <summary>
        /// Have suppression reduces the number of Have messages being sent by only sending Have messages to peers
        /// which do not already have that piece. A peer will never request a piece they have already downloaded,
        /// so informing them that we have that piece is not beneficial. Defaults to <see langword="false" />.
        /// </summary>
        public bool AllowHaveSuppression { get; set; }

        /// <summary>
        /// True if the engine should use LocalPeerDiscovery to search for local peers. Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowLocalPeerDiscovery { get; set; }

        /// <summary>
        /// True if the engine should automatically forward ports using any compatible UPnP or NAT-PMP device.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AllowPortForwarding { get; set; }

        /// <summary>
        /// If set to true dht nodes will be implicitly saved when there are no active <see cref="TorrentManager"/> instances in the engine.
        /// Dht nodes will be restored when the first <see cref="TorrentManager"/> is started. Otherwise dht nodes will not be cached between
        /// restarts and the <see cref="IDhtEngine"/> will have to bootstrap from scratch each time.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AutoSaveLoadDhtCache { get; set; }

        /// <summary>
        /// If set to true FastResume data will be implicitly saved after <see cref="TorrentManager.StopAsync()"/> is invoked,
        /// and will be implicitly loaded before the <see cref="TorrentManager"/> is returned by <see cref="ClientEngine.AddAsync"/>
        /// Otherwise fast resume data will not be saved or restored and <see cref="TorrentManager"/>
        /// instances will have to perform a full hash check when they start.
        /// Defaults to <see langword="true"/>. 
        /// </summary>
        public bool AutoSaveLoadFastResume { get; set; }

        /// <summary>
        /// This setting affects torrents downloaded using a <see cref="MagnetLink"/>. When enabled, metadata for the torrent will be loaded
        /// from <see cref="EngineSettings.MetadataCacheDirectory"/>, if it exists, when the <see cref="MagnetLink"/> is added to the engine using
        /// <see cref="ClientEngine.AddAsync"/>. Additionally, metadata will be written to this directory if it is successfully retrieved
        /// from peers so future downloads can start immediately.
        /// Defaults to <see langword="true"/>. 
        /// </summary>
        public bool AutoSaveLoadMagnetLinkMetadata { get; set; }

        /// <summary>
        /// The directory used to cache any data needed by the engine. Typically used to store a
        /// cache of the DHT table to improve bootstrapping speed, any metadata downloaded
        /// using a magnet link, or fast resume data for individual torrents.
        /// When <see cref="ToSettings"/> is invoked the value will be converted to a full path
        /// if it is not already a full path, or will be replaced with
        /// <see cref="Environment.CurrentDirectory"/> if the value is null or empty.
        /// </summary>
        public string CacheDirectory { get; set; }

        /// <summary>
        /// The delay between each retry when attempting to establish an outgoing connection attempt to a given peer.
        /// Typically an array of length 4 specifying a delay of of 10s, 30s, 60s and 120s. This allows 1 initial attempt
        /// and four retries. If a connection cannot be established after exhausting all retries, the peer's information
        /// will be discarded.
        /// </summary>
        public List<TimeSpan> ConnectionRetryDelays { get; set; }

        /// <summary>
        /// If a connection attempt does not complete within the given timeout, it will be cancelled so
        /// a connection can be attempted with a new peer. Defaults to 10 seconds. It is highly recommended
        /// to keep this value within a range of 7-15 seconds unless absolutely necessary.
        /// </summary>
        public TimeSpan ConnectionTimeout {
            get => connectionTimeout;
            set {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException (nameof (value), "The timeout must be greater than 0");
                connectionTimeout = value;
            }
        }

        /// <summary>
        /// Creates a cache which buffers data before it's written to the disk, or after it's been read from disk.
        /// Set to 0 to disable the cache.
        /// Defaults to 5MB.
        /// </summary>
        public int DiskCacheBytes {
            get => diskCacheBytes;
            set => diskCacheBytes = CheckZeroOrPositive (value);
        }


        /// <summary>
        /// Determines if writes should be cached, or if reads and writes should be cached.
        /// </summary>
        public CachePolicy DiskCachePolicy { get; set; }

        /// <summary>
        /// The UDP port used for DHT communications. Use 0 to choose a random available port.
        /// Choose -1 to disable DHT. Defaults to 0.
        /// </summary>
        public IPEndPoint? DhtEndPoint { get; set; }

        /// <summary>
        /// When <see cref="EngineSettings.AutoSaveLoadFastResume"/> is true, this setting is used to control how fast
        /// resume data is maintained, otherwise it has no effect. You can prioritise accuracy (at the risk of requiring full hash checks if an actively downloading
        /// torrent does not cleanly enter the <see cref="TorrentState.Stopped"/> state) by choosing <see cref="FastResumeMode.Accurate"/>.
        /// You can prioritise torrent start speed (at the risk of re-downloading a small amount of data) by choosing <see cref="FastResumeMode.BestEffort"/>,
        /// in which case a recent, not not 100% accurate, copy of the fast resume data will be loaded whenever it is available. if an actively downloading Torrent does not
        /// cleanly enter the <see cref="TorrentState.Stopped"/> state.
        /// Defaults to <see cref="FastResumeMode.BestEffort"/>.
        /// </summary>
        public FastResumeMode FastResumeMode { get; set; }

        /// <summary>
        /// Sets the preferred approach to creating new files.
        /// </summary>
        public FileCreationOptions FileCreationMode { get; set; }

        /// <summary>
        /// The HTTP(s) prefix which the engine should bind to when a <see cref="TorrentManager"/> is set up
        /// to stream data from the torrent and <see cref="TorrentManager.StreamProvider"/> is non-null. Should be of
        /// the form "http://ip-address-or-hostname:port". Defaults to 'http://127.0.0.1:5555'.
        /// </summary>
        public string HttpStreamingPrefix {
            get => httpStreamingPrefix;
            set => httpStreamingPrefix = CheckHttpStreamingPrefix (value);
        }

        static string CheckHttpStreamingPrefix (string value)
        {
            if (value is null)
                throw new ArgumentNullException (nameof (value));

            if (!value.EndsWith ("/"))
                throw new ArgumentException ("The HTTP prefix must end in '/' ", nameof (value));

            if (!value.StartsWith ("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith ("https://", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException ("The HTTP prefix must begin with 'http://' or 'https://'", nameof (value));

            return value;
        }

        /// <summary>
        /// The TCP port the engine should listen on for incoming connections. Use 0 to choose a random
        /// available port. Choose -1 to disable listening for incoming connections. Defaults to 0.
        /// </summary>
        public Dictionary<string, IPEndPoint> ListenEndPoints { get; set; }

        /// <summary>
        /// The maximum number of concurrent open connections overall. Defaults to 150.
        /// </summary>
        public int MaximumConnections {
            get => maximumConnections;
            set => maximumConnections = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum download rate, in bytes per second, overall. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadRate {
            get => maximumDownloadRate;
            set => maximumDownloadRate = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum number of concurrent connection attempts overall. Defaults to 8.
        /// </summary>
        public int MaximumHalfOpenConnections {
            get => maximumHalfOpenConnections;
            set => maximumHalfOpenConnections = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum upload rate, in bytes per second, overall. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadRate {
            get => maximumUploadRate;
            set => maximumUploadRate = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum number of files which can be opened concurrently. On platforms which limit the maximum
        /// filehandles for a process it can be beneficial to limit the number of open files to prevent
        /// running out of resources. A value of 0 means unlimited, but this is not recommended. Defaults to 20.
        /// </summary>
        public int MaximumOpenFiles {
            get => maximumOpenFiles;
            set => maximumOpenFiles = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum disk read rate, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the hashing process from saturating
        /// the available drive bandwidth. Defaults to 0.
        /// </summary>
        public int MaximumDiskReadRate {
            get => maximumDiskReadRate;
            set => maximumDiskReadRate = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The maximum disk write rate, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the downloading process from saturating
        /// the available drive bandwidth. If the download rate exceeds the max write rate then the
        /// download will be throttled. Defaults to 0.
        /// </summary>
        public int MaximumDiskWriteRate {
            get => maximumDiskWriteRate;
            set => maximumDiskWriteRate = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// If the IPAddress incoming peer connections are received on differs from the IPAddress the tracker
        /// Announce or Scrape requests are sent from, specify it here. Typically this should not be set.
        /// Defaults to <see langword="null" />
        /// </summary>
        public Dictionary<string, IPEndPoint> ReportedListenEndPoints { get; set; }

        /// <summary>
        /// When blocks have been requested from a peer, the connection to that peer will be closed and the
        /// requests will be cancelled if it takes longer than this time to receive a 16kB block. This
        /// value must be higher than <see cref="WebSeedConnectionTimeout"/> or the web seeds will be
        /// considered unhealthy before their connection timeout is exceeded. Defaults to 40 seconds.
        /// </summary>
        public TimeSpan StaleRequestTimeout {
            get => staleRequestTimeout;
            set => staleRequestTimeout = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// If set to <see langword="true"/> then partially downloaded files will have ".!mt" appended to their filename. When the file is fully downloaded, the ".!mt" suffix will be removed.
        /// Defaults to <see langword="false"/> as this is a pre-release feature.
        /// </summary>
        public bool UsePartialFiles { get; set; }

        /// <summary>
        /// The timeout used when connecting to a WebSeed's HTTP endpoint.
        /// </summary>
        public TimeSpan WebSeedConnectionTimeout {
            get => webSeedConnectionTimeout;
            set => webSeedConnectionTimeout = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The delay before a torrent will start using web seeds. A value of zero
        /// means 'immediately'. Using webseeds by default is not recommended
        /// </summary>
        public TimeSpan WebSeedDelay {
            get => webSeedDelay;
            set => webSeedDelay = CheckZeroOrPositive (value);
        }

        /// <summary>
        /// The download speed under which a torrent will start using web seeds. A value of
        /// 0 means webseeds will be added regardless of how fast the torrent is downloading.
        /// </summary>
        public int WebSeedSpeedTrigger {
            get => webSeedSpeedTrigger;
            set => webSeedSpeedTrigger = CheckZeroOrPositive (value);
        }

        public EngineSettingsBuilder ()
            : this (new EngineSettings ())
        {

        }

        public EngineSettingsBuilder (EngineSettings settings)
        {
            // Make sure this is a mutable list.
            AllowedEncryption = new List<EncryptionType> (settings.AllowedEncryption);
            AllowHaveSuppression = settings.AllowHaveSuppression;
            AllowLocalPeerDiscovery = settings.AllowLocalPeerDiscovery;
            AllowPortForwarding = settings.AllowPortForwarding;
            AutoSaveLoadDhtCache = settings.AutoSaveLoadDhtCache;
            AutoSaveLoadFastResume = settings.AutoSaveLoadFastResume;
            AutoSaveLoadMagnetLinkMetadata = settings.AutoSaveLoadMagnetLinkMetadata;
            CacheDirectory = settings.CacheDirectory;
            ConnectionRetryDelays = new List<TimeSpan> (settings.ConnectionRetryDelays);
            ConnectionTimeout = settings.ConnectionTimeout;
            DhtEndPoint = settings.DhtEndPoint;
            DiskCacheBytes = settings.DiskCacheBytes;
            DiskCachePolicy = settings.DiskCachePolicy;
            FastResumeMode = settings.FastResumeMode;
            FileCreationMode = settings.FileCreationOptions;
            httpStreamingPrefix = settings.HttpStreamingPrefix;
            ListenEndPoints = new Dictionary<string, IPEndPoint> (settings.ListenEndPoints);
            ReportedListenEndPoints = new Dictionary<string, IPEndPoint> (settings.ReportedListenEndPoints);
            MaximumConnections = settings.MaximumConnections;
            MaximumDiskReadRate = settings.MaximumDiskReadRate;
            MaximumDiskWriteRate = settings.MaximumDiskWriteRate;
            MaximumDownloadRate = settings.MaximumDownloadRate;
            MaximumHalfOpenConnections = settings.MaximumHalfOpenConnections;
            MaximumOpenFiles = settings.MaximumOpenFiles;
            MaximumUploadRate = settings.MaximumUploadRate;
            StaleRequestTimeout = settings.StaleRequestTimeout;
            UsePartialFiles = settings.UsePartialFiles;
            WebSeedConnectionTimeout = settings.WebSeedConnectionTimeout;
            WebSeedDelay = settings.WebSeedDelay;
            WebSeedSpeedTrigger = settings.WebSeedSpeedTrigger;
        }

        public EngineSettings ToSettings ()
        {
            if (AllowedEncryption == null)
                throw new ArgumentNullException ("AllowedEncryption", "Cannot be null");
            if (AllowedEncryption.Count == 0)
                throw new ArgumentException ("At least one encryption type must be specified");
            if (AllowedEncryption.Distinct ().Count () != AllowedEncryption.Count)
                throw new ArgumentException ("Each encryption type can be specified at most once. Please verify the AllowedEncryption list contains no duplicates", "AllowedEncryption");

            if (ConnectionRetryDelays is null)
                throw new ArgumentNullException (nameof (ConnectionRetryDelays));
            if (ConnectionRetryDelays.Any (t => t < TimeSpan.Zero))
                throw new ArgumentException ("ConnectionRetryDelays cannot be less than zero");

            return new EngineSettings (
                allowedEncryption: AllowedEncryption,
                allowHaveSuppression: AllowHaveSuppression,
                allowLocalPeerDiscovery: AllowLocalPeerDiscovery,
                allowPortForwarding: AllowPortForwarding,
                autoSaveLoadDhtCache: AutoSaveLoadDhtCache,
                autoSaveLoadFastResume: AutoSaveLoadFastResume,
                autoSaveLoadMagnetLinkMetadata: AutoSaveLoadMagnetLinkMetadata,
                cacheDirectory: string.IsNullOrEmpty (CacheDirectory) ? Environment.CurrentDirectory : Path.GetFullPath (CacheDirectory),
                connectionRetryDelays: ConnectionRetryDelays,
                connectionTimeout: ConnectionTimeout,
                dhtEndPoint: DhtEndPoint,
                diskCacheBytes: DiskCacheBytes,
                diskCachePolicy: DiskCachePolicy,
                fastResumeMode: FastResumeMode,
                fileCreationMode: FileCreationMode,
                httpStreamingPrefix: HttpStreamingPrefix,
                listenEndPoints: ListenEndPoints,
                maximumConnections: MaximumConnections,
                maximumDiskReadRate: MaximumDiskReadRate,
                maximumDiskWriteRate: MaximumDiskWriteRate,
                maximumDownloadRate: MaximumDownloadRate,
                maximumHalfOpenConnections: MaximumHalfOpenConnections,
                maximumOpenFiles: MaximumOpenFiles,
                maximumUploadRate: MaximumUploadRate,
                reportedListenEndPoints: ReportedListenEndPoints,
                staleRequestTimeout: StaleRequestTimeout,
                usePartialFiles: UsePartialFiles,
                webSeedConnectionTimeout: WebSeedConnectionTimeout,
                webSeedDelay: WebSeedDelay,
                webSeedSpeedTrigger: webSeedSpeedTrigger
            );
        }

        static int CheckPort (int value)
        {
            if (value < -1 || value > ushort.MaxValue)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be a valid port number between 0 and 65535 inclusive, or -1 to disable listening for connections.");
            return value;
        }

        static int CheckZeroOrPositive (int value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException (nameof (value), "Value should be zero or greater");
            return value;
        }

        static TimeSpan CheckZeroOrPositive (TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException ("value", "must be greater than or equal to TimeSpan.Zero");
            return value;
        }
    }
}
