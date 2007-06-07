/*
 * $Id: NewTorrentDialog.cs 880 2006-08-19 22:50:54Z piotr $
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

using Mono.Unix;

using Gtk;

namespace MonoTorrent.Interface.View
{
    public class NewTorrentDialog : DialogWithTitle
    {
        private Table table;

        private FileChooserButton fromPath;

        private Entry tracker;

        private Entry comment;

        private Entry saveTo;

        public NewTorrentDialog(Window window) : base(
                Catalog.GetString("New Torrent"), window)
        {
            InitLayout();
            InitButtons();
            Validate();
            ShowAll();
        }

        public string FromPath {
            get {
                return fromPath.Filename;
            }
            set {
                fromPath.SelectFilename(value);
            }
        }

        public string Tracker {
            get {
                return tracker.Text;
            }
            set {
                tracker.Text = value;
            }
        }

        public string Comment {
            get {
                return comment.Text;
            }
            set {
                comment.Text = value;
            }
        }

        public string SaveTo {
            get {
                return saveTo.Text;
            }
            set {
                saveTo.Text = value;
            }
        }

        private void InitLayout()
        {
            table = new Table(2, 2, false);
            table.BorderWidth = 0;
            table.RowSpacing = 5;
            table.ColumnSpacing = 5;
            Layout.PackStart(table);

            InitFromPath();
            InitTracker();
            InitComment();
            InitSaveTo();
        }

        private void InitFromPath()
        {
            Label label = new Label(Catalog.GetString("From path:"));
            fromPath = new FileChooserButton("From path",
                    FileChooserAction.SelectFolder);

            label.Xalign = 0;
            table.Attach(label, 0, 1, 0, 1);
            table.Attach(fromPath, 1, 2, 0, 1);
        }

        private void InitTracker()
        {
            Label label = new Label(Catalog.GetString("Tracker:"));
            tracker = new Entry("http://");

            label.Xalign = 0;
            table.Attach(label, 0, 1, 1, 2);
            table.Attach(tracker, 1, 2, 1, 2);
        }

        private void InitComment()
        {
            Label label = new Label(Catalog.GetString("Comment:"));
            comment = new Entry();

            label.Xalign = 0;
            table.Attach(label, 0, 1, 2, 3);
            table.Attach(comment, 1, 2, 2, 3);
        }

        private void InitSaveTo()
        {
            Label label = new Label(Catalog.GetString("Save to:"));
            saveTo = new Entry();
            saveTo.Changed += OnSaveToChanged;

            label.Xalign = 0;
            table.Attach(label, 0, 1, 3, 4);
            table.Attach(saveTo, 1, 2, 3, 4);
        }

        private void InitButtons()
        {
            AddButton(Stock.Cancel, ResponseType.Cancel);
            AddButton(Stock.Ok, ResponseType.Ok);
        }

        private void OnSaveToChanged(object sender, EventArgs args)
        {
            Validate();
        }

        private void Validate()
        {
            SetResponseSensitive(ResponseType.Ok, true);
            if (saveTo.Text.Trim().Length == 0) {
                SetResponseSensitive(ResponseType.Ok, false);
            }
        }
    }
}
