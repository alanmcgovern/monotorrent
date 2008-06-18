//
// PeerMessagesDisplay.cs
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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using MonoTorrent.Client.Messages;

using log4net.Appender;

namespace SampleClient.Stats
{
    public partial class PeerMessagesDisplay : Form
    {
        private String title;
        private String logFile;

        private bool disposed;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="title"></param>
        /// <param name="logFilePath"></param>
        public PeerMessagesDisplay(String title, String logFilePath)
        {
            InitializeComponent();

            this.title = title;
            this.logFile = logFilePath;

            this.Show();
        }


        /// <summary>
        /// Load the messages from the log file into the window
        /// </summary>
        public void LoadLog()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Utils.PerformControlOperation(this, delegate { this.Text = title; });

                    using (FileStream fs = File.OpenRead(this.logFile))
                    {
                        using (StreamReader reader = new StreamReader(fs))
                        {
                            String line;
                            while ((line = reader.ReadLine()) != null)
                                AddNewMessage(line);
                        }
                    }
                }
                catch (IOException ioe)
                {
                    AddNewMessage(ioe.Message);
                }
            });
        }


        /// <summary>
        /// Append message to the bottom of the text window
        /// </summary>
        /// <param name="message"></param>
        public void AddNewMessage(String message)
        {
            lock (this)
            {
                int charsToRemove = 0;

                for (int i = message.Length - 1; i >= 0; i--)
                {
                    if (message[i] == '\r' || message[i] == '\n')
                        charsToRemove++;
                }

                if (this.disposed)
                    throw new ObjectDisposedException("PeerMessagesDisplay");

                Utils.PerformControlOperation(this.listBox1, delegate {
                    AddNewMessageInvoke(charsToRemove == 0 ? message : message.Substring(0, message.Length - charsToRemove));
                });
            }
        }


        /// <summary>
        /// Add a new item to the bottom of the listbox.
        /// If the listbox is already at the bottom, we do a tailing behavior and move the scroll view down to see the new message.
        /// Otherwise, we leave the scroll where it is, because we're probably looking at older messages and don't want to have to
        /// keep scrolling up to them.
        /// </summary>
        /// <param name="message"></param>
        private void AddNewMessageInvoke(String message)
        {
            // if the bottom item shown is the bottom index, don't reset the top index
            int bottomIndex = listBox1.IndexFromPoint(listBox1.Bounds.Left + 10, listBox1.Bounds.Bottom - 10);
            bool shouldTail = (bottomIndex == this.listBox1.Items.Count - 1);
            int index = this.listBox1.TopIndex;

            this.listBox1.Items.Add(message);

            if (shouldTail)
                this.listBox1.TopIndex = index + 1;
        }
    }
}

#endif