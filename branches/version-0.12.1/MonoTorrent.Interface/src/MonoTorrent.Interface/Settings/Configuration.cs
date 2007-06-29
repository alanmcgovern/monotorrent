/*
 * $Id: Configuration.cs 880 2006-08-19 22:50:54Z piotr $
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

using MonoTorrent.Client;

using MonoTorrent.Interface;

namespace MonoTorrent.Interface.Settings
{
    public class Configuration
    {
        private static Configuration instance;

        public static Configuration Instance {
            get {
                if (instance == null) {
                    instance = new Configuration();
                }
                return instance;
            }
        }

        private ISettingsStorage storage;

        private Configuration()
        {
            storage = SettingsStorage.Instance;
        }

        public string DownloadPath {
            get {
                try {
                    return storage.Retrieve("DownloadPath").ToString();
                } catch (Exception) {
                    return PathConstants.DOWNLOAD_DIR;
                }
            }
            set {
                storage.Store("DownloadPath", value);
            }
        }

        public int ListenPort {
            get {
                try {
                    return int.Parse(storage.Retrieve("ListenPort").ToString());
                } catch (Exception) {
                    return EngineSettings.DefaultSettings().ListenPort;
                }
            }
            set {
                storage.Store("ListenPort", value);
            }
        }

        public int MaximumDownloadSpeed {
            get {
                try {
                    return int.Parse(storage.Retrieve("MaximumDownloadSpeed").ToString());
                } catch (Exception) {
                    return 0;
                }
            }
            set {
                storage.Store("MaximumDownloadSpeed", value);
            }
        }

        public int MaximumUploadSpeed {
            get {
                try {
                    return int.Parse(storage.Retrieve("MaximumUploadSpeed").ToString());
                } catch (Exception) {
                    return 0;
                }
            }
            set {
                storage.Store("MaximumUploadSpeed", value);
            }
        }

        public bool DisplayDetails {
            get {
                try {
                    return Boolean.Parse(storage.Retrieve("DisplayDetails").ToString());
                } catch (Exception) {
                    return true;
                }
            }
            set {
                storage.Store("DisplayDetails", value);
            }
        }

        public bool DisplayToolbar {
            get {
                try {
                    return Boolean.Parse(storage.Retrieve("DisplayToolbar").ToString());
                } catch (Exception) {
                    return true;
                }
            }
            set {
                storage.Store("DisplayToolbar", value);
            }
        }

        public int WindowWidth {
            get {
                try {
                    return int.Parse(storage.Retrieve("WindowWidth").ToString());
                } catch (Exception) {
                    return 640;
                }
            }
            set {
                storage.Store("WindowWidth", value);
            }
        }

        public int WindowHeight {
            get {
                try {
                    return int.Parse(storage.Retrieve("WindowHeight").ToString());
                } catch (Exception) {
                    return 480;
                }
            }
            set {
                storage.Store("WindowHeight", value);
            }
        }

        public int SplitterPosition {
            get {
                try {
                    return int.Parse(storage.Retrieve("SplitterPosition").ToString());
                } catch (Exception) {
                    return 200;
                }
            }
            set {
                storage.Store("SplitterPosition", value);
            }
        }
    }
}
