//
// PeerMessageLogger.cs
//
// Authors:
//   Karthik Kailash    karthik.l.kailash@gmail.com
//   David Sanghera     dsanghera@gmail.com
//
// Copyright (C) 2006 Karthik Kailash, David Sanghera
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
#if STATS

using System;
using System.IO;

using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

using MonoTorrent.Client;
using MonoTorrent.Common;

namespace SampleClient.Stats
{
    /// <summary>
    /// Used for logging peer messages, both to disk and to a PeerMessageDisplay (if open)
    /// </summary>
    class PeerMessageLogger : log4net.Appender.AppenderSkeleton
    {
        private static PatternLayout layout;            // default pattern layout

        private IAppender appender;
        private ILog LOG;

        private String uri;
        private String filePath;
        private PeerMessagesDisplay display;

        static PeerMessageLogger()
        {
            layout = new PatternLayout();
            layout.ConversionPattern = "%date{HH:mm:ss} - %message%newline";
            layout.ActivateOptions();
        }


        public PeerMessageLogger(String peerUri, String peerLogDir)
        {
            this.uri = peerUri;

            // create the appender
            filePath = Path.Combine(peerLogDir, CreateLogFileNameFromUri(peerUri));
            appender = CreateFileAppender(peerUri, filePath, layout);

            // create the logger
            LOG = LogManager.GetLogger("peer." + peerUri);

            // set layout options for us, the appender
            this.Layout = layout;
            this.ActivateOptions();

            // attach ourselves as an appender to the logger
            AddAppender(LOG, this);
        }


        /// <summary>
        /// Display the logging messages onto a display window. Further logged peer messages will be written to the window
        /// automatically.
        /// </summary>
        public void CreatePeerDisplay()
        {
            lock (this)
            {
                if (this.display == null)
                {
                    this.display = new PeerMessagesDisplay(this.uri, this.filePath);
                    this.display.LoadLog();

                    this.display.Disposed += new EventHandler(DisplayDisposed);
                }
                else
                {
                    this.display.BringToFront();
                }
            }
        }


        private void DisplayDisposed(object sender, EventArgs args)
        {
            lock (this)
            {
                this.display = null;
            }
        }


        /// <summary>
        /// TODO: Figure out how the different messages should get logged. Right now this just involves overriding a
        /// ToString
        /// </summary>
        /// <param name="message"></param>
        public void LogPeerMessage(PeerMessageEventArgs args)
        {
            LOG.InfoFormat("{0}: {1}", args.Direction == Direction.Incoming ? "Received" : "Sent", args.Message);
        }


        public void LogPeerMessage(object message)
        {
            LOG.Info(message);
        }


        /// <summary>
        /// Get the file name for a given peer uri
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public static String CreateLogFileNameFromUri(String uri)
        {
            uri = uri.Replace(':', '.');
            char[] invalidChars = Path.GetInvalidFileNameChars();
            int index;

            for (int i = 0; i < invalidChars.Length; )
            {
                if ((index = uri.IndexOf(invalidChars[i])) > 0)
                {
                    uri = uri.Remove(index, 1);
                }
                else
                    i++;
            }

            return uri + ".log";
        }


        #region log4net utils

        //this code taken from: http://mail-archives.apache.org/mod_mbox/logging-log4net-user/200602.mbox/%3CDDEB64C8619AC64DBC074208B046611C769745@kronos.neoworks.co.uk%3E

        // Set the level for a named logger
        public static void SetLevel(string loggerName, string levelName)
        {
            ILog log = LogManager.GetLogger(loggerName);
            log4net.Repository.Hierarchy.Logger l = (log4net.Repository.Hierarchy.Logger)log.Logger;

            l.Level = l.Hierarchy.LevelMap[levelName];
        }


        /// <summary>
        /// Add an appender to a logger
        /// </summary>
        /// <param name="loggerName"></param>
        /// <param name="appender"></param>
        public static void AddAppender(string loggerName, log4net.Appender.IAppender appender)
        {
            log4net.ILog log = log4net.LogManager.GetLogger(loggerName);
            AddAppender(log, appender);
        }


        /// <summary>
        /// Add an appender to a logger
        /// </summary>
        /// <param name="log"></param>
        /// <param name="appender"></param>
        public static void AddAppender(ILog log, IAppender appender)
        {
            log4net.Repository.Hierarchy.Logger l = (log4net.Repository.Hierarchy.Logger)log.Logger;

            l.AddAppender(appender);
        }


        // Find a named appender already attached to a logger
        public static IAppender FindAppender(string appenderName)
        {
            foreach (IAppender appender in LogManager.GetRepository().GetAppenders())
            {
                if (appender.Name == appenderName)
                {
                    return appender;
                }
            }
            return null;
        }


        /// <summary>
        /// Create a new file appender
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static IAppender CreateFileAppender(string name, string fileName)
        {
            PatternLayout layout = new PatternLayout();
            layout.ConversionPattern = "%d [%t] %-5p %c [%x] - %m%n";
            layout.ActivateOptions();

            return CreateFileAppender(name, fileName, layout);
        }


        /// <summary>
        /// Create a new file appender with specified layout
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fileName"></param>
        /// <param name="layout"></param>
        /// <returns></returns>
        public static IAppender CreateFileAppender(string name, string fileName, PatternLayout layout)
        {
            FileAppender appender = new FileAppender();
            appender.Name = name;
            appender.File = fileName;
            appender.AppendToFile = true;
            appender.LockingModel = new FileAppender.MinimalLock();

            //TODO: Any other appender options to set?

            appender.Layout = layout;
            appender.ActivateOptions();

            return appender;
        }

        #endregion


        /// <summary>
        /// Sends the log message to the appender and to the display window, if any
        /// </summary>
        /// <param name="loggingEvent"></param>
        protected override void Append(log4net.Core.LoggingEvent loggingEvent)
        {
            // first send it to the default appender
            this.appender.DoAppend(loggingEvent);

            // then, if there is a peer display window, send it to that
            lock (this)
            {
                if (this.display != null)
                {
                    this.display.AddNewMessage(this.RenderLoggingEvent(loggingEvent));
                }
            }
        }
    }
}

#endif