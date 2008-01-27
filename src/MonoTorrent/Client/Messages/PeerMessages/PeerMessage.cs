using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Messages
{
    public abstract class PeerMessage : Message
    {
        internal abstract void Handle(PeerIdInternal id);
    }
}
