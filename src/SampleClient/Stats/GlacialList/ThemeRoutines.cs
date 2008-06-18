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
using System.Drawing;
using System.Runtime.InteropServices;


namespace GlacialComponents.Controls
{


	/// <summary>
	/// Summary description for ThemeRoutines.
	/// </summary>
	internal sealed class ThemeRoutines
	{
		public ThemeRoutines()
		{

		}

		/// <summary>
		/// Tests if a visual style for the current application is active.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern int IsThemeActive();
		
		/// <summary>
		/// Opens the theme data for a window and its associated class.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern IntPtr OpenThemeData(IntPtr hWnd, [MarshalAs(UnmanagedType.LPTStr)] string classList);
		
		/// <summary>Closes the theme data handle.</summary>
		/// <remarks>The CloseThemeData function should be called when a window that has a visual style applied is destroyed.</remarks> 
		[DllImport("uxtheme.dll")]
		public static extern void CloseThemeData(IntPtr hTheme);
		
		/// <summary>
		/// Draws the background image defined by the visual style for the specified control part.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern void DrawThemeBackground(IntPtr hTheme, IntPtr hDC, int partId, int stateId, ref RECT rect, ref RECT clipRect);
		
		/// <summary>
		/// Draws one or more edges defined by the visual style of a rectangle.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern void DrawThemeEdge(IntPtr hTheme, IntPtr hDC, int partId, int stateId, ref RECT destRect, uint edge, uint flags, ref RECT contentRect);
		
		/// <summary>
		/// Draws an image from an image list with the icon effect defined by the visual style.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern void DrawThemeIcon(IntPtr hTheme, IntPtr hDC, int partId, int stateId, ref RECT rect, IntPtr hIml, int imageIndex);
		
		/// <summary>
		/// Draws text using the color and font defined by the visual style.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern void DrawThemeText(IntPtr hTheme, IntPtr hDC, int partId, int stateId, [MarshalAs(UnmanagedType.LPTStr)] string text, int charCount, uint textFlags, uint textFlags2, ref RECT rect);
		
		/// <summary>
		/// Draws the part of a parent control that is covered by a partially-transparent or alpha-blended child control.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern void DrawThemeParentBackground(IntPtr hWnd, IntPtr hDC, ref RECT rect);
		
		/// <summary>
		/// Causes a window to use a different set of visual style information than its class normally uses.
		/// </summary>
		[DllImport("uxtheme.dll")]
		public static extern void SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

	}
}
