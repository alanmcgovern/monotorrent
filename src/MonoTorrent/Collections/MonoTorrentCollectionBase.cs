using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent
{
    public class MonoTorrentCollection<T> : IList<T>
    {
        private List<T> list;
        private bool readOnly;

        public MonoTorrentCollection()
        {
            list = new List<T>();
        }

        public MonoTorrentCollection(IEnumerable<T> collection)
        {
            list = new List<T>(collection);
        }

        public MonoTorrentCollection(int capacity)
        {
            list = new List<T>(capacity);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            list.AddRange(collection);
        }

        public MonoTorrentCollection<T> Clone()
        {
            MonoTorrentCollection<T> clone = new MonoTorrentCollection<T>(Count);
            clone.AddRange(this);
            return clone;
        }

        public T Dequeue()
        {
            T result = this[0];
            RemoveAt(0);
            return result;
        }

        public int IndexOf(T item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            Insert(index, item, false);
        }
        internal void Insert(int index, T item, bool ignoreReadonly)
        {
            if (!ignoreReadonly)
                CheckReadonly();
            list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            RemoveAt(index, false);
        }

        public void RemoveAll(Predicate<T> match)
        {
            list.RemoveAll(match);
        }

        internal void RemoveAt(int index, bool ignoreReadonly)
        {
            if (!ignoreReadonly)
                CheckReadonly();
            list.RemoveAt(index);
        }
        public T this[int index]
        {
            get  { return list[index]; }
            set
            {
                this[index, false] = value;
            }
        }

        internal T this[int index, bool ignoreReadonly]
        {
            get { return list[index]; }
            set
            {
                if (!ignoreReadonly)
                    CheckReadonly();
                list[index] = value;
            }
        }

        public void Add(T item)
        {
            Add(item, false);
        }

        internal void Add(T item, bool ignoreReadonly)
        {
            if (!ignoreReadonly)
                CheckReadonly();
            list.Add(item);
        }

        public void Clear()
        {
            Clear(false);
        }

        internal void Clear(bool ignoreReadonly)
        {
            if (!ignoreReadonly)
                CheckReadonly();
            list.Clear();
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return list.Count; ; }
        }

        public bool IsReadOnly
        {
            get { return readOnly; }
            internal set { readOnly = value; }
        }

        public bool Remove(T item)
        {
            return Remove(item, false);
        }

        internal bool Remove(T item, bool ignoreReadonly)
        {
            if (!ignoreReadonly)
                CheckReadonly();
            return list.Remove(item);
        }

        private void CheckReadonly()
        {
            if (readOnly)
                throw new InvalidOperationException("The operation is not allowed as the list is read only");
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)list).GetEnumerator();
        }
    }
}
