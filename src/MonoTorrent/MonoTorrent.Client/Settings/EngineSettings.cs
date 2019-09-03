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

using MonoTorrent.Client.Encryption;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : ICloneable
    {
        /// <summary>
        /// A flags enum representing which encryption methods are allowed. Defaults to <see cref="EncryptionTypes.All"/>.
        /// If <see cref="EncryptionTypes.None"/> is set, then encrypted and unencrypted connections will both be disallowed
        /// and no connections will be made. Defaults to <see cref="EncryptionTypes.All"/>.
        /// </summary>
        public EncryptionTypes AllowedEncryption { get; set; } = EncryptionTypes.All;

        /// <summary>
        /// Have surpression reduces the number of Have messages being sent by only sending Have messages to peers
        /// which do not already have that piece. A peer will never request a piece they have already downloaded,
        /// so informing them that we have that piece is not beneficial. Defaults to <see langword="false" />.
        /// </summary>
        public bool HaveSupressionEnabled { get; set; } = false;

        /// <summary>
        /// The maximum number of concurrent open connections overall. Defaults to 150.
        /// </summary>
        public int MaximumConnections { get; set; } = 150;

        /// <summary>
        /// The maximum number of concurrent connection attempts overall. Defaults to 5.
        /// </summary>
        public int MaximumHalfOpenConnections { get; set; } = 5;

        /// <summary>
        /// The maximum download speed, in bytes per second, overall. A value of 0 means unlimited. Defaults to 0.
        /// </summary>
        public int MaximumDownloadSpeed { get; set; } = 0;

        /// <summary>
        /// The maximum upload speed, in bytes per second, overall. A value of 0 means unlimited. defaults to 0.
        /// </summary>
        public int MaximumUploadSpeed { get; set; } = 0;
        
        /// <summary>
        /// The TCP port the engine should listen on for incoming connections. Defaults to 52138.
        /// </summary>
        public int ListenPort { get; set; } = 52138;

        /// <summary>
        /// The maximum number of files which can be opened concurrently. On platforms which limit the maximum
        /// filehandles for a process it can be beneficial to limit the number of open files to prevent
        /// running out of resources. Defaults to 20.
        /// </summary>
        public int MaximumOpenFiles { get; set; } = 20;

        /// <summary>
        /// The maximum disk read speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the hashing process from saturating
        /// the available drive bandwidth. Defaults to 0.
        /// </summary>
        public int MaximumDiskReadRate { get; set; } = 0;

        /// <summary>
        /// The maximum disk write speed, in bytes per second. A value of 0 means unlimited. This is
        /// typically only useful for non-SSD drives to prevent the downloading process from saturating
        /// the available drive bandwidth. If the download speed exceeds the max write rate then the
        /// download will be throttled. Defaults to 0.
        /// </summary>
        public int MaximumDiskWriteRate { get; set; } = 0;

        /// <summary>
        /// If the IPAddress incoming peer connections are received on differs from the IPAddress the tracker
        /// Announce or Scrape requests are sent from, specify it here. Typically this should not be set.
        /// Defaults to <see langword="null" />
        /// </summary>
        public IPEndPoint ReportedAddress { get; set; } = null;

        /// <summary>
        /// If this is set to false and <see cref="AllowedEncryption"/> allows <see cref="EncryptionTypes.PlainText"/>, then
        /// unencrypted connections will be used by default for new outgoing connections. Otherwise, if <see cref="AllowedEncryption"/>
        /// allows <see cref="EncryptionTypes.RC4Full"/> or <see cref="EncryptionTypes.RC4Header"/> then an encrypted connection
        /// will be used by default for new outgoing connections. Defaults to <see langword="true" />.
        /// </summary>
        public bool PreferEncryption { get; set; }

        /// <summary>
        /// This is the path where the .torrent metadata will be saved when magnet links are used to start a download.
        /// Defaults to <see langword="null" />
        /// </summary>
        public string SavePath { get; set; } = null;

        public EngineSettings()
        {

        }

        public EngineSettings(string savePath, int listenPort)
        {
            SavePath = savePath;
            ListenPort = listenPort;
        }

        public EngineSettings(string savePath, int listenPort, int maximumConnections)
            : this(savePath, listenPort)
        {
            MaximumConnections = maximumConnections;
        }

        public EngineSettings(string savePath, int listenPort, int maximumConnections, int maximumHalfOpenConnections)
            : this(savePath, listenPort, maximumConnections)
        {
            MaximumHalfOpenConnections = maximumHalfOpenConnections;
        }

        public EngineSettings(string savePath, int listenPort, int maximumConnections, int maximumHalfOpenConnections, int maximumDownloadSpeed, int maximumUploadSpeed, EncryptionTypes allowedEncryption)
            : this(savePath, listenPort, maximumConnections, maximumHalfOpenConnections)
        {
            MaximumDownloadSpeed = maximumDownloadSpeed;
            MaximumUploadSpeed = maximumUploadSpeed;
            AllowedEncryption = allowedEncryption;
        }

        object ICloneable.Clone()
            => Clone();

        public EngineSettings Clone()
            => (EngineSettings)MemberwiseClone();

        public override bool Equals(object obj)
        {
            EngineSettings settings = obj as EngineSettings;
            return settings != null
                && AllowedEncryption          == settings.AllowedEncryption
                && HaveSupressionEnabled      == settings.HaveSupressionEnabled
                && ListenPort                 == settings.ListenPort
                && MaximumConnections         == settings.MaximumConnections
                && MaximumDiskReadRate        == settings.MaximumDiskReadRate
                && MaximumDiskWriteRate       == settings.MaximumDiskWriteRate
                && MaximumDownloadSpeed       == settings.MaximumDownloadSpeed
                && MaximumHalfOpenConnections == settings.MaximumHalfOpenConnections
                && MaximumOpenFiles           == settings.MaximumOpenFiles
                && MaximumUploadSpeed         == settings.MaximumUploadSpeed
                && PreferEncryption           == settings.PreferEncryption
                && ReportedAddress            == settings.ReportedAddress
                && SavePath                   == settings.SavePath;
        }

        public override int GetHashCode()
        {
            return MaximumConnections +
                   MaximumDownloadSpeed +
                   MaximumUploadSpeed +
                   MaximumHalfOpenConnections +
                   ListenPort.GetHashCode() +
                   AllowedEncryption.GetHashCode() +
                   SavePath.GetHashCode();
        }
    }
}