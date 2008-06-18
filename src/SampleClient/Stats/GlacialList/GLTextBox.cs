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
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;

namespace GlacialComponents.Controls
{
	/// <summary>
	/// Summary description for GLTextBox.
	/// </summary>
	internal class GLTextBox : System.Windows.Forms.TextBox, GLEmbeddedControl
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public GLTextBox()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitComponent call
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if( components != null )
					components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			// 
			// GLTextBox
			// 
			this.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.GLTextBox_KeyPress);

		}
		#endregion

		protected override void OnPaint(PaintEventArgs pe)
		{
			// TODO: Add custom paint code here

			// Calling the base class OnPaint
			base.OnPaint(pe);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			Debug.WriteLine( "Got Focus" );

			base.OnGotFocus (e);
		}


		protected override void OnLostFocus(EventArgs e)
		{
			Debug.WriteLine( "Lost Focus" );

			base.OnLostFocus (e);
		}


		public GLItem	 Item 
		{ 
			get
			{
				return m_item;
			}
			set
			{
				m_item = value;
			}
		}

		public GLSubItem SubItem
		{ 
			get
			{
				return m_subItem;
			}
			set
			{
				m_subItem = value;
			}
		}

		public GlacialList	 ListControl
		{ 
			get
			{
				return m_Parent;
			}
			set
			{
				m_Parent = value;
			}
		}


		public string GLReturnText()
		{
			return this.Text;
		}


		protected GLItem m_item = null;
		protected GLSubItem m_subItem = null;
		protected GlacialList m_Parent = null;

		public bool GLLoad( GLItem item, GLSubItem subItem, GlacialList listctrl )				// populate this control however you wish with item
		{
			// set the styles you want for this
			this.BorderStyle = BorderStyle.None;
			this.AutoSize = false;


			m_item = item;
			m_subItem = subItem;
			m_Parent = listctrl;

			this.Text = subItem.Text;

			return true;					// we don't do any heavy processing in this ctrl so we just return true
		}

		public void GLUnload()			// take information from control and return it to the item
		{
			m_subItem.Text = this.Text;
		}

		private void GLTextBox_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
		{
			Debug.WriteLine( "keypress edit control" );
		}


	}
}
