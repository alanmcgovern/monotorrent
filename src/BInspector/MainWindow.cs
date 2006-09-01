using System;
using System.IO;
using System.Collections.Generic;
using Gtk;
using MonoTorrent.Common;

public class MainWindow: Gtk.Window
{
    protected Gtk.Entry entry1;
    protected Gtk.Button buttonChooser;
    protected Gtk.TreeView treeview;

    
    public MainWindow (): base ("")
    {
        Stetic.Gui.Build (this, typeof(MainWindow));
        treeview.Model = new TreeStore(typeof(string), typeof(string));
        CellRendererText col1 = new CellRendererText();        
        col1.Editable = true;
        CellRendererText col2 = new CellRendererText();
        col2.SetFixedSize(60, col2.Height);
        col2.Editable = true;
        treeview.AppendColumn ("Name", col1, "text", 0);
        treeview.AppendColumn ("Value", col2, "text", 1);        
    }
    
    protected void OnDeleteEvent (object sender, DeleteEventArgs a)
    {
        Application.Quit ();
        a.RetVal = true;
    }

    protected virtual void OnButtonChooserClicked(object sender, System.EventArgs e)
    {
       FileChooserDialog dialog = new FileChooserDialog(
                   "Open Torrent File",
                   this,
                   Gtk.FileChooserAction.Open,
                   Gtk.Stock.Cancel, Gtk.ResponseType.Cancel,
                   Gtk.Stock.Open, Gtk.ResponseType.Accept);
        dialog.Filter = new FileFilter();
        dialog.Filter.AddPattern("*.torrent");
        int response = dialog.Run();
        dialog.Hide();
        if (response == (int) Gtk.ResponseType.Accept) {
            Console.WriteLine(dialog.Filename);
            entry1.Text = dialog.Filename;//Path.GetFileName(dialog.Filename);            
            entry1.IsFocus = true;
            LoadTorrent(dialog.Filename);
        }
    }

    protected virtual void OnEntry1Activated(object sender, System.EventArgs e)
    {
       string path = entry1.Text;
       //entry1.Text = Path.GetFileName(entry1.Text);
       LoadTorrent(path);
    }
    
    private void LoadTorrent(string path)
    {      
        BEncodedDictionary dict = BEncode.Decode(new FileStream(path, FileMode.Open)) as BEncodedDictionary;
        TreeStore model = treeview.Model as TreeStore;
        TreeIter iter = model.AppendNode();        
        AddSubs(iter, dict);
    }
    
    public void AddSubs(TreeIter iter, IDictionary<BEncodedString, IBEncodedValue> dict)
    {
        TreeStore model = treeview.Model as TreeStore;
        foreach (KeyValuePair<BEncodedString, IBEncodedValue> kvp in dict) {
            BEncodedDictionary bdict = kvp.Value as BEncodedDictionary;
            BEncodedList blist = kvp.Value as BEncodedList;
            if (bdict != null) {
                AddSubs(model.AppendNode(iter), bdict); 
                continue;
            }
            if (blist != null) {
                AddSubs(model.AppendNode(iter), blist);
                continue;
            }
            string key = kvp.Key.ToString();
            string value = kvp.Value.ToString();
            model.AppendValues(iter, key, value);
        }
    }
    
    public void AddSubs(TreeIter iter, IList<IBEncodedValue> list)
    {
        TreeStore model = treeview.Model as TreeStore;
        foreach (IBEncodedValue bvalue in list) {
            BEncodedDictionary bdict = bvalue as BEncodedDictionary;
            BEncodedList blist = bvalue as BEncodedList;
            if (bdict != null) {
                AddSubs(model.AppendNode(iter), bdict); 
                continue;
            }
            if (blist != null) {
                AddSubs(model.AppendNode(iter), blist);
                continue;
            }            
            string value = bvalue.ToString();
            model.AppendValues(iter, "", value);
        }
    }
}