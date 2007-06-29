/*
 * $Id: MainWindow.cs 884 2006-08-20 11:17:54Z piotr $
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
    public class MainWindow : Window
    {
        private Box mainView;

        private Paned paned;

        private ScrolledWindow torrentsScrolled;

        private TorrentsView torrentsView;

        private TorrentView torrentView;

        private bool displayDetails;

        public MainWindow() : base(Catalog.GetString("MonoTorrent"))
        {
            Init();
        }

        public int SplitterPosition {
            get {
                return paned.Position;
            }
            set {
                paned.Position = value;
            }
        }

        public TorrentsView TorrentsView {
            get {
                return torrentsView;
            }
        }

        public TorrentView TorrentView {
            get {
                return torrentView;
            }
        }

        public bool DisplayDetails {
            get {
                return displayDetails;
            }
            set {
                if (value) {
                    ShowDetails();
                } else {
                    HideDetails();
                }
            }
        }

        private void Init()
        {
            InitTorrentsView();
            InitTorrentView();
            paned = new VPaned();
            mainView = new HBox();
            InitBox();
            ShowDetails();
        }

        private void InitTorrentsView()
        {
            torrentsView = new TorrentsView();
            torrentsScrolled = new ScrolledWindow();
            torrentsScrolled.ShadowType = ShadowType.In;
            torrentsScrolled.Add(torrentsView);
        }

        private void InitTorrentView()
        {
            torrentView = new TorrentView();
        }

        private void InitBox()
        {
            UIBuilder ui = UIBuilder.Instance;
            Box box = new VBox();
            Toolbar toolbar = ui.MainToolbar;

            AddAccelGroup(ui.Accelerators);
            toolbar.ToolbarStyle = ToolbarStyle.Icons;

            box.PackStart(ui.MainMenu, false, false, 0);
            box.PackStart(toolbar, false, false, 0);
            box.PackStart(mainView);
            box.PackStart(new MonoTorrentStatusbar(), false, false, 0);

            Child = box;
        }

        private void ShowDetails()
        {
            displayDetails = true;
            CleanContainer(paned);
            CleanContainer(mainView);
            paned.Add1(torrentsScrolled);
            paned.Add2(torrentView);
            mainView.PackStart(paned);
        }

        private void HideDetails()
        {
            displayDetails = false;
            CleanContainer(paned);
            CleanContainer(mainView);
            mainView.PackStart(torrentsScrolled);
        }

        private void CleanContainer(Container container)
        {
            foreach (Widget widget in container.AllChildren) {
                container.Remove(widget);
            }
        }
    }
}
