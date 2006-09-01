//
// ITorrentManager.cs
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
using MonoTorrent.Common;

namespace MonoTorrent.Client
{
    /// <summary>
    /// Interface used to interact with .torrents loaded into the Engine.
    /// </summary>
    public interface ITorrentManager
    {
        /// <summary>
        /// Event fired when new peers are found due to a tracker update
        /// </summary>
        event EventHandler<PeersAddedEventArgs> OnPeersAdded;


        /// <summary>
        /// Event fired every time a piece is hashed
        /// </summary>
        event EventHandler<PieceHashedEventArgs> OnPieceHashed;


        /// /<summary>
        /// Event fired every time the TorrentManager's state changes
        /// </summary>
        event EventHandler<TorrentStateChangedEventArgs> OnTorrentStateChanged;


        /// <summary>
        /// Returns a torrent instance.
        /// </summary>
        ITorrent Torrent { get; }


        /// <summary>
        /// The current state of the torrent.
        /// </summary>
        TorrentState State { get; }


        /// <summary>
        /// The current progress of the torrent in percent.
        /// </summary>
        double Progress();


        /// <summary>
        /// The current download speed in bytes per second
        /// </summary>
        /// <returns></returns>
        double DownloadSpeed();


        /// <summary>
        /// The current upload speed in bytes per second
        /// </summary>
        /// <returns></returns>
        double UploadSpeed();


        /// <summary>
        /// The count of already downloaded bytes.
        /// </summary>
        long BytesDownloaded { get; }


        /// <summary>
        /// The count of already downloaded bytes.
        /// </summary>
        long BytesUploaded { get; }


        /// <summary>
        /// The path to save the files to
        /// </summary>
        string SavePath { get; }


        /// <summary>
        /// The piecemanager associated with this ITorrentManager
        /// </summary>
        IPieceManager PieceManager { get; }

        int AvailablePeers { get; }

        int Seeds();

        int Leechs();
    }
}
