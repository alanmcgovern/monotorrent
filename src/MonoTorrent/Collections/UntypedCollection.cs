/*

using System;
using System.Text;
using System.Collections;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
	public class @TYPE@Collection : IList
	{
		#region Private Fields

#if NET_2_0
		private List<@TYPE@> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public @TYPE@Collection()
		{
#if NET_2_0
			list = new List<@TYPE@>();
#else
			list = new ArrayList();
#endif
		}

		public @TYPE@Collection(int capacity)
		{
#if NET_2_0
			list = new List<@TYPE@>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public @TYPE@ this[int index]
		{
			get { return (@TYPE@)list[index]; }
			set { list[index] = value; }
		}

		public int Add(@TYPE@ value)
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

		public bool Contains(@TYPE@ value)
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

		public int IndexOf(@TYPE@ value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, @TYPE@ value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(@TYPE@ value)
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
			return Add((@TYPE@)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((@TYPE@)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((@TYPE@)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (@TYPE@)value);
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
			Remove((@TYPE@)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (@TYPE@)value; }
		}

		#endregion
	}
}

*/
