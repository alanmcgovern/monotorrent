//
// DebugStatistics.cs
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


#if STATS  // Conditional compilation

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

using log4net;
using log4net.ObjectRenderer;
using log4net.Appender;
using log4net.Layout;

using MonoTorrent.Common;
using MonoTorrent.Client;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Client.Encryption;
using MonoTorrent.Client.Messages;

namespace SampleClient.Stats
{
    /// <summary>
    /// delegate with no parameters - simulates the parameterless Action delegate from .NET 3.0
    /// </summary>
    delegate void NoParam();


    /// <summary>
    /// Used for displaying debugging statistics for a single torrent.
    /// 
    /// To use: 
    /// 1. Create new instance of DebugStatistics with a ClientEngine and (optionally) a TorrentManager
    /// 2. Set the TorrentManager instance via either the drop-down box at the top of the StatsBox, or programatically by setting
    ///     the Manager property.
    ///     
    /// NOTE: This code depends on System.Windows.Forms (for drawing), and log4net (http://logging.apache.org/log4net/index.html)
    /// for the various logging functions. Both these references have to be resolved in the MonoTorrent project for this to work.
    /// 
    /// To alleviate worries about these dependencies, all this code is only compiled conditional on the STATS symbol. Without
    /// this symbol, nothing referencing either log4net or SWF will exist (unless it exists in other code), and the compiler
    /// won't complain about not finding these assemblies
    /// </summary>
    public partial class DebugStatistics : Form
    {
        #region Fields

        private ClientEngine engine;
        private TorrentManager manager;
        private object managerLock = new object();
        private bool disposed;

        // DebugStatistics thread
        private Thread thread;

        // logging fields
        private ILog statsLog;              // logger for stats
        private ILog announceLog;           // logger for tracker announces
        private ILog connectionLog;         // logger for connect/disconnects
        private String peerLogDir;          // directory for all per-peer logs

        // statistic fields
        private Stopwatch stopwatch;
        private long lastStatsWrite;

        private long milliSeconds;
        private long bytesDownloaded;
        private long bytesUploaded;
        private int downloadSpeed;
        private int uploadSpeed;
        private int peers;
        private int totalChoked;                    // number of peers that we've choked
        private int totalUnchoked;                  // number of peers that we've unchoked
        private int totalInterested;                // number of peers that we are interested in
        private int totalChokingUs;                 // number of peers that are choking us
        private int totalUnchokingUs;               // number of peers that are unchoking us
        private int totalOptimisticallyUnchokingUs; // number of peers that are optimistically unchoking us
        private int totalInterestedInUs;            // number of peers that are interested in us

        private StatsBox statsBox;
        private Pieces pieces;
        private SortableBindingList<PeerInfo> peerList;
        private BindingSource bindingSource;
        private int[] numHeaderClicks;

        private Dictionary<Uri, PeerMessageLogger> loggers;

        #endregion


        #region Properties

        /// <summary>
        /// The TorrentManager that this class is attached to
        /// </summary>
        public TorrentManager Manager
        {
            get { return this.manager; }
            set
            {
                lock (this.managerLock)
                {
                    if (this.manager != value)
                    {
                        if (this.manager != null)
                        {
                            // unregister the events

                            this.manager.PeerConnected -= new EventHandler<PeerConnectionEventArgs>(PeerConnectedHandler);
                            this.manager.PeerDisconnected -= new EventHandler<PeerConnectionEventArgs>(PeerDisconnectedHandler);
                            this.manager.ConnectionAttemptFailed -= this.ConnectionAttemptFailedHandler;
                            this.manager.Engine.ConnectionManager.PeerMessageTransferred -= new EventHandler<PeerMessageEventArgs>(PeerMessageTransferredHandler);
                            foreach(TrackerTier tier in manager.TrackerManager.TrackerTiers)
                                foreach(Tracker t in tier.Trackers)
                                    t.AnnounceComplete -= new EventHandler<AnnounceResponseEventArgs>(TrackerAnnounceCompleteHandler);
                        }

                        this.peerList.Clear();
                        this.statsBox.Clear();

                        this.manager = value;
                        this.pieces.Manager = this.manager;

                        if (this.manager != null)
                        {
                            // set up logging for the new manager
                            ConfigureLogging();

                            // register the events

                            this.manager.PeerConnected += new EventHandler<PeerConnectionEventArgs>(PeerConnectedHandler);
                            this.manager.PeerDisconnected += new EventHandler<PeerConnectionEventArgs>(PeerDisconnectedHandler);
                            this.manager.ConnectionAttemptFailed += this.ConnectionAttemptFailedHandler;
                            this.manager.Engine.ConnectionManager.PeerMessageTransferred += new EventHandler<PeerMessageEventArgs>(PeerMessageTransferredHandler);
                            foreach (TrackerTier tier in manager.TrackerManager.TrackerTiers)
                                foreach (Tracker t in tier.Trackers)
                                    t.AnnounceComplete += new EventHandler<AnnounceResponseEventArgs>(TrackerAnnounceCompleteHandler);

                            this.statsBox.SetTorrent(this.manager);

                            // set the title
                            Utils.PerformControlOperation(this, new NoParam(delegate
                                {
                                    this.Text = "DebugStatistics: " + this.manager.Torrent.Name;
                                }));

                            // timer stuff
                            if (this.stopwatch == null)
                                this.stopwatch = new Stopwatch();

                            this.stopwatch.Stop();
                            this.stopwatch.Reset();
                            this.stopwatch.Start();
                        }
                    }
                }
            }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Initialize the DebugStatistics class without a specified initial TorrentManager
        /// </summary>
        public DebugStatistics(ClientEngine engine)
            : this(engine, null)
        { }

        /// <summary>
        /// Start a DebugStatistics instance with an initial TorrentManager
        /// </summary>
        /// <param name="engine">ClientEngine</param>
        /// <param name="manager">TorrentManager</param>
        public DebugStatistics(ClientEngine engine, TorrentManager manager)
        {
            InitializeComponent();

            this.peerLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peerlogs");

            this.peerList = new SortableBindingList<PeerInfo>();
            this.loggers = new Dictionary<Uri, PeerMessageLogger>();

            this.dataGridView1.AutoGenerateColumns = true;

            this.bindingSource = new BindingSource();
            this.bindingSource.DataSource = this.peerList;

            this.dataGridView1.DataSource = this.bindingSource;

            // make everything readonly and set the column widths
            int[] colWidths = new int[] { 149, 68, 70, 56, 51, 56, 66, 61, 73, 74, 76, 81, 59, 63, 91, 73, 75 };
            foreach (DataGridViewColumn col in this.dataGridView1.Columns)
            {
                col.ReadOnly = true;
                col.Width = colWidths[col.Index];
            }

            this.numHeaderClicks = new int[this.dataGridView1.Columns.Count];

            this.statsBox = new StatsBox();
            this.pieces = new Pieces();

            this.statsBox.SelectedTorrent += new EventHandler<TorrentEventArgs>(SelectedTorrentHandler);

            this.statsBox.Show();
            this.pieces.Show();

            this.engine = engine;
            foreach (TorrentManager mgr in this.engine.Torrents)
            {
                this.statsBox.TorrentAdded(mgr);
            }
            this.engine.TorrentRegistered += new EventHandler<TorrentEventArgs>(TorrentRegisteredHandler);
            this.engine.TorrentUnregistered += new EventHandler<TorrentEventArgs>(TorrentUnregisteredHandler);
            this.Manager = manager;

            // start the DebugStatistics thread
            this.thread = new Thread(
                (ThreadStart)delegate
            {
                while (!this.disposed)
                {
                    try
                    {
                        TorrentDebugStatistics();
                    }
                    catch(Exception e)
                    {
                        LogManager.GetLogger("error").Error("Error in TorrentDebugStatistics: ", e);
                    }
                    Thread.Sleep(500);
                }
            });
            this.thread.Name = "DebugStatistics";
            this.thread.IsBackground = true;
            this.thread.Start();
        }


        /// <summary>
        /// This needs to be run every time a new torrent manager is loaded, so that the log files
        /// roll over and aren't overwritten.
        /// </summary>
        private void ConfigureLogging()
        {
            string[] names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            ResourceManager rm = new ResourceManager("SampleClient.Stats.log4net", Assembly.GetExecutingAssembly());
            String config = rm.GetString("config");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(config);

            XmlElement element = (XmlElement)doc.GetElementsByTagName("log4net")[0];
            log4net.Config.XmlConfigurator.Configure(element);

            // set up the loggers
            this.statsLog = LogManager.GetLogger(typeof(DebugStatistics));
            this.connectionLog = LogManager.GetLogger(typeof(ConnectionManager));
            this.announceLog = LogManager.GetLogger(typeof(TrackerManager));

            // stats log file headers
            statsLog.Info(" Time Percent Playback Total-Down Total-Up Download-Speed Upload-Speed Peers Choked"
                    + " Unchoked Interested Choking-Us Unchoking-Us Optimistically-Unchoking Interested-In-Us");

            RolloverPeerLogs();
        }


        /// <summary>
        /// 
        /// </summary>
        private void RolloverPeerLogs()
        {
            int num = 0;

            // see how many rollover directories there are
            while (Directory.Exists(peerLogDir + "." + (num + 1)))
                num++;

            // increment all of them by 1
            for (; num > 0; num--)
                Directory.Move(peerLogDir + "." + num, peerLogDir + "." + (num + 1));

            // roll over the current directory to peerlogs.1, if it exists
            if (Directory.Exists(peerLogDir))
                Directory.Move(peerLogDir, peerLogDir + ".1");
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handle torrent registration event from ClientEngine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TorrentRegisteredHandler(object sender, TorrentEventArgs args)
        {
            this.statsBox.TorrentAdded(args.TorrentManager);
        }


        /// <summary>
        /// Handle torrent unregistered event from ClientEngine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TorrentUnregisteredHandler(object sender, TorrentEventArgs args)
        {
            this.statsBox.TorrentRemoved(args.TorrentManager);
        }


        /// <summary>
        /// Handle selection of torrent from StatsBox drop down menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void SelectedTorrentHandler(object sender, TorrentEventArgs e)
        {
            this.Manager = e.TorrentManager;
        }


        /// <summary>
        /// Log the peer message event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PeerMessageTransferredHandler(object sender, PeerMessageEventArgs args)
        {
            lock (loggers)
            {
                if (args.TorrentManager == this.manager)
                {
                    PeerMessageLogger logger;

                    if (loggers.ContainsKey(args.ID.Peer.ConnectionUri))
                    {
                        logger = loggers[args.ID.Peer.ConnectionUri];
                    }
                    else
                    {
                        logger = new PeerMessageLogger(args.ID.Peer.ConnectionUri.ToString(), this.peerLogDir);
                        loggers[args.ID.Peer.ConnectionUri] = logger;
                    }

                    logger.LogPeerMessage(args);
                }
            }
        }


        /// <summary>
        /// Log the failed connection attempt
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ConnectionAttemptFailedHandler(object sender, PeerConnectionFailedEventArgs args)
        {
            connectionLog.InfoFormat("Failed to {0} peer at {1}. Message: {2}",
                args.ConnectionDirection == Direction.Incoming ? "accept connection from" : "connect to", 
                args.Peer.ConnectionUri, args.Message);
        }


        /// <summary>
        /// Log the peer connection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PeerConnectedHandler(object sender, PeerConnectionEventArgs args)
        {
            if (args.PeerID != null)
            {
                String msg = String.Format("{0} peer at {1}",
                    args.ConnectionDirection == Direction.Incoming ? "Accepted connection from" : "Connected to",
                    args.PeerID.Peer.ConnectionUri);

                connectionLog.Info(msg);

                lock (this.loggers)
                {
                    PeerMessageLogger logger;

                    if (!this.loggers.ContainsKey(args.PeerID.Peer.ConnectionUri))
                    {
                        logger = new PeerMessageLogger(args.PeerID.Peer.ConnectionUri.ToString(), this.peerLogDir);
                        this.loggers[args.PeerID.Peer.ConnectionUri] = logger;
                    }
                    else
                    {
                        logger = this.loggers[args.PeerID.Peer.ConnectionUri];
                    }

                    logger.LogPeerMessage(msg);
                }
            }
        }


        /// <summary>
        /// Log the peer disconnection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void PeerDisconnectedHandler(object sender, PeerConnectionEventArgs args)
        {
            if (args.PeerID != null)
            {
                String msg = String.Format("{0} peer at {1}. Reason: {2}",
                    args.ConnectionDirection == Direction.Incoming ? "Got disconnected from" : "Disconnected from",
                    args.PeerID.Peer.ConnectionUri, args.Message);

                connectionLog.Info(msg);

                lock (this.loggers)
                {
                    PeerMessageLogger logger;

                    if (!this.loggers.ContainsKey(args.PeerID.Peer.ConnectionUri))
                    {
                        logger = new PeerMessageLogger(args.PeerID.Peer.ConnectionUri.ToString(), this.peerLogDir);
                        this.loggers[args.PeerID.Peer.ConnectionUri] = logger;
                    }
                    else
                    {
                        logger = this.loggers[args.PeerID.Peer.ConnectionUri];
                    }

                    logger.LogPeerMessage(msg);
                }
            }
        }


        /// <summary>
        /// Log the tracker announce
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void TrackerAnnounceCompleteHandler(object sender, AnnounceResponseEventArgs e)
        {
            this.announceLog.InfoFormat(
                "Announce {0} complete for tracker {1}. Success = {2}. Number of peers = {3}. Update Interval = {4}, Minimum = {5}",
                    e.TrackerId.TorrentEvent.ToString("G"), e.Tracker, e.Successful, e.Peers.Count, e.Tracker.UpdateInterval,
                    e.Tracker.MinUpdateInterval);

        }


        /// <summary>
        /// Provides sorting for boolean columns
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView1_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            this.numHeaderClicks[e.ColumnIndex]++;
            switch (e.ColumnIndex)
            {
                case 1:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["Connected"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                case 8:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["ImChoking"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                case 9:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["ImInterested"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                case 10:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["HesChoking"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                case 11:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["HesInterested"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                case 12:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["IsSeeder"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                case 16:
                    this.peerList.ApplySort(TypeDescriptor.GetProperties(typeof(PeerInfo))["Encrypted"],
                        (this.numHeaderClicks[e.ColumnIndex] % 2 == 0 ? ListSortDirection.Ascending : ListSortDirection.Descending));
                    break;
                default:
                    break;

            }
        }


        /// <summary>
        /// Display the peer message window for the clicked row
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView1_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                Uri uri = this.dataGridView1.Rows[e.RowIndex].Cells[0].Value as Uri;

                lock (this.loggers)
                {
                    PeerMessageLogger logger;

                    if (!this.loggers.ContainsKey(uri))
                    {
                        logger = new PeerMessageLogger(uri.ToString(), this.peerLogDir);
                        this.loggers[uri] = logger;
                    }
                    else
                    {
                        logger = this.loggers[uri];
                    }

                    logger.CreatePeerDisplay();
                }
            }
        }

        #endregion


        #region Logic

        /// <summary>
        /// Debug thread logic
        /// </summary>
        private void TorrentDebugStatistics()
        {
            try
            {
                lock (this.managerLock)
                {
                    if (this.disposed || this.manager == null)
                        return;

                    StringBuilder sb = new StringBuilder(1024);

                    List<PeerId> pidCopy;
                    List<Peer> allPeers;

                    totalChoked = 0;
                    totalUnchoked = 0;
                    totalInterested = 0;
                    totalInterestedInUs = 0;
                    totalChokingUs = 0;
                    totalUnchokingUs = 0;
                    totalOptimisticallyUnchokingUs = 0;

                    pidCopy = new List<PeerId>(this.manager.Peers.ConnectedPeers);
                    allPeers = new List<Peer>(this.manager.Peers.AllPeers());

                    sb.Remove(0, sb.Length);

                    AppendFormat(sb, "Disk Read Rate:       {0:0.00} kB/s", this.manager.Engine.DiskManager.ReadRate / 1024.0);
                    AppendFormat(sb, "Disk Write Rate:      {0:0.00} kB/s", this.manager.Engine.DiskManager.WriteRate / 1024.0);
                    AppendFormat(sb, "Total Read:           {0:0.00} kB", this.manager.Engine.DiskManager.TotalRead / 1024.0);
                    AppendFormat(sb, "Total Written:        {0:0.00} kB", this.manager.Engine.DiskManager.TotalWritten / 1024.0);
                    AppendFormat(sb, "Rounds Complete:       {0}", this.manager.PeerReviewRoundsComplete);

                    this.milliSeconds = this.stopwatch.ElapsedMilliseconds;
                    this.bytesDownloaded = this.manager.Monitor.DataBytesDownloaded;
                    this.bytesUploaded = this.manager.Monitor.DataBytesUploaded;
                    this.downloadSpeed = this.manager.Monitor.DownloadSpeed;
                    this.uploadSpeed = this.manager.Monitor.UploadSpeed;
                    this.peers = this.manager.Peers.ConnectedPeers.Count;

                    foreach (PeerInfo pi in this.peerList)
                    {
                        pi.seen = false;
                    }

                    foreach (PeerId pIdInternal in pidCopy)
                    {
                        if (pIdInternal.IsChoking)
                            totalChokingUs++;
                        else
                        {
                            totalUnchokingUs++;

                            // if they aren't interested and they are unchoking us, we mark it as an optimistic unchoke
                            if (!pIdInternal.IsInterested)
                                totalOptimisticallyUnchokingUs++;
                        }
                        if (pIdInternal.AmChoking)
                            totalChoked++;
                        else
                            totalUnchoked++;

                        if (pIdInternal.IsInterested)
                            totalInterestedInUs++;

                        if (pIdInternal.AmInterested)
                            totalInterested++;

                        PeerInfo p = null;
                        foreach (PeerInfo pi in this.peerList)
                        {
                            if (pi.Uri.Equals(pIdInternal.Peer.ConnectionUri))
                            {
                                p = pi;
                                break;
                            }
                        }

                        if (p == null)
                        {
                            p = new PeerInfo(pIdInternal);

                            Utils.PerformControlOperation(this.dataGridView1, delegate { this.peerList.Add(p); });
                        }
                        else
                        {
                            p.UpdateData(pIdInternal);
                        }
                        p.seen = true;

                        foreach (PeerInfo pi in this.peerList)
                        {
                            if (!pi.seen)
                                pi.Connected = false;
                        }
                    }

                    Utils.PerformControlOperation(this.dataGridView1, new NoParam(this.dataGridView1.Refresh));

                    AppendSeperator(sb);

                    AppendFormat(sb, "Total Download Heuristics:", "");
                    AppendFormat(sb, "\tWe've Choked:          {0}", totalChoked);
                    AppendFormat(sb, "\tWe've Unchoked:        {0}", totalUnchoked);
                    AppendFormat(sb, "\tWe're Interested In:   {0}", totalInterested);
                    AppendFormat(sb, "", null);
                    AppendFormat(sb, "\tChoking Us:            {0}", totalChokingUs);
                    AppendFormat(sb, "\tUnchoking Us:          {0}", totalUnchokingUs);
                    AppendFormat(sb, "\tInterested In Us:      {0}", totalInterestedInUs);

                    AppendSeperator(sb);

                    AppendFormat(sb, "Name:                 {0}", this.manager.Torrent.Name);
                    AppendFormat(sb, "Progress:             {0:0.00}", this.manager.Progress);
                    AppendFormat(sb, "Download Speed:       {0:0.00} kB/s", this.manager.Monitor.DownloadSpeed / 1024.0);
                    AppendFormat(sb, "Upload Speed:         {0:0.00} kB/s", this.manager.Monitor.UploadSpeed / 1024.0);
                    AppendFormat(sb, "Total Downloaded:     {0:0.00} MB", this.manager.Monitor.DataBytesDownloaded / (1024.0 * 1024.0));
                    AppendFormat(sb, "Total Uploaded:       {0:0.00} MB", this.manager.Monitor.DataBytesUploaded / (1024.0 * 1024.0));
                    AppendFormat(sb, "Tracker:              {0}", this.manager.TrackerManager.CurrentTracker);
                    AppendFormat(sb, "Tracker Status:       {0}", this.manager.TrackerManager.CurrentTracker.State);
                    AppendFormat(sb, "Warning Message:      {0}", this.manager.TrackerManager.CurrentTracker.WarningMessage);
                    AppendFormat(sb, "Failure Message:      {0}", this.manager.TrackerManager.CurrentTracker.FailureMessage);
                    //AppendFormat( sb, "Piece Picker:         {0}", this.manager.PieceManager.GetWindow() );
                    AppendFormat(sb, "Total Connections:    {0}", this.manager.OpenConnections);
                    AppendFormat(sb, "Seeds:                {0}", this.manager.Peers.Seeds);
                    AppendFormat(sb, "Leeches:              {0}", this.manager.Peers.Leechs);
                    AppendFormat(sb, "Available Peers:      {0}", this.manager.Peers.AvailablePeers.Count);

                    this.statsBox.SetText(sb.ToString());
                }

                // log the statistics every 30 seconds
                if (this.milliSeconds - this.lastStatsWrite > 30000)
                {
                    //statsLog.Info(" Time Percent Playback Total-Down Total-Up Download-Speed Upload-Speed Peers Choked"
                    //+ " Unchoked Interested Choking-Us Unchoking-Us Optimistically-Unchoking Interested-In-Us");
                    statsLog.InfoFormat("{0,5} {1,7} {2,8} {3,10} {4,8} {5,14} {6,12} {7,5} {8,6} {9,8} {10,10} {11,10} {12,12} {13,24} {14,16}",
                        this.milliSeconds / 1000, this.manager.Progress.ToString("#0.##"),
                        // useless data till the SlidingWindowPicker code hits SVN
                        0, 0,
                        //(((double)this.manager.PieceManager.HighPrioritySetStart / (double)this.manager.Torrent.Pieces.Count) * 100).ToString("#0.##"),
                        this.bytesDownloaded, this.bytesUploaded, this.downloadSpeed, this.uploadSpeed, this.peers, this.totalChoked,
                        this.totalUnchoked, this.totalInterested, this.totalChokingUs, this.totalUnchokingUs,
                        this.totalOptimisticallyUnchokingUs, this.totalInterestedInUs);

                    this.lastStatsWrite = this.milliSeconds;
                }
            }
            catch (Exception e)
            {
                LogManager.GetLogger("error").Error("Error in TorrentDebugStatistics: ", e);
            }
        }

        #endregion


        #region Utils

        private static void AppendSeperator(StringBuilder sb)
        {
            AppendFormat(sb, "", null);
            AppendFormat(sb, "- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -", null);
            AppendFormat(sb, "", null);
        }


        private static void AppendFormat(StringBuilder sb, string str, params object[] formatting)
        {
            if (formatting != null)
                sb.AppendFormat(str, formatting);
            else
                sb.Append(str);
            sb.AppendLine();
        }

        #endregion
    }


    /// <summary>
    /// Taken from http://www.timvw.be/presenting-the-sortablebindinglistt/
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class SortableBindingList<T> : BindingList<T>
    {
        private PropertyDescriptor propertyDescriptor;
        private ListSortDirection listSortDirection;
        private bool isSorted;

        protected override bool SupportsSortingCore
        {
            get { return true; }
        }

        protected override bool IsSortedCore
        {
            get { return this.isSorted; }
        }

        protected override PropertyDescriptor SortPropertyCore
        {
            get { return this.propertyDescriptor; }
        }

        protected override ListSortDirection SortDirectionCore
        {
            get { return this.listSortDirection; }
        }

        public void ApplySort(PropertyDescriptor prop, ListSortDirection direction)
        {
            ApplySortCore(prop, direction);
        }

        protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
        {
            List<T> itemsList = this.Items as List<T>;
            itemsList.Sort(delegate(T t1, T t2)
            {
                this.propertyDescriptor = prop;
                this.listSortDirection = direction;
                this.isSorted = true;

                int reverse = direction == ListSortDirection.Ascending ? 1 : -1;

                PropertyInfo propertyInfo = typeof(T).GetProperty(prop.Name);
                object value1 = propertyInfo.GetValue(t1, null);
                object value2 = propertyInfo.GetValue(t2, null);

                IComparable comparable = value1 as IComparable;
                if (comparable != null)
                {
                    return reverse * comparable.CompareTo(value2);
                }
                else
                {
                    comparable = value2 as IComparable;
                    if (comparable != null)
                    {
                        return -1 * reverse * comparable.CompareTo(value1);
                    }
                    else
                    {
                        return 0;
                    }
                }
            });

            this.OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected override void RemoveSortCore()
        {
            this.isSorted = false;
            this.propertyDescriptor = base.SortPropertyCore;
            this.listSortDirection = base.SortDirectionCore;
        }
    }
}

#endif