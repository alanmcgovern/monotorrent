using System;
using System.Diagnostics;

namespace GlacialComponents.Controls
{
	/// <summary>
	/// Direction of column sorting
	/// </summary>
	public enum SortDirections { 
		/// <summary>
		/// Ascending Items
		/// </summary>
		SortAscending, 
		/// <summary>
		/// Descending Items
		/// </summary>
		SortDescending 
	}

	/// <summary>
	/// Summary description for GLQuickSort.
	/// </summary>
	internal class GLQuickSort
	{
		public GLQuickSort()
		{
			//
			// TODO: Add constructor logic here
			//
		}


		private enum CompareDirection { GreaterThan, LessThan };


		/// <summary>
		/// compare only numeric values in items.  Warning, this can end up slowing down routine quite a bit
		/// </summary>
		private bool m_bNumericCompare = false;
		public bool NumericCompare
		{
			get { return m_bNumericCompare; }
			set { m_bNumericCompare = value; }
		}

		/// <summary>
		/// Stop this sort before it finishes
		/// </summary>
		private bool m_bStopRequested = false;
		public bool StopRequested
		{
			get { return m_bStopRequested; }
			set { m_bStopRequested = value; }
		}


		/// <summary>
		/// Column within the items structure to sort
		/// </summary>
		private int m_nSortColumn = 0;
		public int SortColumn
		{
			get { return m_nSortColumn; }
			set { m_nSortColumn = value; }
		}


		/// <summary>
		/// Direction this sorting routine will move items
		/// </summary>
		private SortDirections m_SortDirection = SortDirections.SortDescending;
		public SortDirections SortDirection
		{
			get { return m_SortDirection; }
			set{ m_SortDirection = value; }
		}


		public void QuickSort( GLItemCollection items, int vleft, int vright)
		{
			int w, x;
			GLItem tmpItem;

			int Med = 4;

			if ((vright-vleft)>Med)
			{
				w = (vright+vleft)/2;

				if (CompareItems( items[vleft], items[w], CompareDirection.GreaterThan )) swap(items,vleft,w);
				if (CompareItems( items[vleft], items[vright], CompareDirection.GreaterThan )) swap(items,vleft,vright);
				if (CompareItems( items[w], items[vright], CompareDirection.GreaterThan )) swap(items,w,vright);

				x = vright-1;
				swap(items,w,x);
				w = vleft;
				tmpItem = items[x];

				while( true )
				{
					while( this.CompareItems( items[++w], tmpItem, CompareDirection.LessThan ) );
					while( this.CompareItems( items[--x], tmpItem, CompareDirection.GreaterThan ) );

					if (x<w)
						break;

					swap(items,w,x);

					if ( m_bStopRequested )
						return;

				}
				swap(items,w,vright-1);

				QuickSort(items,vleft,x);
				QuickSort(items,w+1,vright);
			}
		}

		private void swap(GLItemCollection items, int x, int w)
		{
			GLItem tmpItem;
			tmpItem = items[x]; 
			items[x] = items[w];
			items[w] = tmpItem;
		}

		public void GLInsertionSort(GLItemCollection items, int nLow0, int nHigh0)
		{
			int w;

			GLItem tmpItem;

			for ( int x=nLow0+1; x<=nHigh0; x++)
			{
				tmpItem = items[x];
				w=x;

				while ( (w>nLow0) && ( this.CompareItems( items[w-1], tmpItem, CompareDirection.GreaterThan ) ) )
				{
					items[w] = items[w-1];
					w--;
				}

				items[w] = tmpItem;
			}
		}


		public void sort( GLItemCollection items )
		{
			QuickSort( items, 0, items.Count - 1);
			GLInsertionSort( items, 0,items.Count -1);
		}


		private bool CompareItems( GLItem item1, GLItem item2, CompareDirection direction )
		{
			// add a numeric compare here also
			bool dir = false;

			if ( direction == CompareDirection.GreaterThan )
				dir=true;

			if ( this.SortDirection == SortDirections.SortAscending )
				dir = !dir;		// flip it

			if ( !this.NumericCompare )
			{
				if ( dir )
				{
					return ( item1.SubItems[SortColumn].Text.CompareTo( item2.SubItems[SortColumn].Text ) < 0 );
				}
				else
				{
					return ( item1.SubItems[SortColumn].Text.CompareTo( item2.SubItems[SortColumn].Text ) > 0 );
				}
			}
			else
			{
				try
				{
					double n1 = Double.Parse( item1.SubItems[SortColumn].Text );
					double n2 = Double.Parse( item2.SubItems[SortColumn].Text );

					if ( dir )
					{	// compare the numeric values inside the columns
						return ( n1 < n2 );
					}
					else
					{
						return ( n1 > n2 );
					}
				}
				catch( Exception ex )
				{
					// no numeric value (bad bad)
					Debug.WriteLine( ex.ToString() );
					return false;
				}
			}
		}

	}
}
