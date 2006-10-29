/*
 * $Id: WindowController.cs 948 2006-08-24 18:28:12Z piotr $
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
using System.IO;
using System.Reflection;

using Gtk;

using MonoTorrent.Common;
using MonoTorrent.Client;

using MonoTorrent.Interface;
using MonoTorrent.Interface.Helpers;
using MonoTorrent.Interface.Settings;
using MonoTorrent.Interface.View;

namespace MonoTorrent.Interface.Controller
{
    public class WindowController
    {
        private Configuration config;
        private MainWindow window;

        public WindowController(MainWindow window)
        {
            this.window = window;
            this.config = Configuration.Instance;

            InitWindow();
            InitActions();
            InitControllers();
        }

        private void InitWindow()
        {
            window.Resize(config.WindowWidth, config.WindowHeight);
            window.SplitterPosition = config.SplitterPosition;

            window.SizeAllocated += OnResize;
            window.DeleteEvent += OnQuit;
        }

        private void InitActions()
        {
            UIBuilder ui = UIBuilder.Instance;
            ui.SetActionHandler("Quit", OnQuit);
            ui.SetActionHandler("About", OnAbout);
        }

        private void InitControllers()
        {
            ClientEngine clientEngine = new ClientEngine(
                    EngineSettings.DefaultSettings(),
                    TorrentSettings.DefaultSettings());
            new PreferencesController(window, clientEngine.Settings);
            new TorrentsController(window, clientEngine);
        }

        private void OnResize(object sender, SizeAllocatedArgs args)
        {
        }

        private void OnQuit(object sender, EventArgs args)
        {
            config.WindowWidth = window.Allocation.Width;
            config.WindowHeight = window.Allocation.Height;
            config.SplitterPosition = window.SplitterPosition;
            Application.Quit();
        }

        private void OnAbout(object sender, EventArgs args)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AboutDialog dialog = new AboutDialog();

            dialog.Name = AssemblyInfo.Title;
            dialog.Copyright = AssemblyInfo.Copyright;
            dialog.Version = AssemblyInfo.Version;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith("COPYING"))
                    continue;

                using (StreamReader licenseReader = new StreamReader(assembly.GetManifestResourceStream(name)))
                {
                    dialog.License = licenseReader.ReadToEnd();
                    dialog.Run();
                    dialog.Destroy();
                    return;
                }
            }
        }
    }
}
