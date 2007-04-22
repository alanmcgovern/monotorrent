//
// global.asax.cs
//
// Authors:
//   Gregor Burger burger.gregor@gmail.com
//
// Copyright (C) 2006 Gregor Burger
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
    
using System;
using System.Web;
using System.Diagnostics;
using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{
    
    public class Global : HttpApplication
    {
        
        public Global()
        {
            Debug.Listeners.Add(new TextWriterTraceListener(Console.Out));
        }
        
        public void Application_Start()
        {
            Console.Write("Starting Tracker...");
            try {
                //HttpUtility.UrlDecode("%CFI1Zh%17%AF%E7%AA%01%D1%2B%FE%99%A6%B3G%95%DC%01");
                TrackerEngine engine = TrackerEngine.Instance;
                engine.Frontend = TrackerFrontend.ExternalHttp;
                engine.Start();
                Debug.WriteLine("after engine.Start");
                Application.Add("engine", engine);
                Application.Add("tracker", Tracker.Instance);
                Torrent test = new Torrent();
                test.LoadTorrent("bin/torrentcreator.torrent");
                Tracker.Instance.AddTorrent(test);
            } catch (Exception e) {
                Console.WriteLine(e);
            }
            Console.WriteLine("done");
        }

        public void Application_End()
        {
            Console.Write("Stopping Tracker...");
            /*TrackerEngine engine = Application.Get("engine") as TrackerEngine;
            if (engine == null)
                Console.WriteLine("phuck");
            else*/
                //engine.Stop();
            Console.WriteLine("done");
        }

    }
    
    
    
}
