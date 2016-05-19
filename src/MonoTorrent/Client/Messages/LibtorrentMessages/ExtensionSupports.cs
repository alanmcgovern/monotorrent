using System.Collections.Generic;
using MonoTorrent.Client.Messages.Libtorrent;

namespace MonoTorrent.Client
{
    public class ExtensionSupports : List<ExtensionSupport>
    {
        public ExtensionSupports()
        {
        }

        public ExtensionSupports(IEnumerable<ExtensionSupport> collection)
            : base(collection)
        {
        }

        public bool Supports(string name)
        {
            for (var i = 0; i < Count; i++)
                if (this[i].Name == name)
                    return true;
            return false;
        }

        internal byte MessageId(ExtensionSupport support)
        {
            for (var i = 0; i < Count; i++)
                if (this[i].Name == support.Name)
                    return this[i].MessageId;

            throw new MessageException(string.Format("{0} is not supported by this peer", support.Name));
        }
    }
}