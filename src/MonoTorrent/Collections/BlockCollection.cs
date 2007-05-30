using System;
using System.Text;
using System.Collections;
using MonoTorrent.Client;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
    public class BlockCollection : MonoTorrentCollectionBase
    {
        #region Private Fields

#if NET_2_0
        private List<Block> list;
#else
        private ArrayList list;
#endif

        #endregion Private Fields


        #region Constructors

        public BlockCollection()
        {
#if NET_2_0
            list = new List<Block>();
#else
            list = new ArrayList();
#endif
        }

        public BlockCollection(int capacity)
        {
#if NET_2_0
            list = new List<Block>(capacity);
#else
            list = new ArrayList(capacity);
#endif
        }

        #endregion


        #region Methods

        public Block this[int index]
        {
            get { return (Block)list[index]; }
            set { list[index] = value; }
        }

        public int Add(Block value)
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
            BlockCollection clone = new BlockCollection(list.Count);
            for (int i = 0; i < list.Count; i++)
                clone.Add(this[i]);
            return clone;
        }

        public bool Contains(Block value)
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

        public int IndexOf(Block value)
        {
            return list.IndexOf(value);
        }

        public void Insert(int index, Block value)
        {
            list.Insert(index, value);
        }

        public bool IsSynchronized
        {
            get { return ((IList)list).IsSynchronized; }
        }

        public void Remove(Block value)
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
            return Add((Block)value);
        }

        int IList.IndexOf(object value)
        {
            return IndexOf((Block)value);
        }

        bool IList.Contains(object value)
        {
            return Contains((Block)value);
        }

        void IList.Insert(int index, object value)
        {
            Insert(index, (Block)value);
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
            Remove((Block)value);
        }

        object IList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = (Block)value; }
        }

        #endregion
    }
}