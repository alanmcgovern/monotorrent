using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent.Common
{
    public class MonoTorrentCollection<T> : List<T>, ICloneable
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

        object ICloneable.Clone()
        {
            return Clone();
        }

        public MonoTorrentCollection<T> Clone()
        {
            return new MonoTorrentCollection<T>(this);
        }

        public T Dequeue()
        {
            T result = this[0];
            RemoveAt(0);
            return result;
        }
    }
}
