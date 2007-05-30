

using System;
using System.Text;
using System.Collections;
using System.Diagnostics;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
    public class TraceListenerCollection : MonoTorrentCollectionBase
    {
        #region Private Fields

#if NET_2_0
        private List<TraceListener> list;
#else
        private ArrayList list;
#endif

        #endregion Private Fields


        #region Constructors

        public TraceListenerCollection()
        {
#if NET_2_0
            list = new List<TraceListener>();
#else
            list = new ArrayList();
#endif
        }

        public TraceListenerCollection(int capacity)
        {
#if NET_2_0
            list = new List<TraceListener>(capacity);
#else
            list = new ArrayList(capacity);
#endif
        }

        #endregion


        #region Methods

        public TraceListener this[int index]
        {
            get { return (TraceListener)list[index]; }
            set { list[index] = value; }
        }

        public int Add(TraceListener value)
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
            TraceListenerCollection clone = new TraceListenerCollection(list.Count);
            for (int i = 0; i < list.Count; i++)
                clone.Add(this[i]);
            return clone;
        }

        public bool Contains(TraceListener value)
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

        public int IndexOf(TraceListener value)
        {
            return list.IndexOf(value);
        }

        public void Insert(int index, TraceListener value)
        {
            list.Insert(index, value);
        }

        public bool IsSynchronized
        {
            get { return ((IList)list).IsSynchronized; }
        }

        public void Remove(TraceListener value)
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
            return Add((TraceListener)value);
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((TraceListener)value);
        }

        bool IList.Contains(object value)
        {
            return Contains((TraceListener)value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (TraceListener)value);
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
            Remove((TraceListener)value);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (TraceListener)value; }
        }

        #endregion
    }
}
