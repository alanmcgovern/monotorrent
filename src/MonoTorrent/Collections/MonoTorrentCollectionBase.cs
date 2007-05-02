using System;
using System.Text;
using System.Collections;

namespace MonoTorrent
{
	public interface MonoTorrentCollectionBase : IList
	{
        MonoTorrentCollectionBase Clone();
	}
}
