using System;
using System.Text;
using System.Collections;
#if NET_2_0
using System.Collections.Generic;
#endif

using MonoTorrent.Client;

namespace MonoTorrent
{
	public class PeerConnectionIDCollection : IList
	{
		#region Private Fields

#if NET_2_0
		private List<PeerId> list;
#else
		private ArrayList list;
#endif

		#endregion Private Fields


		#region Constructors

		public PeerConnectionIDCollection()
		{
#if NET_2_0
			list = new List<PeerId>();
#else
			list = new ArrayList();
#endif
		}

		public PeerConnectionIDCollection(int capacity)
		{
#if NET_2_0
			list = new List<PeerId>(capacity);
#else
			list = new ArrayList(capacity);
#endif
		}

		#endregion


		#region Methods

		public PeerId this[int index]
		{
			get { return (PeerId)list[index]; }
			set { list[index] = value; }
		}

		public int Add(PeerId value)
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

		public bool Contains(PeerId value)
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

		public int IndexOf(PeerId value)
		{
			return list.IndexOf(value);
		}

		public void Insert(int index, PeerId value)
		{
			list.Insert(index, value);
		}

		public bool IsSynchronized
		{
			get { return ((IList)list).IsSynchronized; }
		}

		public void Remove(PeerId value)
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
			return Add((PeerId)value);
		}

		int IList.IndexOf(object value)
		{
			return IndexOf((PeerId)value);
		}

		bool IList.Contains(object value)
		{
			return Contains((PeerId)value);
		}

		void IList.Insert(int index, object value)
		{
			Insert(index, (PeerId)value);
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
			Remove((PeerId)value);
		}

		object IList.this[int index]
		{
			get { return this[index]; }
			set { this[index] = (PeerId)value; }
		}

		#endregion
	}
}