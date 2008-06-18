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

	/// <summary>
	/// Interface you must include for a control to be activated embedded useable
	/// </summary>
	public interface GLEmbeddedControl
	{

		/// <summary>
		/// item this control is embedded in
		/// </summary>
		GLItem				Item { get; set; }

		/// <summary>
		/// Sub item this control is embedded in
		/// </summary>
		GLSubItem		SubItem { get; set; }

		/// <summary>
		/// Parent control
		/// </summary>
		GlacialList			ListControl { get; set; }

		/// <summary>
		/// This returns the current text output as entered into the control right now
		/// </summary>
		/// <returns></returns>
		string GLReturnText();


		/// <summary>
		/// Called when the control is loaded
		/// </summary>
		/// <param name="item"></param>
		/// <param name="subItem"></param>
		/// <param name="listctrl"></param>
		/// <returns></returns>
		bool GLLoad( GLItem item, GLSubItem subItem, GlacialList listctrl );		// populate this control however you wish with item


		/// <summary>
		/// Called when control is being destructed
		/// </summary>
		void GLUnload();																			// take information from control and return it to the item
	}


}
