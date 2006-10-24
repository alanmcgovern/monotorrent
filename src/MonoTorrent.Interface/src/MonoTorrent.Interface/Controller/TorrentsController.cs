/*
 * $Id: TorrentsController.cs 910 2006-08-20 19:44:02Z piotr $
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

using Gtk;

using MonoTorrent.Common;
using MonoTorrent.Client;

using MonoTorrent.Interface.Model;
using MonoTorrent.Interface.View;

namespace MonoTorrent.Interface.Controller
{
    public class TorrentsController
    {
        private Window window;

        private TorrentsRepository repository;

        private TorrentsView torrentsView;

        private TorrentView torrentView;

        private ClientEngine clientEngine;

        private TorrentsList torrents;

        public TorrentsController(MainWindow window, ClientEngine clientEngine)
        {
            this.torrents = new TorrentsList();
            this.window = window;
            this.torrentsView = window.TorrentsView;
            this.torrentView = window.TorrentView;
            this.clientEngine = clientEngine;
            this.repository =
                    new TorrentsRepository(PathConstants.TORRENTS_DIR);
            ParseCommandLine();
            InitTorrents();
            InitActions();
            InitView();
            UpdateSensitive();
        }

        private void ParseCommandLine()
        {
            string[] paths = Environment.GetCommandLineArgs();
            for (int i = 1; i < paths.Length; i++) {
                try {
                    repository.Add(paths[i]);
                } catch (Exception exception) {
                    Dialog errorDialog = new MessageDialog(window,
                            DialogFlags.Modal, MessageType.Error,
                            ButtonsType.Ok,  exception.Message);
                    errorDialog.Run();
                    errorDialog.Destroy();
                }
            }
        }

        private void InitTorrents()
        {
            foreach (string path in repository.GetPaths()) {
                try {
                    torrents.AddTorrent(clientEngine.LoadTorrent(path));
                } catch (Exception exception) {
                    System.Console.WriteLine(exception);
                    Dialog errorDialog = new MessageDialog(window,
                            DialogFlags.Modal, MessageType.Error,
                            ButtonsType.Ok,  exception.Message);
                    errorDialog.Run();
                    errorDialog.Destroy();
                }
            }
            torrents.TorrentStateChanged += OnTorrentStateChanged;
        }

        private void InitActions()
        {
            UIBuilder ui = UIBuilder.Instance;
            ui.SetActionHandler(ActionConstants.NEW, OnNew);
            ui.SetActionHandler(ActionConstants.OPEN, OnOpen);
            ui.SetActionHandler(ActionConstants.START, OnStart);
            ui.SetActionHandler(ActionConstants.PAUSE, OnPause);
            ui.SetActionHandler(ActionConstants.STOP, OnStop);
            ui.SetActionHandler(ActionConstants.DELETE, OnDelete);
        }

        private void OnNew(object sender, EventArgs args)
        {
            NewTorrentDialog dialog = new NewTorrentDialog(window);
            if ((ResponseType) dialog.Run() == ResponseType.Ok) {
                try {
                    TorrentCreator creator = new TorrentCreator();
                    creator.Path = dialog.FromPath;
                    creator.Comment = dialog.Comment;
                    creator.AddAnnounce(dialog.Tracker);
                    creator.Create(dialog.SaveTo);
                } catch (Exception exception) {
                    Dialog errorDialog = new MessageDialog(window,
                            DialogFlags.Modal, MessageType.Error,
                            ButtonsType.Ok,  exception.Message);
                    errorDialog.Run();
                    errorDialog.Destroy();
                }
            }
            dialog.Destroy();
        }

        private void OnOpen(object sender, EventArgs args)
        {
            FileChooserDialog dialog = new TorrentChooserDialog(window);
            if ((ResponseType) dialog.Run() == ResponseType.Ok) {
                try {
                    string path = repository.Add(dialog.Filename);
                    TorrentManager torrent = clientEngine.LoadTorrent(path);
                    TreeIter row = torrents.AddTorrent(torrent);
                    torrentsView.Selection.SelectIter(row);
                } catch (Exception exception) {
                    Dialog errorDialog = new MessageDialog(window,
                            DialogFlags.Modal, MessageType.Error,
                            ButtonsType.Ok, exception.Message);
                    errorDialog.Run();
                    errorDialog.Destroy();
                }
            }
            dialog.Destroy();
        }

        private void OnStart(object sender, EventArgs args)
        {
            try {
                clientEngine.Start(GetSelectedTorrent());
            } catch (Exception exception) {
                Dialog errorDialog = new MessageDialog(window,
                        DialogFlags.Modal, MessageType.Error,
                        ButtonsType.Ok, exception.Message);
                errorDialog.Run();
                errorDialog.Destroy();
            }
        }

        private void OnPause(object sender, EventArgs args)
        {
            clientEngine.Pause(GetSelectedTorrent());
        }

        private void OnStop(object sender, EventArgs args)
        {
            clientEngine.Stop(GetSelectedTorrent());
        }

        private void OnDelete(object sender, EventArgs args)
        {
            Dialog dialog;
            TreeIter row;
            TorrentManager torrent;

            torrentsView.Selection.GetSelected(out row);
            torrent = torrents.GetTorrent(row);

            dialog = new MessageDialog(this.window, DialogFlags.Modal,
                    MessageType.Question, ButtonsType.OkCancel,
                    string.Format("Do you really want to delete torrent: {0}?",
                            torrent.Torrent.Name));
            if ((ResponseType) dialog.Run() == ResponseType.Ok) {
                repository.Remove(torrent.Torrent.TorrentPath);
                torrents.RemoveTorrent(ref row);
            }
            dialog.Destroy();
        }

        private void InitView()
        {
            UIBuilder ui = UIBuilder.Instance;
            torrentsView.Model = torrents;
            torrentsView.Selection.Changed += OnSelectionChanged;
            torrentsView.Popup = ui.TorrentPopup;
        }

        private void OnSelectionChanged(object sender, EventArgs args)
        {
            UpdateSensitive();
            UpdateTorrent();
        }

        private void OnTorrentStateChanged(object sender, EventArgs args)
        {
            UpdateSensitive();
        }

        private void UpdateSensitive()
        {
            UIBuilder ui = UIBuilder.Instance;
            ui.SetSensitive(ActionConstants.DELETE, false);
            ui.SetSensitive(ActionConstants.START, false);
            ui.SetSensitive(ActionConstants.STOP, false);
            ui.SetSensitive(ActionConstants.PAUSE, false);
            if (torrentsView.Selection.CountSelectedRows() > 0) {
                TorrentManager torrent = GetSelectedTorrent();

                ui.SetSensitive(ActionConstants.DELETE, true);
                if (torrent.State == TorrentState.Stopped
                        || torrent.State == TorrentState.Paused) {
                    ui.SetSensitive(ActionConstants.START, true);
                }
                if (torrent.State != TorrentState.Stopped) {
                    ui.SetSensitive(ActionConstants.STOP, true);
                }
                if (torrent.State != TorrentState.Paused
                        && torrent.State != TorrentState.Stopped) {
                    ui.SetSensitive(ActionConstants.PAUSE, true);
                }
            }
        }

        private void UpdateTorrent()
        {
            if (torrentsView.Selection.CountSelectedRows() > 0) {
                torrentView.Model = GetSelectedTorrent().Torrent;
            } else {
                torrentView.Model = null;
            }
        }

        private TorrentManager GetSelectedTorrent()
        {
            TreeIter row;
            torrentsView.Selection.GetSelected(out row);
            return torrents.GetTorrent(row);
        }
    }
}
