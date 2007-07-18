using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent
{
    public class MonoTorrentCollection<T> : List<T>
    {
        public MonoTorrentCollection()
            : base()
        {

        }

        public MonoTorrentCollection(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public MonoTorrentCollection(int capacity)
            : base(capacity)
        {
        }

        public MonoTorrentCollection<T> Clone()
        {
            MonoTorrentCollection<T> clone = new MonoTorrentCollection<T>(base.Count);
            clone.AddRange(this);
            return clone;
        }
    }
}
