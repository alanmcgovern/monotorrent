using System;
namespace MonoTorrent.Client
{
    public interface IPeerListener : IListener
    {
        event EventHandler<NewConnectionEventArgs> ConnectionReceived;
    }
}
