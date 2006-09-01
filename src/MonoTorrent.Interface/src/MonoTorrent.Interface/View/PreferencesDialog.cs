/*
 * $Id: PreferencesDialog.cs 880 2006-08-19 22:50:54Z piotr $
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

using Mono.Unix;

using Gtk;

namespace MonoTorrent.Interface.View
{
    public class PreferencesDialog : DialogWithTitle
    {
        private Table table;

        private FileChooserButton downloadPath;

        private SpinButton listenPort;

        private SpinButton maxDlSpeed;

        private SpinButton maxUlSpeed;

        public PreferencesDialog(Window window) : base(
                Catalog.GetString("Preferences"), window)
        {
            InitLayout();
            InitButtons();
            ShowAll();
        }

        public string DownloadPath {
            get {
                return downloadPath.Filename;
            }
            set {
                downloadPath.SelectFilename(value);
            }
        }

        public int ListenPort {
            get {
                return (int) listenPort.Value;
            }
            set {
                listenPort.Value = value;
            }
        }

        public int MaxDlSpeed {
            get {
                return (int) maxDlSpeed.Value;
            }
            set {
                maxDlSpeed.Value = value;
            }
        }

        public int MaxUlSpeed {
            get {
                return (int) maxUlSpeed.Value;
            }
            set {
                maxUlSpeed.Value = value;
            }
        }

        private void InitLayout()
        {
            table = new Table(2, 2, false);
            table.BorderWidth = 0;
            table.RowSpacing = 5;
            table.ColumnSpacing = 5;
            Layout.PackStart(table);

            InitDownloadPath();
            InitListenPort();
            InitDlSpeed();
            InitUlSpeed();
        }

        private void InitDownloadPath()
        {
            Label label = new Label(Catalog.GetString("Download path:"));
            downloadPath = new FileChooserButton("Download path", 
                    FileChooserAction.SelectFolder);

            label.Xalign = 0;
            table.Attach(label, 0, 1, 0, 1);
            table.Attach(downloadPath, 1, 2, 0, 1);
        }

        private void InitListenPort()
        {
            Label label = new Label(Catalog.GetString("Listen port:"));
            listenPort = new SpinButton(0, int.MaxValue, 1);

            label.Xalign = 0;
            table.Attach(label, 0, 1, 1, 2);
            table.Attach(listenPort, 1, 2, 1, 2);
        }

        private void InitDlSpeed()
        {
            Label label = new Label(
                    Catalog.GetString("Maximum download speed:"));
            maxDlSpeed = new SpinButton(0, int.MaxValue, 1);

            label.Xalign = 0;
            table.Attach(label, 0, 1, 2, 3);
            table.Attach(maxDlSpeed, 1, 2, 2, 3);
        }

        private void InitUlSpeed()
        {
            Label label = new Label(
                    Catalog.GetString("Maximum upload speed:"));
            maxUlSpeed = new SpinButton(0, int.MaxValue, 1);

            label.Xalign = 0;
            table.Attach(label, 0, 1, 3, 4);
            table.Attach(maxUlSpeed, 1, 2, 3, 4);
        }

        private void InitButtons()
        {
            AddButton(Stock.Cancel, ResponseType.Cancel);
            AddButton(Stock.Ok, ResponseType.Ok);
        }
    }
}
