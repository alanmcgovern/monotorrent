using System;
using System.Text;
using System.Collections;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
    public class IntCollection : MonoTorrentCollectionBase
    {
        #region Private Fields

#if NET_2_0
        private List<int> list;
#else
        private ArrayList list;
#endif

        #endregion Private Fields


        #region Constructors

        public IntCollection()
        {
#if NET_2_0
            list = new List<int>();
#else
            list = new ArrayList();
#endif
        }

        public IntCollection(int capacity)
        {
#if NET_2_0
            list = new List<int>(capacity);
#else
            list = new ArrayList(capacity);
#endif
        }

        #endregion


        #region Methods

        public int this[int index]
        {
            get { return (int)list[index]; }
            set { list[index] = value; }
        }

        public int Add(int value)
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
            IntCollection clone = new IntCollection(list.Count);
            for (int i = 0; i < list.Count; i++)
                clone.Add(this[i]);
            return clone;
        }

        public bool Contains(int value)
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

        public int IndexOf(int value)
        {
            return list.IndexOf(value);
        }

        public void Insert(int index, int value)
        {
            list.Insert(index, value);
        }

        public bool IsSynchronized
        {
            get { return ((IList)list).IsSynchronized; }
        }

        public void Remove(int value)
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
            return Add((int)value);
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((int)value);
        }

        bool IList.Contains(object value)
        {
            return Contains((int)value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (int)value);
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
            Remove((int)value);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (int)value; }
        }

        #endregion
    }
}