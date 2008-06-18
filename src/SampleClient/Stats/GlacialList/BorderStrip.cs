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
	/// Summary description for BorderStrip.
	/// </summary>
	internal class BorderStrip : System.Windows.Forms.Control
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public BorderStrip()
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
			components = new System.ComponentModel.Container();
		}
		#endregion

		protected override void OnPaint(PaintEventArgs pe)
		{
			switch( BorderType )
			{
				case BorderTypes.btSquare:
				{
					ControlPaint.DrawBorder3D( pe.Graphics, this.ClientRectangle, System.Windows.Forms.Border3DStyle.SunkenInner );			// draw control border			
					//pe.Graphics.FillRectangle( SystemBrushes.Control, this.ClientRectangle );
					break;
				}

				case BorderTypes.btLeft:
				{
					// NOTE, the reason to make a fake rect is because we are specifically looking for only a part of the rect which in this case is the left 2
					// however, we have to make it bigger for it to draw an entire left side with 2 pixels, i made it 8 just for safety
					Rectangle tmpRect = new Rectangle( 0, 0, 8, ClientRectangle.Height );
					ControlPaint.DrawBorder3D( pe.Graphics, tmpRect, System.Windows.Forms.Border3DStyle.Sunken );			// draw control border			
					break;
				}

				case BorderTypes.btRight:
				{
					// this should put only the right 2 pixels of the border on the visible strip (i hope)
					Rectangle tmpRect = new Rectangle( -6, 0, 8, ClientRectangle.Height );
					ControlPaint.DrawBorder3D( pe.Graphics, tmpRect, System.Windows.Forms.Border3DStyle.Sunken );			// draw control border			
					break;
				}

				case BorderTypes.btBottom:
				{
					Rectangle tmpRect = new Rectangle( 0, -6, ClientRectangle.Width, 8 );
					ControlPaint.DrawBorder3D( pe.Graphics, tmpRect, System.Windows.Forms.Border3DStyle.Sunken );			// draw control border			
					break;
				}

				case BorderTypes.btTop:
				{
					Rectangle tmpRect = new Rectangle( 0, 0, ClientRectangle.Width, 8 );
					ControlPaint.DrawBorder3D( pe.Graphics, tmpRect, System.Windows.Forms.Border3DStyle.Sunken );			// draw control border			
					break;
				}
			}

			// Calling the base class OnPaint
			base.OnPaint(pe);
		}

		public enum BorderTypes { btLeft = 0, btRight = 1, btTop = 2, btBottom = 3, btSquare = 4 };


		private BorderTypes			m_BorderType;

		/// <summary>
		/// how the control looks on the outside
		/// </summary>
		public BorderTypes BorderType
		{
			get
			{
				return m_BorderType;
			}
			set
			{
				m_BorderType = value;
			}
		}


	}
}
