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
    /// <summary>
    /// Class representing the "Settings" for individual torrents
    /// </summary>
    public class TorrentSettings
    {
        #region Member Variables
        /// <summary>
        /// The maximum download speed for the torrent in kB/sec
        /// </summary>
        public int MaxDownloadSpeed
        {
            get { return this.maxDownloadSpeed; }
            set { this.maxDownloadSpeed = value; }
        }
        private int maxDownloadSpeed;


        /// <summary>
        /// The maximum upload speed for the torrent in kB/sec
        /// </summary>
        public int MaxUploadSpeed
        {
            get { return this.maxUploadSpeed; }
            set { this.maxUploadSpeed = value; }
        }
        private int maxUploadSpeed;


        /// <summary>
        /// The maximum simultaneous open connections for the torrent
        /// </summary>
        public int MaxConnections
        {
            get { return this.maxConnections; }
            set { this.maxConnections = value; }
        }
        private int maxConnections;


        /// <summary>
        /// The number of upload slots for the torrent
        /// </summary>
        public int UploadSlots
        {
            get { return this.uploadSlots; }
            set { this.uploadSlots = value; }
        }
        private int uploadSlots;
        #endregion


        #region Constructors
        private TorrentSettings()
        {
        }


        /// <summary>
        /// Creates a new TorrentSettings
        /// </summary>
        /// <param name="settings">The settings to clone</param>
        public TorrentSettings(TorrentSettings settings)
        {
            this.uploadSlots = settings.uploadSlots;
            this.maxUploadSpeed = settings.maxUploadSpeed;
            this.maxDownloadSpeed = settings.maxDownloadSpeed;
            this.maxConnections = settings.maxConnections;
        }


        /// <summary>
        /// Creates a new TorrentSettings with the specified number of upload slots and with
        /// default settings for everything else
        /// </summary>
        /// <param name="uploadSlots">The number of upload slots for this torrent</param>
        public TorrentSettings(int uploadSlots)
            : this(uploadSlots, DefaultSettings.maxConnections, DefaultSettings.maxDownloadSpeed, DefaultSettings.maxUploadSpeed)
        {
        }


        /// <summary>
        /// Creates a new TorrentSettings with the specified number of uploadSlots and max connections and
        /// default settings for everything else
        /// </summary>
        /// <param name="uploadSlots">The number of upload slots for this torrent</param>
        /// <param name="maxConnections">The maximum number of simultaneous open connections for this torrent</param>
        public TorrentSettings(int uploadSlots, int maxConnections)
            : this(uploadSlots, maxConnections, DefaultSettings.maxDownloadSpeed, DefaultSettings.maxUploadSpeed)
        {
        }


        /// <summary>
        /// Creates a new TorrentSettings with the specified settings
        /// </summary>
        /// <param name="uploadSlots">The number of upload slots for this torrent</param>
        /// <param name="maxConnections">The maximum number of simultaneous open connections for this torrent</param>
        /// <param name="maxDownloadSpeed">The maximum download speed for this torrent</param>
        /// <param name="maxUploadSpeed">The maximum upload speed for this torrent</param>
        public TorrentSettings(int uploadSlots, int maxConnections, int maxDownloadSpeed, int maxUploadSpeed)
        {
            this.uploadSlots = uploadSlots;
            this.maxDownloadSpeed = maxDownloadSpeed;
            this.maxUploadSpeed = maxUploadSpeed;
            this.maxConnections = maxConnections;
        }
        #endregion


        #region Default Settings
        /// <summary>
        /// These are hardcoded default settings. They should be good enough
        /// for connections with upload between 128kB/sec and 512kB/sec
        /// </summary>
        public static TorrentSettings DefaultSettings
        {
            get
            {
                TorrentSettings sett = new TorrentSettings();
                sett.maxConnections = 50;
                sett.maxDownloadSpeed = 0;
                sett.maxUploadSpeed = 0;
                sett.uploadSlots = 4;
                return sett;
            }
        }
        #endregion
    }
}