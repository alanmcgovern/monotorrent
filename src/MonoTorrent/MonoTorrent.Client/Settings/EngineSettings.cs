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
using System.IO;
using System.Linq;
using System.Net;

using MonoTorrent.Dht;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : IEquatable<EngineSettings>
    {
        /// <summary>
        /// A prioritised list of encryption methods, including plain text, which can be used to connect to another peer.
        /// Connections will be attempted in the same order as they are in the list. Defaults to <see cref="EncryptionTypes.All"/>,
        /// which is <see cref="EncryptionType.RC4Header"/>, <see cref="EncryptionType.RC4Full"/> and <see cref="EncryptionType.PlainText"/>.
        /// </summary>
        public IList<EncryptionType> AllowedEncryption { get; } = EncryptionTypes.All;

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
        /// If set to true FastResume data will be implicitly saved after <see cref="TorrentManager.StopAsync()"/> is invoked,
        /// and will be implicitly loaded before the <see cref="TorrentManager"/> is returned by <see cref="ClientEngine.AddAsync"/>
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool AutomaticFastResume { get; } = true;

        /// <summary>
        /// The full path to the directory used to cache any data needed by the engine. Typically used to store a
        /// cache of the DHT table to improve bootstrapping speed, any metadata downloaded
        /// using a magnet link, or fast resume data for individual torrents.
        /// Defaults to a sub-directory of <see cref="Environment.CurrentDirectory"/> called 'cache'
        /// </summary>
        public string CacheDirectory { get; } = Path.Combine (Environment.CurrentDirectory, "cache");

        /// <summary>
        /// If a connection attempt does not complete within the given timeout, it will be cancelled so
        /// a connection can be attempted with a new peer. Defaults to 10 seconds. It is highly recommended
        /// to keep this value within a range of 7-15 seconds unless absolutely necessary.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; } = TimeSpan.FromSeconds (10);

        /// <summary>
        /// Creates a cache which buffers data before it's written to the disk, or after it's been read from disk.
        /// Experimental feature. Currently defaults 0. Recommended value when experimenting is 5MB (5 * 1024 * 1024).
        /// </summary>
        public int DiskCacheBytes { get; } = 0;

        /// <summary>
        /// The UDP port used for DHT communications. Use 0 to choose a random available port.
        /// Choose -1 to disable DHT. Defaults to 52139.
        /// </summary>
        public int DhtPort { get; } = 52193;

        /// <summary>
        /// The TCP port the engine should listen on for incoming connections. Use 0 to choose a random
        /// available port. Choose -1 to disable listening for incoming connections. Defaults to 52138.
        /// </summary>
        public int ListenPort { get; } = 52138;

        /// <summary>
        /// This is the full path to a sub-directory of <see cref="CacheDirectory"/>. If <see cref="AutomaticFastResume"/>
        /// is enabled then fast resume data will be written to this when <see cref="TorrentManager.StopAsync"/> or
        /// <see cref="ClientEngine.StopAllAsync"/> is invoked. If fast resume data is available, the data will be loaded
        /// from disk as part of <see cref="ClientEngine.AddAsync"/> or <see cref="ClientEngine.AddStreamingAsync"/>. If
        /// <see cref="TorrentManager.StartAsync"/> is invoked, any on-disk fast resume data will be deleted to eliminate
        /// the possibility of loading stale data later.
        /// </summary>
        public string FastResumeSaveDirectory => Path.Combine (CacheDirectory, "fastresume");

        /// <summary>
        /// The maximum number of concurrent open connections overall. Defaults to 150.
        /// </summary>
        public int MaximumConnections { get; } = 150;

        /// <summary>
        /// The maximum download speed, in bytes per second, overall. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadSpeed { get; }

        /// <summary>
        /// The maximum number of concurrent connection attempts overall. Defaults to 8.
        /// </summary>
        public int MaximumHalfOpenConnections { get; } = 8;

        /// <summary>
        /// The maximum upload speed, in bytes per second, overall. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadSpeed { get; }

        /// <summary>
        /// The maximum number of files which can be opened concurrently. On platforms which limit the maximum
        /// filehandles for a process it can be beneficial to limit the number of open files to prevent
        /// running out of resources. A value of 0 means unlimited, but this is not recommended. Defaults to 196.
        /// </summary>
        public int MaximumOpenFiles { get; } = 196;

        /// <summary>
        /// The maximum disk read speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the hashing process from saturating
        /// the available drive bandwidth. Defaults to 0.
        /// </summary>
        public int MaximumDiskReadRate { get; }

        /// <summary>
        /// The maximum disk write speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the downloading process from saturating
        /// the available drive bandwidth. If the download speed exceeds the max write rate then the
        /// download will be throttled. Defaults to 0.
        /// </summary>
        public int MaximumDiskWriteRate { get; }

        /// <summary>
        /// If the IPAddress incoming peer connections are received on differs from the IPAddress the tracker
        /// Announce or Scrape requests are sent from, specify it here. Typically this should not be set.
        /// Defaults to <see langword="null" />
        /// </summary>
        public IPEndPoint ReportedAddress { get; }

        /// <summary>
        /// This is the full path to a sub-directory of <see cref="CacheDirectory"/>. If a magnet link is used
        /// to download a torrent, the downloaded metata will be cached here.
        /// </summary>
        public string MetadataSaveDirectory => Path.Combine (CacheDirectory, "metadata");

        [Obsolete ("Use the 'CacheDirectory' property instead")]
        public string SavePath => CacheDirectory;

        public EngineSettings ()
        {

        }

        internal EngineSettings (IList<EncryptionType> allowedEncryption, bool allowHaveSuppression, bool allowLocalPeerDiscovery, bool allowPortForwarding, bool automaticFastResume, string cacheDirectory, TimeSpan connectionTimeout, int dhtPort, int diskCacheBytes, int listenPort, int maximumConnections, int maximumDiskReadRate, int maximumDiskWriteRate, int maximumDownloadSpeed, int maximumHalfOpenConnections, int maximumOpenFiles, int maximumUploadSpeed, IPEndPoint reportedAddress)
        {
            // Make sure this is immutable now
            AllowedEncryption = EncryptionTypes.MakeReadOnly (allowedEncryption);
            AllowHaveSuppression = allowHaveSuppression;
            AllowLocalPeerDiscovery = allowLocalPeerDiscovery;
            AllowPortForwarding = allowPortForwarding;
            AutomaticFastResume = automaticFastResume;
            DhtPort = dhtPort;
            DiskCacheBytes = diskCacheBytes;
            CacheDirectory = cacheDirectory;
            ConnectionTimeout = connectionTimeout;
            ListenPort = listenPort;
            MaximumConnections = maximumConnections;
            MaximumDiskReadRate = maximumDiskReadRate;
            MaximumDiskWriteRate = maximumDiskWriteRate;
            MaximumDownloadSpeed = maximumDownloadSpeed;
            MaximumHalfOpenConnections = maximumHalfOpenConnections;
            MaximumOpenFiles = maximumOpenFiles;
            MaximumUploadSpeed = maximumUploadSpeed;
            ReportedAddress = reportedAddress;
        }

        internal string GetMetadataPath (InfoHash infoHash)
            => Path.Combine (MetadataSaveDirectory, infoHash.ToHex () + ".torrent");

        internal string GetFastResumePath (InfoHash infoHash)
            => Path.Combine (FastResumeSaveDirectory, $"{infoHash.ToHex ()}.fresume");

        public override bool Equals (object obj)
            => Equals (obj as EngineSettings);

        public bool Equals (EngineSettings other)
        {
            return other != null
                   && AllowedEncryption.SequenceEqual (other.AllowedEncryption)
                   && AllowHaveSuppression == other.AllowHaveSuppression
                   && AllowLocalPeerDiscovery == other.AllowLocalPeerDiscovery
                   && AllowPortForwarding == other.AllowPortForwarding
                   && AutomaticFastResume == other.AutomaticFastResume
                   && CacheDirectory == other.CacheDirectory
                   && DhtPort == other.DhtPort
                   && DiskCacheBytes == other.DiskCacheBytes
                   && ListenPort == other.ListenPort
                   && MaximumConnections == other.MaximumConnections
                   && MaximumDiskReadRate == other.MaximumDiskReadRate
                   && MaximumDiskWriteRate == other.MaximumDiskWriteRate
                   && MaximumDownloadSpeed == other.MaximumDownloadSpeed
                   && MaximumHalfOpenConnections == other.MaximumHalfOpenConnections
                   && MaximumOpenFiles == other.MaximumOpenFiles
                   && MaximumUploadSpeed == other.MaximumUploadSpeed
                   && ReportedAddress == other.ReportedAddress
                   ;
        }

        public override int GetHashCode ()
        {
            return MaximumConnections +
                   MaximumDownloadSpeed +
                   MaximumUploadSpeed +
                   MaximumHalfOpenConnections +
                   ListenPort.GetHashCode () +
                   AllowedEncryption.GetHashCode () +
                   CacheDirectory.GetHashCode ();
        }
    }
}
