/*
 * $Id: UIBuilder.cs 885 2006-08-20 11:28:37Z piotr $
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

using MonoTorrent.Interface.Settings;

namespace MonoTorrent.Interface.View
{
    public class UIBuilder
    {
        private static UIBuilder instance;
        
        public static UIBuilder Instance {
            get {
                if (instance == null) {
                    instance = new UIBuilder();
                }
                return instance;
            }
        }
        
        private readonly UIManager ui;

        private readonly ActionGroup actions;

        private UIBuilder()
        {
            ui = new UIManager();
            actions = new ActionGroup("actions");
            
            ui.AddUiFromResource("ui.xml");
            InitActions();
        }

        public AccelGroup Accelerators {
            get {
                return ui.AccelGroup;
            }
        }

        public MenuBar MainMenu {
            get {
                return (MenuBar) ui.GetWidget("/ui/menubar");
            }
        }

        public Toolbar MainToolbar {
            get {
                return (Toolbar) ui.GetWidget("/ui/toolbar");
            }
        }

        public Menu TorrentPopup {
            get {
                return (Menu) ui.GetWidget("/ui/TorrentPopup");
            }
        }

        private void InitActions()
        {
            ActionEntry[] actionEntries = new ActionEntry[] {
                new ActionEntry(ActionConstants.FILE, null,
                        Catalog.GetString("_File"), null,
                        Catalog.GetString("File"), null),
                new ActionEntry(ActionConstants.NEW, Stock.New,
                        Catalog.GetString("_New..."), "<Control>n",
                        Catalog.GetString("New..."), null),
                new ActionEntry(ActionConstants.OPEN, Stock.Open,
                        Catalog.GetString("_Open..."), "<Control>o",
                        Catalog.GetString("Open..."), null),
                new ActionEntry(ActionConstants.QUIT, Stock.Quit,
                        Catalog.GetString("_Quit"), "<Control>q",
                        Catalog.GetString("Quit"), null),

                new ActionEntry(ActionConstants.EDIT, null,
                        Catalog.GetString("_Edit"), null,
                        Catalog.GetString("Edit"), null),
                new ActionEntry(ActionConstants.PREFERENCES, Stock.Preferences,
                        Catalog.GetString("_Preferences"), "<Control>p",
                        Catalog.GetString("Preferences"), null),

                new ActionEntry(ActionConstants.VIEW, null,
                        Catalog.GetString("_View"), null,
                        Catalog.GetString("View"), null),

                new ActionEntry(ActionConstants.ACTION, null,
                        Catalog.GetString("_Action"), null,
                        Catalog.GetString("Action"), null),
                new ActionEntry(ActionConstants.START, Stock.MediaPlay,
                        Catalog.GetString("_Start"), null,
                        Catalog.GetString("Start"), null),
                new ActionEntry(ActionConstants.PAUSE, Stock.MediaPause,
                        Catalog.GetString("_Pause"), null,
                        Catalog.GetString("Pause"), null),
                new ActionEntry(ActionConstants.STOP, Stock.MediaStop,
                        Catalog.GetString("S_top"), null,
                        Catalog.GetString("Stop"), null),
                new ActionEntry(ActionConstants.DELETE, Stock.Delete,
                        Catalog.GetString("_Delete"), null,
                        Catalog.GetString("Delete"), null),

                new ActionEntry(ActionConstants.HELP, null, 
                        Catalog.GetString("_Help"), null,
                        Catalog.GetString("Help"), null),
                new ActionEntry(ActionConstants.ABOUT, Stock.About,
                        Catalog.GetString("_About"), null,
                        Catalog.GetString("About"), null)
            };
            ToggleActionEntry[] toggleActionEntries = new ToggleActionEntry[] {
                new ToggleActionEntry(ActionConstants.TOOLBAR, null,
                        Catalog.GetString("_Toolbar"), null, null, null,
                        Configuration.Instance.DisplayToolbar),
                new ToggleActionEntry(ActionConstants.DETAILS, null,
                        Catalog.GetString("_Details"), null, null, null,
                        Configuration.Instance.DisplayDetails),
            };

            actions.Add(actionEntries);
            actions.Add(toggleActionEntries);
            ui.InsertActionGroup(actions, 0);
        }

        public void SetActionHandler(string actionName,
                EventHandler actionHandler)
        {
            actions.GetAction(actionName).Activated += actionHandler;
        }

        public void SetSensitive(string actionName, bool sensitive)
        {
            actions.GetAction(actionName).Sensitive = sensitive;
        }
    }
}
