using System;
using System.Text;
using System.Collections;

using MonoTorrent.BEncoding;

#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
	public class IBEncodedValueCollection : IList
	{
		#region Private Fields

#if NET_2_0
		private List<IBEncodedValue> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public IBEncodedValueCollection()
		{
#if NET_2_0
			list = new List<IBEncodedValue>();
#else
			list = new ArrayList();
#endif
		}

		public IBEncodedValueCollection(int capacity)
		{
#if NET_2_0
			list = new List<IBEncodedValue>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public IBEncodedValue this[int index]
		{
			get { return (IBEncodedValue)list[index]; }
			set { list[index] = value; }
		}

		public int Add(IBEncodedValue value)
		{
#if NET_2_0
			list.Add(value);
			return 0;
#else
			return this.list.Add(value);
#endif
		}

		public void Clear()
		{
			this.list.Clear();
		}

		public bool Contains(IBEncodedValue value)
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

		public int IndexOf(IBEncodedValue value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, IBEncodedValue value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(IBEncodedValue value)
		{
			list.Remove(value);
		}

		public void RemoveAt(int index)
		{
			RemoveAt(index);
		}

		public object SyncRoot
		{
			get { return ((IList)list).SyncRoot; }
		}

		#endregion Methods


		#region Explicit Implementation

		int IList.Add(object value)
		{
			return Add((IBEncodedValue)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((IBEncodedValue)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((IBEncodedValue)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (IBEncodedValue)value);
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
			Remove((IBEncodedValue)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (IBEncodedValue)value; }
		}

		#endregion
    }
}