using System;

namespace MonoTorrent.Dht
{
    internal class TaskCompleteEventArgs : EventArgs
    {
    	public TaskCompleteEventArgs(bool succeed) : base()
    	{
    		this.succeed = succeed;
    	}
    	private bool succeed;
    	
    	public bool IsSucceed
    	{
    		get {return succeed;}
    	}
    }
}