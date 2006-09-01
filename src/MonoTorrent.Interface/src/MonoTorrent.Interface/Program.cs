/*
 * $Id: Program.cs 880 2006-08-19 22:50:54Z piotr $
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

using System.IO;

using Mono.Unix;

using Gtk;

using MonoTorrent.Interface.Controller;
using MonoTorrent.Interface.View;

namespace MonoTorrent.Interface
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Init();
            Run();
            return 0;
        }

        private static void Init()
        {
            string basedir = Path.Combine(System.IO.Path.GetDirectoryName(
                    typeof(Program).Assembly.CodeBase), "locale").Substring(7);
            Catalog.Init("monotorrent", basedir);
            CreateDirs();
            Application.Init();
        }

        private static void CreateDirs()
        {
            CreateDir(PathConstants.CONFIG_DIR);
            CreateDir(PathConstants.TORRENTS_DIR);
            CreateDir(PathConstants.DOWNLOAD_DIR);
        }

        private static void CreateDir(string path)
        {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }

        private static void Run()
        {
            MainWindow window = new MainWindow();
            window.ShowAll();
            new WindowController(window);
            Application.Run();
        }
    }
}
