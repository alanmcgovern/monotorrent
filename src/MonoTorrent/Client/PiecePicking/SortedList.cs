using System;
using System.Collections;
using System.Collections.Generic;

namespace MonoTorrent.Client
{
    public class SortList<T> : IList<T>
    {
        private readonly List<T> list;

        public SortList()
        {
            list = new List<T>();
        }

        public SortList(IEnumerable<T> list)
        {
            this.list = new List<T>(list);
        }

        public int IndexOf(T item)
        {
            var index = list.BinarySearch(item);
            return index < 0 ? -1 : index;
        }

        public void Insert(int index, T item)
        {
            Add(item);
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        public T this[int index]
        {
            get { return list[index]; }
            set { list[index] = value; }
        }

        public void Add(T item)
        {
            var index = list.BinarySearch(item);
            if (index < 0)
                list.Insert(~index, item);
            else
                list.Insert(index, item);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(T item)
        {
            return list.BinarySearch(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            var index = list.BinarySearch(item);
            if (index < 0)
                return false;
            list.RemoveAt(index);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int BinarySearch(T piece, IComparer<T> comparer)
        {
            return list.BinarySearch(piece, comparer);
        }

        public bool Exists(Predicate<T> predicate)
        {
            return list.Exists(predicate);
        }

        public List<T> FindAll(Predicate<T> predicate)
        {
            return list.FindAll(predicate);
        }

        public void ForEach(Action<T> action)
        {
            list.ForEach(action);
        }

        public int RemoveAll(Predicate<T> predicate)
        {
            return list.RemoveAll(predicate);
        }
    }
}