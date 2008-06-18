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
using System.Windows;
using System.Windows.Forms;
using System.Diagnostics;


namespace GlacialComponents.Controls
{
	/// <summary>
	/// Summary description for ManagedVScrollBar.
	/// </summary>
	internal class ManagedVScrollBar : System.Windows.Forms.VScrollBar
	{
		public ManagedVScrollBar()
		{
			this.TabStop = false;
			this.GotFocus += new EventHandler( ReflectFocus );
		}

		public void ReflectFocus( object source, EventArgs e )
		{
			Debug.WriteLine( "focus called" );
			this.Parent.Focus();
		}

		private void InitializeComponent()
		{

		}

		public int mTop
		{
			set
			{
				if ( Top!=value)
					Top = value;
			}
		}
		public int mLeft
		{
			set
			{
				if ( value != Left )
					Left = value;
			}
		}
		public int mWidth
		{
			get
			{
				if ( Visible != true )
					return 0;
				else
					return Width;
			}
			set
			{
				if ( Width != value )
					Width = value;
			}
		}
		public int mHeight
		{
			get
			{
				if ( Visible != true )
					return 0;
				else
					return Height;
			}
			set
			{
				if ( Height != value )
					Height = value;
			}
		}
		public bool mVisible
		{
			set
			{
				if ( Visible != value )
					Visible = value;
			}
		}
		public int mSmallChange
		{
			set
			{
				if ( SmallChange != value )
					SmallChange = value;
			}
		}
		public int mLargeChange
		{
			set
			{
				if ( LargeChange != value )
					LargeChange = value;
			}
		}
		public int mMaximum
		{
			set
			{
				if ( Maximum != value )
					Maximum = value;
			}
		}
	}

}
