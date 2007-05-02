

using System;
using System.Text;
using System.Collections;
using MonoTorrent.Client;
#if NET_2_0
using System.Collections.Generic;
#endif

namespace MonoTorrent
{
	public class TorrentManagerCollection : MonoTorrentCollectionBase
	{
		#region Private Fields

#if NET_2_0
		private List<TorrentManager> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public TorrentManagerCollection()
		{
#if NET_2_0
			list = new List<TorrentManager>();
#else
			list = new ArrayList();
#endif
		}

		public TorrentManagerCollection(int capacity)
		{
#if NET_2_0
			list = new List<TorrentManager>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public TorrentManager this[int index]
		{
			get { return (TorrentManager)list[index]; }
			set { list[index] = value; }
		}

		public int Add(TorrentManager value)
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
			TorrentManagerCollection clone = new TorrentManagerCollection(list.Count);
			for (int i = 0; i < list.Count; i++)
				clone.Add(this[i]);
			return clone;
		}

		public bool Contains(TorrentManager value)
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

		public int IndexOf(TorrentManager value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, TorrentManager value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(TorrentManager value)
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
			return Add((TorrentManager)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((TorrentManager)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((TorrentManager)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (TorrentManager)value);
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
			Remove((TorrentManager)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (TorrentManager)value; }
		}

		#endregion
	}
}
