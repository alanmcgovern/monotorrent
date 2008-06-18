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
	/// Summary description for GLDateTimePicker.
	/// </summary>
	internal class GLDateTimePicker : System.Windows.Forms.DateTimePicker, GLEmbeddedControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public GLDateTimePicker()
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();


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
			this.Format = DateTimePickerFormat.Long;
			try
			{
				m_item = item;
				m_subItem = subItem;
				m_Parent = listctrl;

				this.Text = subItem.Text;

				//this.Value = subItem.Text;
			}
			catch ( Exception ex )
			{
				Debug.WriteLine( ex.ToString() );

				this.Text = DateTime.Now.ToString();
			}

			return true;
		}

		public void GLUnload()
		{
			m_subItem.Text = this.Text;
		}



		#endregion

	}
}
