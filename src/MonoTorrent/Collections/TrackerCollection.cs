using System;
using System.Text;
using System.Collections;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
	public class TrackerCollection : IList
	{
		#region Private Fields

#if NET_2_0
		private List<MonoTorrent.Client.Tracker> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public TrackerCollection()
		{
#if NET_2_0
			list = new List<MonoTorrent.Client.Tracker>();
#else
			list = new ArrayList();
#endif
		}

		public TrackerCollection(int capacity)
		{
#if NET_2_0
			list = new List<MonoTorrent.Client.Tracker>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public MonoTorrent.Client.Tracker this[int index]
		{
			get { return (MonoTorrent.Client.Tracker)list[index]; }
			set { list[index] = value; }
		}

		public int Add(MonoTorrent.Client.Tracker value)
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

		public bool Contains(MonoTorrent.Client.Tracker value)
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

		public int IndexOf(MonoTorrent.Client.Tracker value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, MonoTorrent.Client.Tracker value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(MonoTorrent.Client.Tracker value)
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
			return Add((MonoTorrent.Client.Tracker)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((MonoTorrent.Client.Tracker)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((MonoTorrent.Client.Tracker)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (MonoTorrent.Client.Tracker)value);
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
			Remove((MonoTorrent.Client.Tracker)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (MonoTorrent.Client.Tracker)value; }
		}

		#endregion

		internal TrackerCollection Clone()
		{
			TrackerCollection clone = new TrackerCollection(list.Count);
			for (int i = 0; i < list.Count; i++)
				clone.Add(this[i]);

			return clone;
		}
	}
}

