using System;
using System.Text;
using System.Collections;
#if NET_2_0
using System.Collections.Generic;
#endif

using MonoTorrent.Common;

namespace MonoTorrent
{
	public class ITorrentWatcherCollection : IList
	{
		#region Private Fields

#if NET_2_0
		private List<ITorrentWatcher> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public ITorrentWatcherCollection()
		{
#if NET_2_0
			list = new List<ITorrentWatcher>();
#else
			list = new ArrayList();
#endif
		}

		public ITorrentWatcherCollection(int capacity)
		{
#if NET_2_0
			list = new List<ITorrentWatcher>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public ITorrentWatcher this[int index]
		{
			get { return (ITorrentWatcher)list[index]; }
			set { list[index] = value; }
		}

		public int Add(ITorrentWatcher value)
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

		public bool Contains(ITorrentWatcher value)
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

		public int IndexOf(ITorrentWatcher value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, ITorrentWatcher value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(ITorrentWatcher value)
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
			return Add((ITorrentWatcher)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((ITorrentWatcher)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((ITorrentWatcher)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (ITorrentWatcher)value);
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
			Remove((ITorrentWatcher)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (ITorrentWatcher)value; }
		}

		#endregion
	}
}