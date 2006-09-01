/*
 * $Id: PreferencesController.cs 880 2006-08-19 22:50:54Z piotr $
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

using MonoTorrent.Client;

using MonoTorrent.Interface.Settings;
using MonoTorrent.Interface.View;

namespace MonoTorrent.Interface.Controller
{
    public class PreferencesController
    {
        private MainWindow window;

        private Configuration config;

        private IEngineSettings engineSettings;

        public PreferencesController(MainWindow window,
                IEngineSettings engineSettings)
        {
            this.window = window;
            this.engineSettings = engineSettings;
            this.config = Configuration.Instance;
            RetrievePreferences();
            InitActions();
        }

        private void InitActions()
        {
            UIBuilder ui = UIBuilder.Instance;
            ui.SetActionHandler(ActionConstants.PREFERENCES, OnPreferences);
            ui.SetActionHandler(ActionConstants.TOOLBAR, OnToolbar);
            ui.SetActionHandler(ActionConstants.DETAILS, OnDetails);
        }

        private void OnPreferences(object sender, EventArgs args)
        {
            PreferencesDialog dialog = new PreferencesDialog(window);
            dialog.DownloadPath = engineSettings.DefaultSavePath;
            dialog.ListenPort = engineSettings.ListenPort;
            dialog.MaxDlSpeed = engineSettings.GlobalMaxDownloadSpeed;
            dialog.MaxUlSpeed = engineSettings.GlobalMaxUploadSpeed;
            
            if (dialog.Run() == (int) ResponseType.Ok) {
                engineSettings.DefaultSavePath = dialog.DownloadPath;
                engineSettings.ListenPort = dialog.ListenPort;
                engineSettings.GlobalMaxDownloadSpeed = dialog.MaxDlSpeed;
                engineSettings.GlobalMaxUploadSpeed = dialog.MaxUlSpeed;
                StorePreferences();
            }
            dialog.Destroy();
        }

        private void RetrievePreferences()
        {
            engineSettings.DefaultSavePath = config.DownloadPath;
            engineSettings.ListenPort = config.ListenPort;
            engineSettings.GlobalMaxDownloadSpeed = config.MaximumDownloadSpeed;
            engineSettings.GlobalMaxUploadSpeed = config.MaximumUploadSpeed;
            UIBuilder.Instance.MainToolbar.Visible = config.DisplayToolbar;
            window.DisplayDetails = config.DisplayDetails;
        }

        private void StorePreferences()
        {
            config.DownloadPath = engineSettings.DefaultSavePath;
            config.ListenPort = engineSettings.ListenPort;
            config.MaximumDownloadSpeed = engineSettings.GlobalMaxDownloadSpeed;
            config.MaximumUploadSpeed = engineSettings.GlobalMaxUploadSpeed;
            config.DisplayToolbar = UIBuilder.Instance.MainToolbar.Visible;
            config.DisplayDetails = window.DisplayDetails;
        }

        private void OnToolbar(object sender, EventArgs args)
        {
            Toolbar toolbar = UIBuilder.Instance.MainToolbar;
            toolbar.Visible = !toolbar.Visible;
            StorePreferences();
        }

        private void OnDetails(object sender, EventArgs args)
        {
            window.DisplayDetails = !window.DisplayDetails;
            StorePreferences();
        }
    }
}
