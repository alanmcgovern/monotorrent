using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client.Messages.Libtorrent
{
    public struct ExtensionSupport
    {
        private byte messageId;
        private string name;

        public byte MessageId
        {
            get { return messageId; }
        }

        public string Name
        {
            get { return name; }
        }

        public ExtensionSupport(string name, byte messageId)
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
