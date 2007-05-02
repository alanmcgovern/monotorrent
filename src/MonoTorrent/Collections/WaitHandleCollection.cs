using System;
using System.Text;
using System.Collections;
using System.Threading;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
	public class WaitHandleCollection : MonoTorrentCollectionBase
	{
		#region Private Fields

#if NET_2_0
		private List<WaitHandle> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public WaitHandleCollection()
		{
#if NET_2_0
			list = new List<WaitHandle>();
#else
			list = new ArrayList();
#endif
		}

		public WaitHandleCollection(int capacity)
		{
#if NET_2_0
			list = new List<WaitHandle>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public WaitHandle this[int index]
		{
			get { return (WaitHandle)list[index]; }
			set { list[index] = value; }
		}

		public int Add(WaitHandle value)
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
			WaitHandleCollection clone = new WaitHandleCollection(list.Count);
			for (int i = 0; i < list.Count; i++)
				clone.Add(this[i]);
			return clone;
		}

		public bool Contains(WaitHandle value)
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

		public int IndexOf(WaitHandle value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, WaitHandle value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(WaitHandle value)
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
			return Add((WaitHandle)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((WaitHandle)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((WaitHandle)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (WaitHandle)value);
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
			Remove((WaitHandle)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (WaitHandle)value; }
		}

		#endregion

		internal WaitHandle[] ToArray()
		{
			return (WaitHandle[])list.ToArray();
		}
	}
}