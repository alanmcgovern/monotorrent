//
// InternalHttpServer.cs
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
using System.IO;
using System.Net;
using System.Web;
using System.Text;
using System.Collections.Specialized;
using System.Diagnostics;

using MonoTorrent.Common;

namespace MonoTorrent.Tracker
{   
    public class InternalHttpServer
    {
        private Tracker tracker;
        private int port;
        private string ip;
        private HttpListener listener;
        private bool running;
        
        public InternalHttpServer(string ip, ushort port)
        {
            tracker = Tracker.Instance;
            this.ip = ip;
            this.port = port;
            
            listener = new HttpListener();
        }
        
        public string AnnouncePath
        {
            get {
                return announce_path;
            }
            set {
                announce_path = value;
            }
        }
        private string announce_path = "/announce";
        
        public string ScrapePath
        {
            get {
                return scrape_path;
            }
            set {
                scrape_path = value;
            }
        }
        private string scrape_path = "/scrape";
        
        public void Start()
        {
            running = true;
            listener.Prefixes.Add(string.Format("http://{0}:{1}/", ip, port));
            Console.WriteLine(string.Format("http://{0}:{1}/", ip, port));
            listener.Start();
            //FIXME start thread???
            WaitForRequests();
        }
        
        public void Stop()
        {
            running = false;
            listener.Stop();
        }
        
        private void WaitForRequests()
        {
            while (running)
            {                
                Console.Write("waiting for requests...");//TODO remove
                HttpListenerContext context = listener.GetContext();
                string path = context.Request.Url.LocalPath;
                Console.WriteLine("got one: " + context.Request.RawUrl);//TODO remove
                if (path.Equals(announce_path)) {
                    HandleAnnounce(context);
                    continue;
                }
                if (path.Equals(scrape_path)) {
                    HandleScrape(context);
                    continue;
                }
                HandleNotFound(context);
            }
        }
        
        private void HandleAnnounce(HttpListenerContext context)
        {            
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            try {
                AnnounceParameters parm = new AnnounceParameterParser(context.Request.RawUrl).GetParameters();
                if (parm.ip == null || parm.ip.Length < 1) {
                    parm.ip = context.Request.RemoteEndPoint.Address.ToString();
                }
                if (parm.ip == null) {                   
                    Debug.WriteLine("ip volle null---------------------------------------");
                }
                tracker.Announce(parm, context.Response.OutputStream);            
            
            } catch (Exception e) {
                tracker.Failure(e.Message, context.Response.OutputStream);
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
                //Debug.WriteLine(e.Source);
            }
            finally {
                if (!context.Request.KeepAlive)
                    context.Response.OutputStream.Close();
            }
            
        }
        
        private void HandleScrape(HttpListenerContext context)
        {
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 200;
            
            
            try {
                ScrapeParameters parm = new ScrapeParameterParser(context.Request.RawUrl).GetParameters();
                tracker.Scrape(parm, context.Response.OutputStream);
            } catch (TrackerException e) {
                tracker.Failure(e.Message, context.Response.OutputStream);
                Debug.WriteLine(e.StackTrace);
            } catch (Exception e) {
                tracker.Failure("internal Tracker error: " + e.Message, context.Response.OutputStream);
                Debug.WriteLine(e.StackTrace);
            }
            finally {
                if (!context.Request.KeepAlive)
                    context.Response.Close();                
            }            
        }
        
        private void HandleNotFound(HttpListenerContext context)
        {
            context.Response.StatusCode = 404;
            context.Response.StatusDescription = "file not found";            
            StringBuilder responseBuilder = new StringBuilder();
            responseBuilder.Append("<HTML><BODY>");
            responseBuilder.Append(string.Format("Request not found: {0}", context.Request.Url.LocalPath));
            
            responseBuilder.Append("<table border=1>");
            foreach (string name in context.Request.QueryString) {
                string tentry = string.Format(" <tr><td>{0}</td><td>{1}</td></tr>", name, context.Request.QueryString.Get(name));
                responseBuilder.Append(tentry);
            }
            responseBuilder.Append("</table>");
            
            responseBuilder.Append("</BODY></HTML>");
            byte[] response = Encoding.UTF8.GetBytes(responseBuilder.ToString());
            
            context.Response.ContentLength64 = response.Length;
            context.Response.OutputStream.Write(response, 0, response.Length);
        }
        
        
    }
    
}
