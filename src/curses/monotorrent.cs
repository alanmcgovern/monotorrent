//
// demo.cs
//
// Authors:
//   Miguel de Icaza (miguel.de.icaza@gmail.com)
//
// Copyright (C) 2007 Novell (http://www.novell.com)
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
using System.Collections.Generic;
using System.IO;
using System.Collections;
using System.Xml;
using System.Xml.Serialization;
using System.Threading;

using Mono.Terminal;

using MonoTorrent.Common;
using MonoTorrent.Client;
using MonoTorrent.BEncoding;

public class TorrentCurses {

	//
	// Configuration data, loaded at startup
	//
	static string config_file = Path.Combine (Environment.GetFolderPath (
							  Environment.SpecialFolder.ApplicationData), "monotorrent.config");
	public static string fast_resume = Path.Combine (Environment.GetFolderPath (
							  Environment.SpecialFolder.ApplicationData), "monotorrent.fastresume");

	static Config config;

	// The torrent status
	static TorrentList torrent_list;
	static TorrentDetailsList details_list;
	static EngineSettings engine_settings;
	static TorrentSettings torrent_settings;
	public static ClientEngine engine;
	static ListView list_details;
	static LogWidget log_widget;
	
	static Label iteration;

	public class TorrentList : IListProvider {
		public List<TorrentManager> items = new List<TorrentManager> ();
		public ListView view;
		private List<FastResume> fastResume;

		void IListProvider.SetListView (ListView v)
		{
			view = v;
		}
		
		int IListProvider.Items {
			get {
				return items.Count;
			}
		}

		public TorrentList ()
		{
			fastResume = new List<FastResume>();
			LoadFastResume ();
		}
		
		public void SaveFastResume ()
		{
			
			BEncodedDictionary dict = new BEncodedDictionary();
			foreach (TorrentManager manager in items)
				dict.Add (manager.Torrent.InfoHash, manager.SaveFastResume ().Encode ());
			try
			{
				File.WriteAllBytes (TorrentCurses.fast_resume, dict.Encode ());
			}
			catch
			{
			}
		}
		void LoadFastResume()
		{
			fastResume = new List<FastResume>();
			
			try
			{
				if (!File.Exists (fast_resume))
					return;
				BEncodedDictionary dict = BEncodedValue.Decode<BEncodedDictionary>(File.ReadAllBytes(fast_resume));
				
				foreach (BEncodedDictionary v in dict.Values)
					fastResume.Add (new FastResume(v));
				
				File.Delete (fast_resume);
			}
			catch
			{
			}
		}
		
		public void Add (string s)
		{
			if (!File.Exists (s))
				return;

			foreach (TorrentManager mgr in items){
				if (mgr.Torrent.TorrentPath == s){
					Application.Info ("Info", "The specified torrent is already loaded");
					continue;
				}
			}
			Torrent torrent = Torrent.Load(s);
			FastResume resume = null;
			foreach (FastResume r in fastResume)
			{
				if (((BEncodedString)r.InfoHash).Equals((BEncodedString)torrent.InfoHash))
					resume = r;
			}
			TorrentManager manager;
			if (resume == null)
				manager = new TorrentManager(torrent, engine_settings.SavePath, (TorrentSettings)torrent_settings.Clone());
			else
				manager = new MonoTorrent.Client.TorrentManager(torrent, engine_settings.SavePath, (TorrentSettings)torrent_settings.Clone(), resume);
			
			manager.PeerConnected +=
				new EventHandler<PeerConnectionEventArgs>(cm_PeerConnected);
			manager.PeerDisconnected +=
			new EventHandler<PeerConnectionEventArgs>(cm_PeerDisconnected);
			engine.Register(manager);
			items.Add (manager);
			
			if (view != null)
				view.ProviderChanged ();

			manager.Start();
		}
		
		bool IListProvider.AllowMark {
			get {
				return false;
			}
		}

		bool IListProvider.IsMarked (int n)
		{
			return false;
		}

		void IListProvider.Render (int line, int col, int width, int item)
		{
			TorrentManager manager = items [item];
			string name = manager.Torrent.Name;
			
			string s = String.Format ("[{0,3:N0}%] {1}", manager.Progress, name);
			if (s.Length > width){
				s = s.Substring (0, width);
				Curses.addstr (s);
			} else {
				Curses.addstr (s);
				for (int i = s.Length; i < width; i++)
					Curses.addch (' ');
			}
		}

		bool IListProvider.ProcessKey (int ch)
		{
			if (ch != '\n')
				return false;

			int selected = view.Selected;
			if (selected == -1)
				return false;

			TorrentControl (selected);
			return true;
		}

		void IListProvider.SelectedChanged ()
		{
			UpdateStatus ();

			list_details.ProviderChanged ();
		}
		
		public void TorrentControl (int selected)
		{
			Dialog d = new Dialog (60, 8, "Torrent Control");

			TorrentManager item = items [selected];
			
			d.Add (new TrimLabel (1, 1, 60-6, item.Torrent.Name));

			bool stopped = item.State == TorrentState.Stopped;
			Button bstartstop = new Button (stopped ? "Start" : "Stop");
			bstartstop.Clicked += delegate {
				if (stopped)
					item.Start();
				else
					item.Stop();
				d.Running = false;
				
			};
			d.AddButton (bstartstop);
			
			// Later, when we hook it up, look up the state
			
			bool paused = item.State == TorrentState.Paused;
			Button br = new Button (paused ? "Resume" : "Pause");
			br.Clicked += delegate {
				if (paused)
					item.Start();
				else
					item.Pause();
				d.Running = false;
			};
			d.AddButton (br);

			Button bd = new Button ("Delete");
			bd.Clicked += delegate {
				Application.Error ("Not Implemented",
						   "I have not implemented delete yet");
				d.Running = false;
			};
			d.AddButton (bd);
			
			Button bmap = new Button ("Map");
			bmap.Clicked += delegate {
				Application.Error ("Not Implemented",
						   "I have not implemented map yet");
				d.Running = false;
			};
			d.AddButton (bmap);

			Button bcancel = new Button ("Cancel");
			bcancel.Clicked += delegate {
				d.Running = false;
			};
			Application.Run (d);
			UpdateStatus ();
		}

		public ArrayList GetPathNames ()
		{
			ArrayList names = new ArrayList ();
			
			foreach (TorrentManager tm in items){
				TorrentDesc td = new TorrentDesc ();
				td.Filename = tm.Torrent.TorrentPath;
				names.Add (td);
			}

			return names;
		}

		public TorrentManager GetSelected ()
		{
			int selected = view.Selected;
			if (selected == -1)
				return null;

			return items [selected];
		}
	}

	public class TorrentDetailsList : IListProvider {
		public ListView view;
		
		void IListProvider.SetListView (ListView v)
		{
			view = v;
		}
		
		int IListProvider.Items {
			get {
				TorrentManager tm = torrent_list.GetSelected ();

				if (tm == null)
					return 0;

				return tm.Torrent.Files.Length;
			}
		}

		bool IListProvider.AllowMark {
			get {
				return false;
			}
		}

		bool IListProvider.IsMarked (int n)
		{
			return false;
		}

		void IListProvider.Render (int line, int col, int width, int item)
		{
			TorrentManager tm = torrent_list.GetSelected ();
			string name;
			
			if (tm == null)
				name = "";
			else 
				name = tm.Torrent.Files [item].Path;

			string s = String.Format ("{0}. {1}", item, name);
			if (s.Length > width){
				s = s.Substring (0, width);
				Curses.addstr (s);
			} else {
				Curses.addstr (s);
				for (int i = s.Length; i < width; i++)
					Curses.addch (' ');
			}
		}

		bool IListProvider.ProcessKey (int ch)
		{
			return false;
		}

		void IListProvider.SelectedChanged ()
		{
		}
	}

	public class LogWidget : Widget {
		string [] messages = new string [80];
		int head, tail;
		int count;
		
		public LogWidget (int x, int y) : base (x, y, 0, 0)
		{
			Fill = Fill.Horizontal | Fill.Vertical;
			AddText ("Started");
		}

		public void AddText (string s)
		{
			messages [head] = s;
			head++;
			if (head == messages.Length)
				head = 0;
			if (head == tail)
				tail = (tail+1) % messages.Length;
		}
		
		public override void Redraw ()
		{
			Curses.attrset (ColorNormal);

			int i = 0;
			int l;
			int n = head > tail ? head-tail : (head + messages.Length) - tail;
			for (l = h-1; l >= 0 && n-- > 0; l--){
				int item = head-1-i;
				if (item < 0)
					item = messages.Length+item;

				Move (y+l, x);

				int sl = messages [item].Length;
				if (sl < w){
					Curses.addstr (messages [item]);
					for (int fi = 0; fi < w-sl; fi++)
						Curses.addch (' ');
				} else {
					Curses.addstr (messages [item].Substring (0, sl));
				}
				i++;
			}

			for (; l >= 0; l--){
				Move (y+l, x);
				for (i = 0; i < w; i++)
					Curses.addch (' ');
			}
		}
	}
	
	static void AddDialog ()
	{
		int cols = (int) (Application.Cols * 0.7);
		Dialog d = new Dialog (cols, 8, "Add");
		Entry e;
		string name = null;
		
		
		d.Add (new Label (1, 0, "Torrent file:"));
		e = new Entry (1, 1, cols - 6, Environment.CurrentDirectory);
		d.Add (e);

		// buttons
		Button b = new Button ("Ok", true);
		b.Clicked += delegate {
			b.Container.Running = false;
			name = e.Text;
		};
		d.AddButton (b);
		b = new Button ("Cancel");
		b.Clicked += delegate {
			b.Container.Running = false;
		};
		d.AddButton (b);
		
		Application.Run (d);

		if (name != null){
			if (!File.Exists (name)){
				Application.Error ("Missing File", "Torrent file:\n" + name + "\ndoes not exist");
				return;
			}
			torrent_list.Add (name);
		}
	}

	static bool ValidateSpeed (Entry e, out int value)
	{
		string t = e.Text;

		if (t == "0" || t == "" || t == "unlimited"){
			value = 0;
			return true;
		}

		if (int.TryParse (t, out value))
			return true;

		Application.Error ("Error", "Invalid speed `{0}' specified", t);
		return false;
	}

	static string RenderSpeed (float f)
	{
		if (f == 0)
			return "unlimited";
		return f.ToString (); 
	}
	
	static void OptionsDialog ()
	{
		Dialog d = new Dialog (62, 15, "Options");

		d.Add (new Label (1, 1, "  Download Directory:"));
		d.Add (new Label (1, 3, "         Listen Port:"));
		d.Add (new Label (1, 5, "  Upload Speed Limit:"));
		d.Add (new Label (35,5, "kB/s"));
		d.Add (new Label (1, 7, "Download Speed Limit:"));
		d.Add (new Label (35,7, "kB/s"));

		Entry download_dir = new Entry (24, 1, 30, config.DownloadDir);
		d.Add (download_dir);

		Entry listen_port = new Entry (24, 3, 6, config.ListenPort.ToString ());
		d.Add (listen_port);

		Entry upload_limit = new Entry (24, 5, 10, RenderSpeed (config.UploadSpeed / 1024));
		d.Add (upload_limit);

		Entry download_limit = new Entry (24, 7, 10, RenderSpeed (config.DownloadSpeed / 1024));
		d.Add (download_limit);
		
		bool ok = false;
		
		Button b = new Button ("Ok", true);
		b.Clicked += delegate { ok = true; b.Container.Running = false; };
		d.AddButton (b);

		b = new Button ("Cancel");
		b.Clicked += delegate { b.Container.Running = false; };
		d.AddButton (b);
		
		Application.Run (d);

		if (ok){
			int v;
			
			if (!Int32.TryParse (listen_port.Text, out v)){
				Application.Error ("Error", "The value `{0}' is not a valid port number", listen_port.Text);
				return;
			}

			if (!Directory.Exists (download_dir.Text)){
				Application.Error ("Error", "The directory\n{0}\ndoes not exist", download_dir.Text);
				return;
			}

			if (!ValidateSpeed (upload_limit, out config.UploadSpeed))
				return;

			if (!ValidateSpeed (download_limit, out config.DownloadSpeed))
				return;

			config.DownloadDir = download_dir.Text;
			config.ListenPort = v;

			// The user enters in kB/sec, the engine expects bytes/sec
			config.UploadSpeed *= 1024;
			config.DownloadSpeed *= 1024;
			TorrentCurses.engine.Settings.GlobalMaxUploadSpeed = config.UploadSpeed;
			TorrentCurses.engine.Settings.GlobalMaxDownloadSpeed = config.DownloadSpeed;
		}
	}

	//
	// We split this, so if the terminal resizes, we resize accordingly
	//
	static void LayoutDialogs (Frame ftorrents, Frame fstatus, Frame fdetails, Frame fprogress)
	{
		int cols = Application.Cols;
		int lines = Application.Lines;
		
		int midx = Application.Cols/2;
		int midy = Application.Lines/2;

		// Torrents
		ftorrents.x = 0;
		ftorrents.y = 0;
		ftorrents.w = cols - 40;
		ftorrents.h = midy;

		// Status: Always 40x12
		fstatus.x = cols - 40;
		fstatus.y = 0;
		fstatus.w = 40;
		fstatus.h = midy;

		// Details
		fdetails.x = 0;
		fdetails.y = midy;
		fdetails.w = midx;
		fdetails.h = midy;

		// fprogress
		fprogress.x = midx;
		fprogress.y = midy;
		fprogress.w = midx + Application.Cols % 2;
		fprogress.h = midy;
	}

	static Label status_progress;
	static Label status_state;
	static Label status_peers;
	static Label status_tracker;
	static Label status_up, status_up_speed;
	static Label status_down, status_down_speed;
	static Label status_warnings, status_failures;
	static Label torrent_name;
	
	static Frame SetupStatus ()
	{
		Frame fstatus = new Frame ("Status");
		int y = 0;
		int x = 13;
		string init = "<init>";
		
		fstatus.Add (status_progress = new Label (x, y, "0%"));
		status_progress.Color = status_progress.ColorHotNormal;
		fstatus.Add (new Label (1, y++, "Progress:"));
		
		fstatus.Add (status_state = new Label (x, y, init));
		fstatus.Add (new Label (1, y++, "State:"));

		fstatus.Add (status_peers = new Label (x, y, init));
		fstatus.Add (new Label (1, y++, "Peers:"));

		fstatus.Add (status_tracker = new Label (x, y, init));
		fstatus.Add (new Label (1, y++, "Tracker: "));
		y++;

		fstatus.Add (new Label (1, y++, "Upload:"));
		fstatus.Add (new Label (16, y, "KB   Speed: "));
		fstatus.Add (status_up = new Label (1, y, init));
		fstatus.Add (status_up_speed = new Label (28, y, init));
		y++;
		fstatus.Add (new Label (1, y++, "Download:"));
		fstatus.Add (new Label (16, y, "KB   Speed: "));
		fstatus.Add (status_down = new Label (1, y, init));
		fstatus.Add (status_down_speed = new Label (28, y, init));
		y += 2;
		fstatus.Add (status_warnings = new Label (11, y, init));
		fstatus.Add (new Label (1, y++, "Warnings: "));
		fstatus.Add (status_failures = new Label (11, y, init));
		fstatus.Add (new Label (1, y++, "Failures: "));
		y += 2;

		return fstatus;
	}

	static void UpdateStatus ()
	{
		TorrentManager tm = torrent_list.GetSelected ();
		if (tm == null)
			return;

		status_progress.Text   = String.Format ("{0:0.00}%", tm.Progress);
		status_state.Text      = tm.State.ToString ();
		status_peers.Text      = String.Format ("{0} ({1}/{2})", tm.Peers.Available, tm.Peers.Seeds, tm.Peers.Leechs);
		status_tracker.Text    = tm.TrackerManager.TrackerTiers[0].Trackers[0].State.ToString ();

		status_up.Text         = String.Format ("{0,14:N0}", tm.Monitor.DataBytesUploaded / 1024.0);
		status_up_speed.Text   = String.Format ("{0:0.0}", tm.Monitor.UploadSpeed / 1024.0);
		status_down.Text       = String.Format ("{0,14:N0}", tm.Monitor.DataBytesDownloaded / 1024.0);
		status_down_speed.Text = String.Format ("{0:0.0}", tm.Monitor.DownloadSpeed / 1024.0);

		torrent_name.Text      = tm.Torrent.Name;
	}
	
	static void Shutdown ()
	{
		Console.WriteLine ("Shutting down");
		WaitHandle[] handles = engine.StopAll();
		for (int i = 0; i < handles.Length; i++)
			if (handles[i] != null)
				handles[i].WaitOne();
		torrent_list.SaveFastResume ();
		engine.Dispose ();
		Console.WriteLine ("Shut down");
	}
	
	static void RunGui ()
	{
		Container a = new Container (0, 0, Application.Cols, Application.Lines);

		Frame ftorrents = new Frame (0,  0, 0, 0, "Torrents");
		a.Add (ftorrents);

		// Add
		Button badd = new Button (1, 1, "Add");
		badd.Clicked += delegate { AddDialog (); };
		ftorrents.Add (badd);

		// Options
		Button boptions = new Button (9, 1, "Options");
		boptions.Clicked += delegate { OptionsDialog (); };
		ftorrents.Add (boptions);

		// Quit
		Button bquit = new Button (21, 1, "Quit");
		bquit.Clicked += delegate {
			// FIXME: shut down torrent here
			a.Running = false;
		};
		ftorrents.Add (bquit);
		
		// Random widget tests
		//f.Add (new Label (7,  3, "Name:"));
		//f.Add (new Entry (13, 3, 20, "First"));
		
		//f.Add (new Label (4,  5, "Address:"));
		//f.Add (new Entry (13, 5, 20, "Second"));

		ListView ltorrents = new ListView (1, 5, 0, 0, torrent_list);
		ltorrents.Fill = Fill.Horizontal | Fill.Vertical;
		ftorrents.Add (ltorrents);
		
		Frame fprogress = new Frame ("Messages");
		fprogress.Add (log_widget = new LogWidget (0, 0));
		a.Add (fprogress);

		// For testing focus, not ready
		//f.Add (new Label (0, 0, "->0<-"));
		//f.Add (new Entry  (7, 0, 20, "Another"));


		// Details
		Frame fdetails = new Frame ("Details");
		fdetails.Add (new Label (1, 1, "Files for: "));
		torrent_name = new TrimLabel (12, 1, 10, "");
		torrent_name.Fill = Fill.Horizontal;
		fdetails.Add (torrent_name);
			      
		details_list = new TorrentDetailsList ();
		list_details = new ListView (1, 3, 0, 0, details_list);
		list_details.Fill = Fill.Horizontal | Fill.Vertical;
		fdetails.Add (list_details);
		
		a.Add (fdetails);

		// Status
		Frame fstatus = SetupStatus ();
		a.Add (fstatus);

		iteration = new Label (35, 0, "0");
		fstatus.Add (iteration);
		
		Application.Timeout = 1000;

		Application.Iteration += delegate {
			iteration.Text = (it++).ToString ();
			UpdateStatus ();
			lock (queue){
				if (queue.Count > 0){
					foreach (string s in queue){
						log_widget.AddText (s);
					}
					queue.Clear ();
				}
			}
			Application.Refresh ();
		};
		
		LayoutDialogs (ftorrents, fstatus, fdetails, fprogress);
		a.SizeChangedEvent += delegate {
			LayoutDialogs (ftorrents, fstatus, fdetails, fprogress);
		};

		UpdateStatus ();
		Application.Run (a);
	}
	static int it;
	
	static void SetupDefaults ()
	{
		config = new Config ();
		config.ListenPort = 9876;
		config.UploadSpeed = 0;
		config.DownloadSpeed = 0;
		config.Torrents = new ArrayList ();
		config.UploadSlots = 5;
		config.MaxConnections = 50;
		config.DownloadDir = Path.Combine (Environment.GetFolderPath (
							   Environment.SpecialFolder.Personal), "downloads");
		
		try {
			Directory.CreateDirectory (config.DownloadDir);
		} catch {
			config.DownloadDir = null;
		}
	}
	
	static void LoadSettings ()
	{
		if (!File.Exists (config_file)){
			try {
				Directory.CreateDirectory (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData));
			} catch {
				return;
			}

			SetupDefaults ();
			return;
		}
		
		try {
			config = (Config)
				new XmlSerializer (typeof (Config), new Type [] { typeof (TorrentDesc) })
					.Deserialize (new XmlTextReader (config_file));
		} catch {
			SetupDefaults ();
		}

		if (config.UploadSpeed < 0)
			config.UploadSpeed = 0;
		if (config.DownloadSpeed < 0)
			config.DownloadSpeed = 0;
		if (config.UploadSlots < 0)
			config.UploadSlots = 1;
		if (config.MaxConnections < 0)
			config.MaxConnections = 1;
		if (!Directory.Exists (config.DownloadDir)){
			try {
				Directory.CreateDirectory (config.DownloadDir);
			} catch {
				Application.Error ("Error",
						   "Problem with the configured download directory:\n\n" +
						   config.DownloadDir + "\n\n" +
						   "Create the directory, or edit your ~/.config/monotorrent.config file");
				Environment.Exit (1);
			}
		}
	}

	static void SaveSettings ()
	{
		config.Torrents = torrent_list.GetPathNames ();
		
		try {
			File.Delete (config_file);
			using (FileStream f = File.Create (config_file)){
				XmlTextWriter tw = new XmlTextWriter (new StreamWriter (f));
				tw.Formatting = Formatting.Indented;
				
				new XmlSerializer (typeof (Config), new Type [] { typeof (TorrentDesc) }).
					Serialize (tw, config);
			}
		} catch (Exception e){
			Application.Error ("Exception", e.ToString ());
		}
	}

        static void cm_PeerDisconnected(object sender, PeerConnectionEventArgs e)
        {
		if (e.PeerID == null)
			return;
		lock (queue){
			queue.Add (String.Format("{0} peer at {1}. Reason: {2}",
                    		e.ConnectionDirection == Direction.Incoming ? "Got disconnected from" : "Disconnected from",
                    		e.PeerID.Uri, e.Message));
		}
        }

        static void cm_PeerConnected(object sender, PeerConnectionEventArgs e)
        {
		if (e.PeerID == null)
			return;

		lock (queue){
			queue.Add (String.Format("{0} peer at {1}",
				e.ConnectionDirection == Direction.Incoming ? "Accepted connection from" : "Connected to",
				e.PeerID.Uri));

		}
        }

	static ArrayList queue;
	
	static void InitMonoTorrent ()
	{
		queue = new ArrayList ();
		engine_settings = new EngineSettings (config.DownloadDir, config.ListenPort);
		torrent_settings = new TorrentSettings (config.UploadSlots, config.MaxConnections,
							(int) config.UploadSpeed, (int) config.DownloadSpeed);
		
		engine = new ClientEngine (engine_settings);

		// Our store
		torrent_list = new TorrentList ();
		foreach (TorrentDesc td in config.Torrents){
			if (File.Exists (td.Filename)){
				torrent_list.Add (td.Filename);
			}
		}
	}
	
	static void Main ()
	{
		Application.Init (false);
		
		LoadSettings ();
		
		InitMonoTorrent ();
		RunGui ();
		SaveSettings ();
		Shutdown ();
	}

	[XmlRoot ("config")]
	public class Config {
		[XmlAttribute] public string DownloadDir;
		[XmlAttribute] public int ListenPort;
		[XmlAttribute] public int UploadSlots;
		[XmlAttribute] public int MaxConnections;
		[XmlAttribute] public int UploadSpeed;
		[XmlAttribute] public int DownloadSpeed;
		[XmlElement ("Torrent", typeof (TorrentDesc))] public ArrayList Torrents;
	}

	public class TorrentDesc {
		[XmlAttribute] public string Filename;
	}
}

