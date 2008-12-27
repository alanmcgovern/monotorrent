using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.Client
{
    public class SortedPieces : IList<Piece>
    {
        private List<Piece> list;

        public SortedPieces(IEnumerable<Piece> list)
        {
            this.list = new List<Piece>(list);
        }

        public int BinarySearch(Piece piece, IComparer<Piece> comparer)
        {
            return list.BinarySearch(piece, comparer);
        }

        public bool Exists(Predicate<Piece> predicate)
        {
            return list.Exists(predicate);
        }

        public int IndexOf(Piece item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, Piece item)
        {
            list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            list.RemoveAt(index);
        }

        public Piece this[int index]
        {
            get { return list[index]; }
            set { list[index] = value; }
        }

        public void Add(Piece item)
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

        public bool Contains(Piece item)
        {
            return list.BinarySearch(item) != -1;
        }

        public void CopyTo(Piece[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return list.Count; }
        }

        public void ForEach(Action<Piece> action)
        {
            list.ForEach(action);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(Piece item)
        {
            int index = list.BinarySearch(item);
            if (index < 0)
                return false;
            list.RemoveAt(index);
            return true;
        }

        public int RemoveAll(Predicate<Piece> predicate)
        {
            return list.RemoveAll(predicate);
        }

        public IEnumerator<Piece> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void BinarySearch()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
