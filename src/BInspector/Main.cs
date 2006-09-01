// project created on 10.08.2006 at 23:00
using System;
using Gtk;

namespace BInspector
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init ();
			MainWindow win = new MainWindow ();
			win.Show ();
			Application.Run ();
		}
	}
}