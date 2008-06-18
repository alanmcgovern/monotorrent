//
// StatsBox.cs
//
// Authors:
//   Karthik Kailash    karthik.l.kailash@gmail.com
//   David Sanghera     dsanghera@gmail.com
//
// Copyright (C) 2006 Karthik Kailash, David Sanghera
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

#if STATS

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using MonoTorrent.Client;

namespace SampleClient.Stats
{
    internal delegate int ItemAdd(TorrentManager o);

    internal partial class StatsBox : Form
    {
        public event EventHandler<TorrentEventArgs> SelectedTorrent;

        public StatsBox()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Set the text inside the textbox
        /// </summary>
        /// <param name="text"></param>
        public void SetText(String text)
        {
            Utils.PerformControlOperation(this.textBox1, delegate { this.textBox1.Text = text; });
        }


        /// <summary>
        /// Changes the title
        /// </summary>
        /// <param name="manager"></param>
        public void SetTorrent(TorrentManager manager)
        {
            Utils.PerformControlOperation(this, new NoParam(delegate { this.Text = "StatsBox: " + manager.Torrent.Name; }));
        }


        /// <summary>
        /// Clear the statsBox
        /// </summary>
        public void Clear()
        {
            Utils.PerformControlOperation(this.textBox1, new NoParam(this.textBox1.Clear));
        }


        /// <summary>
        /// Report the addition of a torrent to the ClientEngine
        /// </summary>
        /// <param name="torrent"></param>
        public void TorrentAdded(TorrentManager torrent)
        {
            Utils.PerformControlOperation(this.comboBox1, delegate { this.comboBox1.Items.Add(torrent);});
        }


        /// <summary>
        /// Report the removal of a torrent from the ClientEngine
        /// </summary>
        /// <param name="torrent"></param>
        public void TorrentRemoved(TorrentManager torrent)
        {
            Utils.PerformControlOperation(this.comboBox1, delegate { TorrentRemovedInvoke(torrent); });
        }


        private void TorrentRemovedInvoke(TorrentManager torrent)
        {
            for (int i = 0; i < this.comboBox1.Items.Count; i++)
            {
                object o = this.comboBox1.Items[i];
                if (o == torrent)
                    this.comboBox1.Items.Remove(o);
            }
        }


        private void comboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            if (SelectedTorrent != null)
            {
                Utils.PerformControlOperation(this.comboBox1, delegate
                        {
                            SelectedTorrent(this, new TorrentEventArgs((TorrentManager)this.comboBox1.SelectedItem));
                        });
            }
        }
    }
}

#endif