using System;
using System.Text;
using System.Collections;
using MonoTorrent.BEncoding;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
    public class IBEncodedValueCollection : MonoTorrentCollectionBase
    {
        #region Private Fields

#if NET_2_0
        protected List<BEncodedValue> list;
#else
        protected ArrayList list;
#endif

        #endregion Private Fields


        #region Constructors

        public IBEncodedValueCollection()
        {
#if NET_2_0
            list = new List<BEncodedValue>();
#else
            list = new ArrayList();
#endif
        }

        public IBEncodedValueCollection(int capacity)
        {
#if NET_2_0
            list = new List<BEncodedValue>(capacity);
#else
            list = new ArrayList(capacity);
#endif
        }

        #endregion


        #region Methods

        public BEncodedValue this[int index]
        {
            get { return (BEncodedValue)list[index]; }
            set { list[index] = value; }
        }

        public int Add(BEncodedValue value)
        {
#if NET_2_0
            list.Add(value);
            return list.Count;
#else
            return list.Add(value);
#endif
        }

        public void Clear()
        {
            this.list.Clear();
        }

        public MonoTorrentCollectionBase Clone()
        {
            IBEncodedValueCollection clone = new IBEncodedValueCollection(list.Count);
            for (int i = 0; i < list.Count; i++)
                clone.Add(this[i]);
            return clone;
        }

        public bool Contains(BEncodedValue value)
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

        public int IndexOf(BEncodedValue value)
        {
            return list.IndexOf(value);
        }

        public void Insert(int index, BEncodedValue value)
        {
            list.Insert(index, value);
        }

        public bool IsSynchronized
        {
            get { return ((IList)list).IsSynchronized; }
        }

        public void Remove(BEncodedValue value)
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
            return Add((BEncodedValue)value);
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((BEncodedValue)value);
        }

        bool IList.Contains(object value)
        {
            return Contains((BEncodedValue)value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (BEncodedValue)value);
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
            Remove((BEncodedValue)value);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (BEncodedValue)value; }
        }

        #endregion
    }
}