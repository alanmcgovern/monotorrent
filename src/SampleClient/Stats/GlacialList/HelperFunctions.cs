/***************************************************
 * Glacial List v1.30
 * 
 * Written By Allen Anderson
 * http://www.glacialcomponents.com
 * 
 * February 24th, 2004
 * 
 * You may redistribute this control in binary and modified binary form as you please.  You may
 * use this control in commercial applications without need for external credit royalty free.
 * 
 * However, you are restricted from releasing the source code in any modified fashion
 * whatsoever.
 * 
 * I MAKE NO PROMISES OR WARRANTIES ON THIS CODE/CONTROL.  IF ANY DAMAGE OR PROBLEMS HAPPEN FROM ITS USE
 * THEN YOU ARE RESPONSIBLE.
 * 
 */



using System;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing.Design;
using System.Windows.Forms.Design;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Collections.Specialized;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GlacialComponents.Controls
{
	#region Helper Classes

	/// <summary>
	/// Internal struct for use with the header style flat only
	/// </summary>
	[StructLayout(LayoutKind.Explicit)] 
	internal struct RECT
	{
		[FieldOffset(0)] public int Left;

		[FieldOffset(4)] public int Top;

		[FieldOffset(8)] public int Right;

		[FieldOffset(12)] public int Bottom;

		public RECT(int left, int top, int right, int bottom) 
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		public RECT(Rectangle rect) 
		{
			Left = rect.Left; 
			Top = rect.Top;
			Right = rect.Right;
			Bottom = rect.Bottom;
		}

		public Rectangle ToRectangle() 
		{
			return new Rectangle(Left, Top, Right, Bottom - 1);
		}
	}


	/// <summary>
	/// Delegate for changed events within Columns, Items, and SubItems
	/// </summary>
	public delegate void ChangedEventHandler( object source, ChangedEventArgs e );


	/// <summary>
	/// Change events that are filtered up out of the control
	/// </summary>
	public enum ChangedTypes 
	{ 
		/// <summary>
		/// Invalidation Fired
		/// </summary>
		GeneralInvalidate, 
		/// <summary>
		/// Sub Item Changed
		/// </summary>
		SubItemChanged,
		/// <summary>
		/// Sub Item Collection Changed
		/// </summary>
		SubItemCollectionChanged, 
		/// <summary>
		/// Item Changed
		/// </summary>
		ItemChanged, 
		/// <summary>
		/// Item Collection Changed
		/// </summary>
		ItemCollectionChanged, 
		/// <summary>
		/// Column changed
		/// </summary>
		ColumnChanged, 
		/// <summary>
		/// Column Collection Changed
		/// </summary>
		ColumnCollectionChanged, 
		/// <summary>
		/// Focus Changed
		/// </summary>
		FocusedChanged,
		/// <summary>
		/// A different item is now selected
		/// </summary>
		SelectionChanged,
		/// <summary>
		/// Column state has changed
		/// </summary>
		ColumnStateChanged
	};


	/// <summary>
	/// Changed Event Args
	/// </summary>
	public class ChangedEventArgs : EventArgs
	{
		private GLColumn				m_Column;
		private GLItem					m_Item;
		private GLSubItem				m_SubItem;

		private ChangedTypes		m_ctType = ChangedTypes.GeneralInvalidate;


		/// <summary>
		/// Changes in the columns, items or subitems
		/// </summary>
		/// <param name="ctType"></param>
		/// <param name="column"></param>
		/// <param name="item"></param>
		/// <param name="subItem"></param>
		public ChangedEventArgs( ChangedTypes ctType, GLColumn column, GLItem item, GLSubItem subItem )
		{
			m_Column = column;
			m_Item = item;
			m_SubItem = subItem;

			m_ctType = ctType;
		}

		/// <summary>
		/// Column Name
		/// </summary>
		public GLColumn Column
		{
			get	{ return m_Column; }
			set	{ m_Column = value; }
		}

		/// <summary>
		/// Item Name
		/// </summary>
		public GLItem Item
		{
			get	{ return m_Item; }
			set	{ m_Item = value; }
		}

		/// <summary>
		/// SubItem Name
		/// </summary>
		public GLSubItem SubItem
		{
			get	{ return m_SubItem; }
			set	{ m_SubItem = value; }
		}

		/// <summary>
		/// Type of change
		/// </summary>
		public ChangedTypes ChangedType
		{
			get { return m_ctType; 	}
		}
	}

	/// <summary>
	/// String Manipulation Help
	/// </summary>
	internal class GLStringHelpers
	{
		/// <summary>
		/// Truncate a string.
		/// 
		/// This function also handles truncation of multiline strings
		/// </summary>
		/// <param name="strText"></param>
		/// <param name="nWidth"></param>
		/// <param name="subDC"></param>
		/// <param name="font"></param>
		/// <returns>
		/// Truncated string
		/// </returns>
		public static string TruncateString( string strText, int nWidth, Graphics subDC, Font font )
		{
			//DW("TuncateString");
			string strTruncated = "";

			SizeF sizeString = MeasureMultiLineString( strText, subDC, font );
			if ( sizeString.Width < nWidth )
				return strText;				// this doesnt need any work, bail out

			int strTDotSize;
			strTDotSize = (int)subDC.MeasureString( "...", font ).Width;
			if ( strTDotSize > nWidth )
				return "";					// Cant even fit the triple dots here


			StringReader r = new StringReader(strText); 
			string line; 
			while ((line = r.ReadLine()) != null) 
			{
				if ( subDC.MeasureString( line, font ).Width < nWidth )
				{	// original sub line is fine, doesn't need truncation
					strTruncated += line + "\n";
				}
				else
				{	// sub line needs to be truncated
					for ( int index=line.Length; index!=0; index-- )
					{
						string tmpString;
						tmpString = line.Substring( 0, index ) + "...";

						//DW("Truncating string to " + strText );

						if ( subDC.MeasureString( tmpString, font ).Width < nWidth )
						{
							strTruncated += tmpString + "\n";
							break;			// stop the for loop so we can test more strings
						}
					}
				}
			}

			// remove the trailing linefeed for the last line in a sequence (because its not needed and woudl possibly mess things up
			if ( strTruncated.Length > 1 )
				strTruncated.Remove( strTruncated.Length-1, 1 );

			return strTruncated;
		}

		/// <summary>
		/// Measure a multi lined string
		/// </summary>
		/// <param name="strText"></param>
		/// <param name="mDC"></param>
		/// <param name="font"></param>
		/// <returns></returns>
		public static SizeF MeasureMultiLineString( string strText, Graphics mDC, Font font )
		{
			StringReader r = new StringReader(strText); 
			SizeF sizeStr = new SizeF(0,0);

			string line; 
			while ((line = r.ReadLine()) != null) 
			{
				SizeF tsize = mDC.MeasureString( line, font );

				sizeStr.Height += tsize.Height;
				if ( sizeStr.Width < tsize.Width )
					sizeStr.Width = tsize.Width;
			}

			return sizeStr;
		}



		public static StringAlignment ConvertContentAlignmentToVerticalStringAlignment( ContentAlignment alignment )
		{
			StringAlignment sa = StringAlignment.Near;

			switch ( alignment )
			{
				case ContentAlignment.TopLeft:
				case ContentAlignment.TopCenter:
				case ContentAlignment.TopRight:
				{
					sa = StringAlignment.Near;
					break;
				}

				case ContentAlignment.MiddleLeft:
				case ContentAlignment.MiddleCenter:
				case ContentAlignment.MiddleRight:
				{
					sa = StringAlignment.Center;
					break;
				}

				case ContentAlignment.BottomLeft:
				case ContentAlignment.BottomCenter:
				case ContentAlignment.BottomRight:
				{
					sa = StringAlignment.Far;
					break;
				}
			}

			return sa;
		}

		public static StringAlignment ConvertContentAlignmentToHorizontalStringAlignment( ContentAlignment alignment )
		{
			StringAlignment sa = StringAlignment.Near;

			switch ( alignment )
			{
				case ContentAlignment.TopLeft:
				case ContentAlignment.MiddleLeft:
				case ContentAlignment.BottomLeft:
				{
					sa = StringAlignment.Near;
					break;
				}

				case ContentAlignment.TopCenter:
				case ContentAlignment.MiddleCenter:
				case ContentAlignment.BottomCenter:
				{
					sa = StringAlignment.Center;
					break;
				}

				case ContentAlignment.TopRight:
				case ContentAlignment.MiddleRight:
				case ContentAlignment.BottomRight:
				{
					sa = StringAlignment.Far;
					break;
				}
			}

			return sa;
		}


	}

	/// <summary>
	/// Clicked Event Args
	/// </summary>
	public class ClickEventArgs : EventArgs
	{
		private int m_nItemIndex;
		private int m_nColumnIndex;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="itemindex"></param>
		/// <param name="columnindex"></param>
		public ClickEventArgs( int itemindex, int columnindex )
		{
			m_nItemIndex = itemindex;
			m_nColumnIndex = columnindex;
		}

		/// <summary>
		/// Index of item clicked
		/// </summary>
		public int ItemIndex
		{
			get	{ return m_nItemIndex; }
		}

		/// <summary>
		/// Index of column clicked
		/// </summary>
		public int ColumnIndex
		{
			get	{ return m_nColumnIndex; }
		}
	}


	/// <summary>
	/// Types of hover
	/// </summary>
	public enum HoverTypes 
	{ 
		/// <summary>
		/// Hover has begun
		/// </summary>
		HoverStart, 
		/// <summary>
		/// Hover ending
		/// </summary>
		HoverEnd 
	}

	/// <summary>
	/// Hover event args
	/// </summary>
	public class HoverEventArgs : EventArgs
	{
		private int m_nItemIndex;
		private int m_nColumnIndex;
		private GLListRegion m_Region;
		private HoverTypes m_HoverType;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="hovertype"></param>
		/// <param name="itemindex"></param>
		/// <param name="columnindex"></param>
		/// <param name="region"></param>
		public HoverEventArgs( HoverTypes hovertype, int itemindex, int columnindex, GLListRegion region )
		{
			m_Region = region;
			m_nItemIndex = itemindex;
			m_nColumnIndex = columnindex;
			m_HoverType = hovertype;
		}

		/// <summary>
		/// Type of hover
		/// </summary>
		public HoverTypes HoverType
		{
			get { return m_HoverType; }
		}

		/// <summary>
		/// Region being hovered
		/// </summary>
		public GLListRegion Region
		{
			get { return m_Region; }
		}

		/// <summary>
		/// Index of item hovered
		/// </summary>
		public int ItemIndex
		{
			get { return m_nItemIndex; }
		}

		/// <summary>
		/// Index of column hovered
		/// </summary>
		public int ColumnIndex
		{
			get	{ return m_nColumnIndex; }
		}
	}


	#endregion
}
