//
// SortedList.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2008 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class SortList<T> : IList<T>
    {
        private List<T> list;

        public SortList()
        {
            list = new List<T>();
        }
        public SortList(IEnumerable<T> list)
        {
            this.list = new List<T>(list);
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

        public int IndexOf(T item)
        {
            int index = list.BinarySearch(item);
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
            int index = list.BinarySearch(item);
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

        public void ForEach(Action<T> action)
        {
            list.ForEach(action);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            int index = list.BinarySearch(item);
            if (index < 0)
                return false;
            list.RemoveAt(index);
            return true;
        }

        public int RemoveAll(Predicate<T> predicate)
        {
            return list.RemoveAll(predicate);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
