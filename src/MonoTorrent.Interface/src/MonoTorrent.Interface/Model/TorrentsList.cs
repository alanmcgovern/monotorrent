/*
 * $Id: TorrentsList.cs 931 2006-08-22 17:45:59Z piotr $
 * Copyright (c) 2006 by Piotr Wolny <gildur@gmail.com>
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;

using Gtk;

using MonoTorrent.Client;

using MonoTorrent.Interface.Helpers;

namespace MonoTorrent.Interface.Model
{
    public class TorrentsList : ListStore
    {
        public event EventHandler TorrentStateChanged;

        private Dictionary<TreeIter, ITorrentManager> rowsToTorrents;

        public TorrentsList() :  base(
                typeof(string), typeof(string), typeof(string), 
                typeof(string), typeof(string), typeof(string),
                typeof(string), typeof(string), typeof(string),
                typeof(string), typeof(string))
        {
            this.rowsToTorrents = new Dictionary<TreeIter, ITorrentManager>();
            ClientEngine.connectionManager.OnPeerConnectionChanged
                    += OnPeerChange;
            ClientEngine.connectionManager.OnPeerMessages += OnPeerChange;
        }

        public TreeIter AddTorrent(ITorrentManager torrent)
        {
            TreeIter row = Append();
            UpdateRow(row, torrent);
            rowsToTorrents.Add(row, torrent);
            torrent.OnPieceHashed += OnTorrentChange;
            torrent.OnPeersAdded += OnTorrentChange;
            torrent.OnTorrentStateChanged += OnTorrentStateChange;
            torrent.PieceManager.OnPieceChanged += OnTorrentChange;
            return row;
        }

        public bool RemoveTorrent(ref TreeIter row)
        {
            rowsToTorrents[row].PieceManager.OnPieceChanged -= OnTorrentChange;
            rowsToTorrents[row].OnPeersAdded -= OnTorrentChange;
            rowsToTorrents[row].OnPieceHashed -= OnTorrentChange;
            rowsToTorrents.Remove(row);
            return Remove(ref row);
        }

        public ITorrentManager GetTorrent(TreeIter row)
        {
            return rowsToTorrents[row];
        }

        private void OnTorrentChange(object sender, EventArgs args)
        {
            Application.Invoke(sender, args, OnTorrentChangeSync);
        }

        private void OnTorrentChangeSync(object sender, EventArgs args)
        {
            ITorrentManager torrent = (ITorrentManager) sender;
            UpdateStats(FindRow(torrent), torrent);
        }

        private void OnTorrentStateChange(object sender, EventArgs args)
        {
            Application.Invoke(sender, args, OnTorrentStateChangeSync);
        }

        private void OnTorrentStateChangeSync(object sender, EventArgs args)
        {
            ITorrentManager torrent = (ITorrentManager) sender;
            UpdateState(FindRow(torrent), torrent);
        }

        private void OnPeerChange(object sender, EventArgs args)
        {
            Application.Invoke(sender, args, OnPeerChangeSync);
        }

        private void OnPeerChangeSync(object sender, EventArgs args)
        {
            IPeerConnectionID connection = (IPeerConnectionID) sender;
            ITorrentManager torrent = connection.TorrentManager;
            UpdateStats(FindRow(torrent), torrent);
        }

        private TreeIter FindRow(ITorrentManager torrent)
        {
            foreach (TreeIter row in rowsToTorrents.Keys) {
                if (rowsToTorrents[row] == torrent) {
                    return row;
                }
            }
            return TreeIter.Zero;
        }

        private void UpdateRow(TreeIter row, ITorrentManager torrent)
        {
            UpdateState(row, torrent);
            SetValue(row, 1, torrent.Torrent.Name);
            SetValue(row, 2, Formatter.FormatSize(torrent.Torrent.Size));
            UpdateStats(row, torrent);
        }

        private void UpdateStats(TreeIter row, ITorrentManager torrent)
        {
            SetValue(row, 3, Formatter.FormatPercent(torrent.Progress()));
            SetValue(row, 4, Formatter.FormatSize(torrent.BytesDownloaded));
            SetValue(row, 5, Formatter.FormatSize(torrent.BytesUploaded));
            SetValue(row, 6, Formatter.FormatSpeed(torrent.DownloadSpeed()));
            SetValue(row, 7, Formatter.FormatSpeed(torrent.UploadSpeed()));
            SetValue(row, 9, torrent.Leechs().ToString());
            SetValue(row, 8, torrent.Seeds().ToString());
            SetValue(row, 10, torrent.AvailablePeers.ToString());
        }

        private void UpdateState(TreeIter row, ITorrentManager torrent)
        {
            SetValue(row, 0, torrent.State.ToString());
            FireTorrentStateChanged();
        }

        private void FireTorrentStateChanged()
        {
            if (TorrentStateChanged != null) {
                TorrentStateChanged(this, EventArgs.Empty);
            }
        }
    }
}
