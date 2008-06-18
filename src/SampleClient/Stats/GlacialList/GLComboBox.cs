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

namespace GlacialComponents.Controls
{
	/// <summary>
	/// Summary description for GLComboBox.
	/// </summary>
	internal class GLComboBox : System.Windows.Forms.ComboBox, GLEmbeddedControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public GLComboBox()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call

		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
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
			components = new System.ComponentModel.Container();
		}
		#endregion

		#region GLEmbeddedControl Members

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

		public bool GLLoad(GLItem item, GLSubItem subItem, GlacialList listctrl)
		{
			m_item = item;
			m_subItem = subItem;
			m_Parent = listctrl;

			this.Text = subItem.Text;

			this.Items.Add( "i1" );
			this.Items.Add( "i2" );
			this.Items.Add( "i3" );

			return true;
		}

		public void GLUnload()
		{
			m_subItem.Text = this.Text;
		}


		#endregion

	}
}
