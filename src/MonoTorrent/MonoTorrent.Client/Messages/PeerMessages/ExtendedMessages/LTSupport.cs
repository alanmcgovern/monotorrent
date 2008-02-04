using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Messages.PeerMessages
{
    public struct LTSupport
    {
        private byte messageId;
        private string name;

        public int MessageId
        {
            get { return messageId; }
        }

        public string Name
        {
            get { return name; }
        }

        public LTSupport(string name, byte messageId)
        {
            this.messageId = messageId;
            this.name = name;
        }

        public override string ToString()
        {
            return string.Format("{1}: {0}", name, messageId);
        }
    }
}
