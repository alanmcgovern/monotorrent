using System;
using System.Text;
using System.Collections;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
	public class UInt32Collection : IList
	{
		#region Private Fields

#if NET_2_0
		private List<UInt32> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public UInt32Collection()
		{
#if NET_2_0
			list = new List<UInt32>();
#else
			list = new ArrayList();
#endif
		}

		public UInt32Collection(int capacity)
		{
#if NET_2_0
			list = new List<UInt32>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public UInt32 this[int index]
		{
			get { return (UInt32)list[index]; }
			set { list[index] = value; }
		}

		public int Add(UInt32 value)
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

		public bool Contains(UInt32 value)
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

		public int IndexOf(UInt32 value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, UInt32 value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(UInt32 value)
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
			return Add((UInt32)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((UInt32)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((UInt32)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (UInt32)value);
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
			Remove((UInt32)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (UInt32)value; }
		}

		#endregion
	}
}