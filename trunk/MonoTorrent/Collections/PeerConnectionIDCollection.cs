

using System;
using System.Text;
using System.Collections;
using MonoTorrent.Client;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
    public class PeerIdCollection : MonoTorrentCollectionBase
    {
        #region Private Fields

#if NET_2_0
        private List<PeerId> list;
#else
        private ArrayList list;
#endif

        #endregion Private Fields


        #region Constructors

        public PeerIdCollection()
        {
#if NET_2_0
            list = new List<PeerId>();
#else
            list = new ArrayList();
#endif
        }

        public PeerIdCollection(int capacity)
        {
#if NET_2_0
            list = new List<PeerId>(capacity);
#else
            list = new ArrayList(capacity);
#endif
        }

        #endregion


        #region Methods

        public PeerId this[int index]
        {
            get { return (PeerId)list[index]; }
            set { list[index] = value; }
        }

        public int Add(PeerId value)
        {
#if NET_2_0
            list.Add(value);
            return list.Count;
#else
            return this.list.Add(value);
#endif
        }

        public void Clear()
        {
            this.list.Clear();
        }

        public MonoTorrentCollectionBase Clone()
        {
            PeerIdCollection clone = new PeerIdCollection(list.Count);
            for (int i = 0; i < list.Count; i++)
                clone.Add(this[i]);
            return clone;
        }

        public bool Contains(PeerId value)
        {
            return list.Contains(value);
        }

        public void CopyTo(Array array, int index)
        {
            ((IList)list).CopyTo(array, index);
        }

        public int Count
        {
            get { return list.Count; }
        }

        public IEnumerator GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public int IndexOf(PeerId value)
        {
            return list.IndexOf(value);
        }

        public void Insert(int index, PeerId value)
        {
            list.Insert(index, value);
        }

        public bool IsSynchronized
        {
            get { return ((IList)list).IsSynchronized; }
        }

        public void Remove(PeerId value)
        {
            list.Remove(value);
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        public object SyncRoot
        {
            get { return ((IList)list).SyncRoot; }
        }

        #endregion Methods


        #region Explicit Implementation

        int IList.Add(object value)
        {
            return Add((PeerId)value);
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((PeerId)value);
        }

        bool IList.Contains(object value)
        {
            return Contains((PeerId)value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (PeerId)value);
        }

        bool IList.IsFixedSize
        {
            get { return ((IList)list).IsFixedSize; }
        }

        bool IList.IsReadOnly
        {
            get { return ((IList)list).IsReadOnly; }

        }

        void IList.Remove(object value)
        {
            Remove((PeerId)value);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (PeerId)value; }
        }

        #endregion
    }
}
