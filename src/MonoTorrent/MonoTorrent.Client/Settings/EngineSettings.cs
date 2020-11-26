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
using System.Net;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : IEquatable<EngineSettings>
    {
        /// <summary>
        /// A flags enum representing which encryption methods are allowed. Defaults to <see cref="EncryptionTypes.All"/>.
        /// If <see cref="EncryptionTypes.None"/> is set, then encrypted and unencrypted connections will both be disallowed
        /// and no connections will be made. Defaults to <see cref="EncryptionTypes.All"/>.
        /// </summary>
        public EncryptionTypes AllowedEncryption { get; } = EncryptionTypes.All;

        /// <summary>
        /// Have suppression reduces the number of Have messages being sent by only sending Have messages to peers
        /// which do not already have that piece. A peer will never request a piece they have already downloaded,
        /// so informing them that we have that piece is not beneficial. Defaults to <see langword="false" />.
        /// </summary>
        public bool AllowHaveSuppression { get; }

        /// <summary>
        /// If a connection attempt does not complete within the given timeout, it will be cancelled so
        /// a connection can be attempted with a new peer. Defaults to 10 seconds. It is highly recommended
        /// to keep this value within a range of 7-15 seconds unless absolutely necessary.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; } = TimeSpan.FromSeconds (10);

        /// <summary>
        /// The TCP port the engine should listen on for incoming connections. Use 0 to choose a random
        /// available port. Choose -1 to disable listening for incoming connections. Defaults to 52138.
        /// </summary>
        public int ListenPort { get; } = 52138;

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
        /// running out of resources. A value of 0 means unlimited, but this is not recommended. Defaults to 20.
        /// </summary>
        public int MaximumOpenFiles { get; } = 20;

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
        /// If this is set to false and <see cref="AllowedEncryption"/> allows <see cref="EncryptionTypes.PlainText"/>, then
        /// unencrypted connections will be used by default for new outgoing connections. Otherwise, if <see cref="AllowedEncryption"/>
        /// allows <see cref="EncryptionTypes.RC4Full"/> or <see cref="EncryptionTypes.RC4Header"/> then an encrypted connection
        /// will be used by default for new outgoing connections. Defaults to <see langword="true" />.
        /// </summary>
        public bool PreferEncryption { get; } = true;

        /// <summary>
        /// This is the path where the .torrent metadata will be saved when magnet links are used to start a download.
        /// Defaults to <see langword="null" />
        /// </summary>
        public string SavePath { get; }

        public EngineSettings ()
        {

        }

        internal EngineSettings (EncryptionTypes allowedEncryption, bool allowHaveSuppression, TimeSpan connectionTimeout, int listenPort, int maximumConnections, int maximumDiskReadRate, int maximumDiskWriteRate, int maximumDownloadSpeed, int maximumHalfOpenConnections, int maximumOpenFiles, int maximumUploadSpeed, bool preferEncryption, IPEndPoint reportedAddress, string savePath)
        {
            AllowedEncryption = allowedEncryption;
            AllowHaveSuppression = allowHaveSuppression;
            ConnectionTimeout = connectionTimeout;
            ListenPort = listenPort;
            MaximumConnections = maximumConnections;
            MaximumDiskReadRate = maximumDiskReadRate;
            MaximumDiskWriteRate = maximumDiskWriteRate;
            MaximumDownloadSpeed = maximumDownloadSpeed;
            MaximumHalfOpenConnections = maximumHalfOpenConnections;
            MaximumOpenFiles = maximumOpenFiles;
            MaximumUploadSpeed = maximumUploadSpeed;
            PreferEncryption = preferEncryption;
            ReportedAddress = reportedAddress;
            SavePath = savePath;
        }

        public override bool Equals (object obj)
            => Equals (obj as EngineSettings);

        public bool Equals (EngineSettings other)
        {
            return other != null
                   && AllowedEncryption == other.AllowedEncryption
                   && AllowHaveSuppression == other.AllowHaveSuppression
                   && ListenPort == other.ListenPort
                   && MaximumConnections == other.MaximumConnections
                   && MaximumDiskReadRate == other.MaximumDiskReadRate
                   && MaximumDiskWriteRate == other.MaximumDiskWriteRate
                   && MaximumDownloadSpeed == other.MaximumDownloadSpeed
                   && MaximumHalfOpenConnections == other.MaximumHalfOpenConnections
                   && MaximumOpenFiles == other.MaximumOpenFiles
                   && MaximumUploadSpeed == other.MaximumUploadSpeed
                   && PreferEncryption == other.PreferEncryption
                   && ReportedAddress == other.ReportedAddress
                   && SavePath == other.SavePath;
        }

        public override int GetHashCode ()
        {
            return MaximumConnections +
                   MaximumDownloadSpeed +
                   MaximumUploadSpeed +
                   MaximumHalfOpenConnections +
                   ListenPort.GetHashCode () +
                   AllowedEncryption.GetHashCode () +
                   SavePath.GetHashCode ();
        }
    }
}
