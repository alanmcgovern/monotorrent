using MonoTorrent.Dht.Messages;
using System;

namespace MonoTorrent.Dht
{
    internal abstract class Task<T> where T : TaskCompleteEventArgs
    {
    	private bool active;

    	public Task()
    	{
            active = true;
   	}

        public abstract void Execute ();

    	public event EventHandler<T> Completed;

        public void Cancel ()
    	{
            active = false;
            Complete(new TaskCompleteEventArgs(false));
    	}
    	
    	internal void Complete<T>(T e)
    	{
    		active = false;
    		if (Completed != null)
    			Completed(this, e);
    	}

        public bool Active { get {return active;} }	
    }
}

