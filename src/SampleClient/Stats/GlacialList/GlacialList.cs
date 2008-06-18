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

	#region Enumerations

	/// <summary>
	/// Types of sorting available
	/// </summary>
	public enum SortTypes 
	{ 
		/// <summary>
		/// No Sorting
		/// </summary>
		None,
		/// <summary>
		/// Insertion Sort
		/// </summary>
		InsertionSort, 
		/// <summary>
		/// Merge Sort
		/// </summary>
		MergeSort, 
		/// <summary>
		/// Quick Sort
		/// </summary>
		QuickSort 
	}

	/// <summary>
	/// State of the listview
	/// </summary>
	public enum ListStates 
	{ 
		/// <summary>
		/// listview is in normal state
		/// </summary>
		stateNone, 
		/// <summary>
		/// an item is selected in listview
		/// </summary>
		stateSelecting, 
		/// <summary>
		/// a column is selected in listview
		/// </summary>
		stateColumnSelect, 
		/// <summary>
		/// a column is being selected in listview
		/// </summary>
		stateColumnResizing 
	}

	/// <summary>
	/// Region reference
	/// </summary>
	public enum GLListRegion 
	{ 
		/// <summary>
		/// Header Area (Column Header)
		/// </summary>
		header=0, 
		/// <summary>
		/// Client Area (Items)
		/// </summary>
		client=1, 
		/// <summary>
		/// Non Client Area (Potentitally not on surface)
		/// </summary>
		nonclient=2 
	}

	/// <summary>
	/// Style of grid lines in client area
	/// </summary>
	public enum GLGridLineStyles 
	{ 
		/// <summary>
		/// Do not show a grid at all/// 
		/// </summary>
		gridNone=0, 
		/// <summary>
		/// Dashed line grid
		/// </summary>
		gridDashed=1, 
		/// <summary>
		/// Solid grid line
		/// </summary>
		gridSolid=2 
	}


	/// <summary>
	/// Grid Line direction
	/// </summary>
	public enum GLGridLines 
	{ 
		/// <summary>
		/// Horizontal and Vertical lines
		/// </summary>
		gridBoth=0, 
		/// <summary>
		/// Vertical Grid
		/// </summary>
		gridVertical=1, 
		/// <summary>
		/// Horizontal Grid
		/// </summary>
		gridHorizontal=2 
	}

	/// <summary>
	/// Grid type
	/// </summary>
	/// <remarks>
	/// Normal grid shows lines regardless if items exist or not.  If you choose OnExists the lines will
	/// only show if there are items present.
	/// </remarks>
	public enum GLGridTypes 
	{ 
		/// <summary>
		/// Normal lines always present
		/// </summary>
		gridNormal=0, 
		/// <summary>
		/// Horizontal Lines only present when items exist
		/// </summary>
		gridOnExists=1 
	}

	/// <summary>
	/// Column Header Styles
	/// </summary>
	public enum GLControlStyles
	{
		/// <summary>
		/// Common Style
		/// </summary>
		Normal = 0,
		/// <summary>
		/// Flat look (much like an HTML list header)
		/// </summary>
		SuperFlat = 1,
		/// <summary>
		/// Windows XP look header
		/// </summary>
		XP = 2
	}


	/// <summary>
	/// Activated Embedding Types
	/// </summary>
	public enum GLActivatedEmbeddedTypes 
	{ 
		/// <summary>
		/// Do not use an activated embedded type for this
		/// </summary>
		None, 
		/// <summary>
		/// User fills in Embedded type
		/// </summary>
		UserType, 
		/// <summary>
		/// Text Box.  Used mostly for editable cells.
		/// </summary>
		TextBox, 
		/// <summary>
		/// Combo Box.
		/// </summary>
		ComboBox, 
		/// <summary>
		/// Date Picker
		/// </summary>
		DateTimePicker 
	}


	#endregion

	/// <summary>
	/// Summary description for GlacialList.
	/// </summary>
	public class GlacialList : System.Windows.Forms.Control
	{
		#region Debugging

		/// <summary>
		/// Debugging output routines.  All routines implement this within the glacial list to make routine tracing
		/// as easy as possible
		/// </summary>
		/// <param name="strout"></param>
		internal static void DW( string strout )			// debug write
		{
#if false
			System.IO.StreamWriter sw = new System.IO.StreamWriter( "e:\\debug.txt", true );
			sw.WriteLine( strout );
			sw.Close();
#else
			//Debug.WriteLine( strout );
#endif
		}

		/// <summary>
		/// In order to track all invalidations DI is added around all invalidates
		/// </summary>
		/// <param name="strout"></param>
		internal static void DI( string strout )			// debug write
		{
#if false
			//System.IO.StreamWriter sw = new System.IO.StreamWriter( "e:\\debug.txt", true );
			//sw.WriteLine( strout );
			//sw.Close();
#else
			Debug.WriteLine( DateTime.Now.ToLocalTime().ToString() + " " + strout );
#endif
		}


		private void InitializeComponent()
		{
			//this.Columns.
			if ( this.ControlStyle == GLControlStyles.XP )
				Application.EnableVisualStyles();


			this.components = new System.ComponentModel.Container();
			System.Resources.ResourceManager resources = new System.Resources.ResourceManager(typeof(GlacialList));
			this.imageList1 = new System.Windows.Forms.ImageList(this.components);
			// 
			// imageList1
			// 
			this.imageList1.ImageSize = new System.Drawing.Size(13, 13);
			this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
			this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
		}



		#endregion

		#region Header

		#region Events and Delegates

		#region ListView Events

		/// <summary>
		/// Click happened inside control.  Use ClickEventArgs to find out origination area.
		/// </summary>
		public event ClickedEventHandler SelectedIndexChanged;

		#endregion

		#region Clicked Events

		/// <summary>
		/// Clicked Event Handler delegate definition
		/// </summary>
		public delegate void ClickedEventHandler( object source, ClickEventArgs e );//int nItem, int nSubItem );
		/// <summary>
		/// Click happened inside control.  Use ClickEventArgs to find out origination area.
		/// </summary>
		public event ClickedEventHandler ColumnClickedEvent;

		#endregion

		#region Changed Events


		/// <summary>
		/// Item Changed Event
		/// </summary>
		public event ChangedEventHandler ItemChangedEvent;

		/// <summary>
		/// Column Changed Event
		/// </summary>
		public event ChangedEventHandler ColumnChangedEvent;

		#endregion
		
		#region Hover Events

		/// <summary>
		/// Hover Event delegate definition
		/// </summary>
		public delegate void HoverEventDelegate( object source, HoverEventArgs e );
		/// <summary>
		/// A hover event has occured.
		/// </summary>
		/// <remarks>
		/// Use HoverType member of HoverEventArgs to find out if this is a hover origination
		/// or termination event.
		/// </remarks>
		public event HoverEventDelegate HoverEvent;

		#endregion

		#endregion

		#region VarsDefsProps

		#region Definitions



		private enum WIN32Codes
		{
			WM_GETDLGCODE = 0x0087,
			WM_SETREDRAW = 0x000B,
			WM_CANCELMODE = 0x001F,
			WM_NOTIFY = 0x4e,
			WM_KEYDOWN = 0x100,
			WM_KEYUP = 0x101,
			WM_CHAR = 0x0102,
			WM_SYSKEYDOWN = 0x104,
			WM_SYSKEYUP = 0x105,
			WM_COMMAND = 0x111,
			WM_MENUCHAR = 0x120,
			WM_MOUSEMOVE = 0x200,
			WM_LBUTTONDOWN = 0x201,
			WM_MOUSELAST = 0x20a,
			WM_USER = 0x0400,
			WM_REFLECT = WM_USER + 0x1c00
		}

		private enum DialogCodes
		{
			DLGC_WANTARROWS =     0x0001,
			DLGC_WANTTAB =        0x0002,
			DLGC_WANTALLKEYS =    0x0004,
			DLGC_WANTMESSAGE =    0x0004,
			DLGC_HASSETSEL =      0x0008,
			DLGC_DEFPUSHBUTTON =  0x0010,
			DLGC_UNDEFPUSHBUTTON = 0x0020,
			DLGC_RADIOBUTTON =    0x0040,
			DLGC_WANTCHARS =      0x0080,
			DLGC_STATIC =         0x0100,
			DLGC_BUTTON =         0x2000,
		}

		private const int WM_KEYDOWN = 0x0100;
		private const int VK_LEFT = 0x0025;
		private const int VK_UP = 0x0026;
		private const int VK_RIGHT = 0x0027;
		private const int VK_DOWN = 0x0028;


		private const int CHECKBOX_SIZE = 13;


		const int					RESIZE_ARROW_PADDING = 3;
		const int					MINIMUM_COLUMN_SIZE = 0;

		#endregion

		#region Class Variables

		private int					m_nLastSelectionIndex = 0;
		private int					m_nLastSubSelectionIndex = 0;

		private ListStates			m_nState = ListStates.stateNone;
		private Point				m_pointColumnResizeAnchor;
		private int					m_nResizeColumnNumber;			// the column number thats being resized

		private ArrayList			LiveControls = new ArrayList();		// list of controls currently visible.  THIS IS AN OPTIMIZATION.  This will keep us from having to iterate the entire list beforehand.
		private ArrayList			NewLiveControls = new ArrayList();
		private System.ComponentModel.IContainer components;

		private GlacialComponents.Controls.ManagedVScrollBar vPanelScrollBar;
		private GlacialComponents.Controls.ManagedHScrollBar hPanelScrollBar;


		private BorderStrip			vertLeftBorderStrip;
		private BorderStrip			vertRightBorderStrip;
		private BorderStrip			horiBottomBorderStrip;
		private BorderStrip			horiTopBorderStrip;
		private BorderStrip			cornerBox;


		private Control				m_ActivatedEmbeddedControl = null;


		#endregion

		#region Control Properties

		private GLColumnCollection				m_Columns;
		private GLItemCollection				m_Items;
		

		// border
		private bool							m_bShowBorder = true;

		private GLGridLineStyles				m_GridLineStyle = GLGridLineStyles.gridSolid;
		private GLGridLines 					m_GridLines = GLGridLines.gridBoth;
		private GLGridTypes						m_GridType = GLGridTypes.gridOnExists;

		private int								m_nItemHeight = 18;
		private int								m_nHeaderHeight = 22;
		//private int							m_nBorderWidth = 2;
		private Color							m_colorGridColor = Color.LightGray;
		private bool							m_bMultiSelect = false;
		private Color							m_colorSelectionColor = Color.DarkBlue;
		private bool							m_bHeaderVisible = true;
		private ImageList						m_ImageList = null;								// if it doesnt exist, then don't make it yet.

		private Color							m_SelectedTextColor = Color.White;

		private int								m_nMaxHeight = 0;
		private bool							m_bAutoHeight = true;
		private bool							m_bAllowColumnResize = true;
		private bool							m_bFullRowSelect = true;
		private SortTypes						m_SortType = SortTypes.InsertionSort;

		private GLItem							m_FocusedItem = null;
		private bool							m_bShowFocusRect = false;

		private bool							m_bHotColumnTracking = false;
		private bool							m_bHotItemTracking = false;
		private int								m_nHotColumnIndex = -1;							// internal hot column
		private int								m_nHotItemIndex = -1;							// internal hot item index
		private Color							m_HotTrackingColor = Color.LightGray;			// brush color to use

		private bool							m_bUpdating = false;


		private bool							m_bAlternatingColors = false;
		private Color							m_colorAlternateBackground = Color.DarkGreen;
		private Color							m_colorSuperFlatHeaderColor = Color.White;

		//private GLControlStyles					m_ControlStyle = GLControlStyles.Normal;
		private GLControlStyles					m_ControlStyle = GLControlStyles.Normal;

		private bool							m_bItemWordWrap = false;
		private bool							m_bHeaderWordWrap = false;

		private bool							m_bSelectable = true;
		//private int								m_nRowBorderSize = 0;


		private bool							m_bHoverEvents = false;
		private int								m_nHoverTime = 1;
		private Point							m_ptLastHoverSpot = new Point(0,0);
		private bool							m_bHoverLive = false;			// if a hover event has been sent out (needs to be cancelled later)
		private Timer							m_Timer;


		private bool							m_bBackgroundStretchToFit = true;


		private System.Windows.Forms.ImageList imageList1;


		#region Hover

		/// <summary>
		/// Items HoverEvents.
		/// </summary>
		[
		Description("Enabling hover events slows the control some but allows you to be informed when a user has hovered over an item."),
		Category("Behavior"),
		Browsable(true)
		]
		public bool HoverEvents
		{
			get	
			{ 
				return m_bHoverEvents; 
			}
			set 
			{ 
				m_bHoverEvents = value;

				if ( !DesignMode )
				{
					if ( m_bHoverEvents )
					{	// turn the events off, so we need to add the events
						this.m_Timer = new Timer();
						m_Timer.Interval = this.m_nHoverTime*1000;		// convert to seconds
						m_Timer.Tick += new EventHandler(m_TimerTick);
						m_Timer.Start();
					}
					else if ( m_Timer != null )
					{	// turn the events off
						m_Timer.Stop();
						m_Timer = null;
					}
				}
			}
		}


		/// <summary>
		///
		/// </summary>
		/// 
		[
		Description("Amount of time in seconds a user hovers before hover event is fired.  Can NOT be zero."),
		Category("Behavior"),
		Browsable(true)
		]
		public int HoverTime
		{
			get 
			{ 
				return m_nHoverTime; 
			}
			set 
			{
				if ( m_nHoverTime < 1 )
					m_nHoverTime = 1;
				else
					m_nHoverTime = value; 
			}
		}

		#endregion



		/// <summary>
		/// Items ActivatedEmbeddedControl.
		/// </summary>
		[
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public Control ActivatedEmbeddedControl
		{
			get 
			{ 
				return m_ActivatedEmbeddedControl; 
			}
			set 
			{ 
				m_ActivatedEmbeddedControl = value; 
			}
		}


		/// <summary>
		/// Items BackgroundStretchToFit.
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Whether or not to stretch background to fit inner list area."),
		Category("Behavior"),
		Browsable(true)
		]
		public bool BackgroundStretchToFit
		{
			get	{ return m_bBackgroundStretchToFit; }
			set { m_bBackgroundStretchToFit = value; }
		}


		/// <summary>
		/// Items selectable.
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Items selectable."),
		Category("Behavior"),
		Browsable(true)
		]
		public bool Selectable
		{
			get	{ return m_bSelectable; }
			set { m_bSelectable = value; }
		}


		/// <summary>
		/// Word wrap in header
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Word wrap in header"),
		Category("Header"),
		Browsable(true)
		]
		public bool HeaderWordWrap
		{
			get	
			{ 
				return m_bHeaderWordWrap; 
			}
			set 
			{ 
				m_bHeaderWordWrap = value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}


		/// <summary>
		/// Word wrap in cells
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Word wrap in cells"),
		Category("Item"),
		Browsable(true)
		]
		public bool ItemWordWrap
		{
			get	{ return m_bItemWordWrap; }
			set 
			{ 
				m_bItemWordWrap = value; 

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}



		/// <summary>
		/// background color to use if flat
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Color for text in boxes that are selected."),
		Category("Header"),
		Browsable(true)
		]
		public Color SuperFlatHeaderColor
		{
			get	{ return m_colorSuperFlatHeaderColor; }
			set 
			{ 
				m_colorSuperFlatHeaderColor = value; 

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}



		/// <summary>
		/// Overall look of control
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Overall look of control"),
		Category("Behavior"),
		Browsable(true)
		]
		public GLControlStyles ControlStyle
		{
			get	{ return m_ControlStyle; }
			set
			{
				m_ControlStyle = value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					DI("Calling Invalidate from ControlStyle Property");
					Parent.Invalidate(true);
				}
			}
		}


		/// <summary>
		/// Alternating Colors on or off
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("turn xp themes on or not"),
		Category("Item Alternating Colors"),
		Browsable(true)
		]
		public bool AlternatingColors
		{
			get	{ return m_bAlternatingColors; }
			set 
			{ 
				m_bAlternatingColors = value; 

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}


		/// <summary>
		/// second background color if we use alternating colors
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Color for text in boxes that are selected."),
		Category("Item Alternating Colors"),
		Browsable(true)
		]
		public Color AlternateBackground
		{
			get	{ return m_colorAlternateBackground; }
			set 
			{ 
				m_colorAlternateBackground = value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}


		/// <summary>
		/// Whether or not to show a border.
		/// </summary>
		[
		Description("Whether or not to show a border."),
		Category("Appearance"),
		Browsable(true),
		]
		public bool ShowBorder
		{
			get	{ return m_bShowBorder; }
			set 
			{ 
				m_bShowBorder = value; 
			
				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}


		/// <summary>
		/// Color for text in boxes that are selected
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Color for text in boxes that are selected."),
		Category("Item"),
		Browsable(true)
		]
		public Color SelectedTextColor
		{
			get	{ return m_SelectedTextColor; }
			set { m_SelectedTextColor = value; }
		}


		/// <summary>
		/// hot tracking
		/// </summary>
		[
		Description("Color for hot tracking."),
		Category("Appearance"),
		Browsable(true)
		]
		public Color HotTrackingColor
		{
			get	{ return m_HotTrackingColor; }
			set { m_HotTrackingColor = value; }
		}


		/// <summary>
		/// Hot Tracking of columns and items
		/// </summary>
		[
		Description("Show hot tracking."),
		Category("Behavior"),
		Browsable(true)
		]
		public bool HotItemTracking
		{
			get	{ return m_bHotItemTracking; }
			set { m_bHotItemTracking = value; }
		}

		/// <summary>
		/// Hot Tracking of columns and items
		/// </summary>
		[
		Description("Show hot tracking."),
		Category("Behavior"),
		Browsable(true)
		]
		public bool HotColumnTracking
		{
			get	{ return m_bHotColumnTracking; }
			set { m_bHotColumnTracking = value; }
		}



		/// <summary>
		/// Show the focus rect or not
		/// </summary>
		[
		Description("Show Focus Rect on items."),
		Category("Item"),
		Browsable(true)
		]
		public bool ShowFocusRect
		{
			get	{ return m_bShowFocusRect; }
			set { m_bShowFocusRect = value; }
		}


		/// <summary>
		/// auto sorting
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Type of sorting algorithm used."),
		Category("Behavior"),
		Browsable(true),
		]
		public SortTypes SortType
		{
			get	{ return m_SortType; }
			set { m_SortType = value; }
		}


		/// <summary>
		/// 
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("ImageList to be used in listview."),
		Category("Behavior"),
		Browsable(true),
		]
		public ImageList ImageList
		{
			get	{ return m_ImageList; }
			set { m_ImageList = value; }
		}


		/// <summary>
		/// Allow columns to be resized
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Allow resizing of columns"),
		Category("Header"),
		Browsable(true)
		]
		public bool AllowColumnResize
		{
			get { return m_bAllowColumnResize; }
			set { m_bAllowColumnResize = value; }
		}


		/// <summary>
		/// Control resizes height of row based on size.
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Do we want rows to automatically adjust height"),
		Category("Item"),
		Browsable(true)
		]
		public bool AutoHeight
		{
			get { return m_bAutoHeight; }
			set 
			{ 
				m_bAutoHeight = value; 
				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}


		/// <summary>
		/// you want the header to be visible or not
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Column Headers Visible"),
		Category("Header"),
		Browsable(true)
		]
		public bool HeaderVisible
		{
			get { return m_bHeaderVisible; }
			set 
			{ 
				m_bHeaderVisible = value; 
				if ( ( this.DesignMode ) && ( this.Parent != null ) )
					this.Parent.Invalidate(true);
			}
		}


		/// <summary>
		/// Collection of columns
		/// </summary>
		[
		Category("Header"),
		Description("Column Collection"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
		Editor(typeof(CustomCollectionEditor), typeof(UITypeEditor)),
		Browsable(true)
		]
		public GLColumnCollection Columns
		{
			get	{ return m_Columns; }
		}


		/// <summary>
		/// Collection of items
		/// </summary>
		[
		Category("Item"),
		Description("Items collection"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
		//Editor(typeof(ItemCollectionEditor), typeof(UITypeEditor)),
		Editor(typeof(CollectionEditor), typeof(UITypeEditor)),
		Browsable(true)
		]
		public GLItemCollection Items
		{
			get	{ return m_Items; }
		}



		/// <summary>
		/// selection bar color
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Background color to mark selection."),
		Category("Item"),
		Browsable(true),
		]
		public Color SelectionColor
		{
			get	{ return m_colorSelectionColor; }
			set { m_colorSelectionColor = value; }
		}


		/// <summary>
		/// Selection Full Row
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Allow full row select."),
		Category("Item"),
		Browsable(true)
		]
		public bool FullRowSelect
		{
			get	{ return m_bFullRowSelect; }
			set	{ m_bFullRowSelect = value; }
		}


		/// <summary>
		/// Allow multiple row selection
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Allow multiple selections."),
		Category("Item"),
		Browsable(true)
		]
		public bool AllowMultiselect
		{
			get	{ return m_bMultiSelect; }
			set	{ m_bMultiSelect = value; }
		}


		/// <summary>
		/// Internal border padding
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Border Padding"),
		Category("Appearance"),
		Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
		]
		private int BorderPadding
		{
			get	
			{ 
				if ( ShowBorder )
					return 2; 
				else
					return 0;
			}
			//set	{ m_nBorderWidth = value; }
		}




		/// <summary>
		/// Grid Line Styles
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Whether or not to draw gridlines"),
		Category("Grid"),
		Browsable(true)
		]
		public GLGridLineStyles GridLineStyle
		{
			get	{ return m_GridLineStyle; }
			set
			{
				m_GridLineStyle = value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					//Invalidate();
					this.Parent.Invalidate(true);
				}
			}
		}

		/// <summary>
		/// What type of grid you want to draw
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Whether or not to draw gridlines"),
		Category("Grid"),
		Browsable(true)
		]
		public GLGridTypes GridTypes
		{
			get	{ return m_GridType; }
			set
			{
				m_GridType = value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					DI("Calling Invalidate From GLGridTypes");
					this.Parent.Invalidate(true);
				}
			}
		}


		/// <summary>
		/// Grid Lines Type
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Whether or not to draw gridlines"),
		Category("Grid"),
		Browsable(true)
		]
		public GLGridLines GridLines
		{
			get	{ return m_GridLines; }
			set
			{
				m_GridLines = value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					DI("Calling Invalidate From GLGridLines");
					this.Parent.Invalidate(true);
				}
			}
		}



		/// <summary>
		/// Color of grid lines.
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("Color of the grid if we draw it."),
		Category("Grid"),
		Browsable(true)
		]
		public Color GridColor
		{
			get	{ return m_colorGridColor; }
			set
			{
				m_colorGridColor = (Color)value;

				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					DI("Calling Invalidate From GridColor");
					this.Parent.Invalidate(true);
				}
			}
		}


		/// <summary>
		/// how big do we want the individual items to be
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("How high each row is."),
		Category("Item"),
		Browsable(true)
		]
		public int ItemHeight
		{
			get { return m_nItemHeight;	}
			set
			{
				//Debug.WriteLine( "Setting item height to " + value.ToString() );

				//if ( value == 15 )
				//Debug.WriteLine( "stop" );

				m_nItemHeight = value;
				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					DI("Calling Invalidate From ItemHeight");
					this.Parent.Invalidate(true);
				}
			}
		}


		/// <summary>
		/// Force header height.
		/// </summary>
		[
		RefreshProperties(RefreshProperties.Repaint),
		Description("How high the columns are."),
		Category("Header"),
		Browsable(true)
		]
		public int HeaderHeight
		{
			get
			{
				if ( HeaderVisible == true )
					return m_nHeaderHeight;
				else
					return 0;
			}
			set
			{
				m_nHeaderHeight = value;
				if ( ( this.DesignMode ) && ( this.Parent != null ) )
				{
					DI("Calling Invalidate From HeaderHeight");
					this.Parent.Invalidate(true);
				}
			}
		}


		/// <summary>
		/// amount of space inside any given cell to borders
		/// </summary>
		[
		Description("Cell padding area"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public int CellPaddingSize
		{
			get	{ return 2; }			// default I set to 4
		}

		#endregion

		#region Working Properties

		//private int								m_nSortIndex = 0;
		private bool							m_bThemesAvailable = false;

		private IntPtr							m_hTheme = IntPtr.Zero;


		/// <summary>
		/// Are themes available for this control?
		/// </summary>
		[
		Description("Are Themes Available"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		protected bool ThemesAvailable
		{
			get { return this.m_bThemesAvailable; }
		}


		/// <summary>
		/// returns a list of only the selected items
		/// </summary>
		[
		Description("Selected Items Array"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public ArrayList SelectedItems
		{
			get { return Items.SelectedItems; }
		}


		/// <summary>
		/// returns a list of only the selected items indexes
		/// </summary>
		[
		Description("Selected Items Array Of Indicies"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public ArrayList SelectedIndicies
		{
			get { return this.Items.SelectedIndicies; }
		}



		/// <summary>
		/// currently Hot Column
		/// </summary>
		[
		Description("Currently Focused Column"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public int HotColumnIndex
		{
			get 
			{
				return m_nHotColumnIndex;
			}
			set 
			{
				if ( m_bHotColumnTracking )
					if ( m_nHotColumnIndex != value )
					{
						m_nHotItemIndex = -1;
						m_nHotColumnIndex = value; 

						if ( !DesignMode )
						{
							DI("Calling Invalidate From HotColumnIndex");
							Invalidate(true);
						}
					}
			}
		}


		/// <summary>
		/// Current Hot Item
		/// </summary>
		[
		Description("Currently Focused Item"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public int HotItemIndex
		{
			get 
			{
				return m_nHotItemIndex;
			}
			set 
			{
				if ( m_bHotItemTracking )
					if ( m_nHotItemIndex != value )
					{
						m_nHotColumnIndex = -1;
						m_nHotItemIndex = value; 

						DI("Calling Invalidate From HotItemIndex");
						Invalidate(true);
					}
			}
		}


		/// <summary>
		/// Currently focused item
		/// </summary>
		[
		Description("Currently Focused Item"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public GLItem FocusedItem
		{
			get 
			{ 
				// need to make sure focused item actually exists
				if ( m_FocusedItem != null && Items.FindItemIndex( m_FocusedItem ) < 0 )
					m_FocusedItem = null;			// even though there is a focused item, it doesn't actually exist anymore

				return m_FocusedItem; 
			}
			set 
			{
				if ( m_FocusedItem != value )
				{
					m_FocusedItem = value; 
					if ( !DesignMode )
					{
						DI("Calling Invalidate From FocusedItem");
						Invalidate(true);
					}

					if ( this.SelectedIndexChanged != null )
						this.SelectedIndexChanged( this, new ClickEventArgs( Items.FindItemIndex( value ), -1 ) );			// never a column sent with selection index change
				}
			}
		}


		/// <summary>
		/// Current count of items in collection.
		/// </summary>
		[
		Description("Number of items/rows in the list."),
		Category("Behavior"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false),
		DefaultValue(0)
		]
		public int Count
		{
			get { return Items.Count; }
		}


		/// <summary>
		/// Calculates total height of all rows combined.
		/// </summary>
		[
		Description("All items together height."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		protected int TotalRowHeight
		{
			get
			{
				return ItemHeight * Items.Count;
			}
		}


		/// <summary>
		/// Number of rows currently visible
		/// </summary>
		[
		Description("Number of rows currently visible in inner rect."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		protected int VisibleRowsCount
		{
			get	{ return RowsInnerClientRect.Height / ItemHeight; }
		}


		/// <summary>
		/// Max Height of any given row at any given time.  Used with AutoHeight exclusively.
		/// </summary>
		[
		Description("this will always reflect the most height any item line has needed"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		protected int MaxHeight
		{
			get	{ return m_nMaxHeight; }
			set
			{
				if ( value > m_nMaxHeight )
				{
					m_nMaxHeight = value;
					if ( AutoHeight == true )
					{
						ItemHeight = MaxHeight;

						if ( !DesignMode)
						{
							DI("Calling Invalidate From MaxHeight");
							Invalidate(true);
						}
						DW("Item height set bigger");
					}
				}
			}
		}


		/// <summary>
		/// Rect of header area
		/// </summary>
		[
		Description("The rectangle of the header inside parent control"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		protected Rectangle HeaderRect
		{
			get	{ return new Rectangle( this.BorderPadding, this.BorderPadding, Width-(this.BorderPadding*2), HeaderHeight ); }
		}


		/// <summary>
		/// Row Client Rectangle
		/// </summary>
		[
		Description("The rectangle of the client inside parent control"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		protected Rectangle RowsClientRect
		{
			get
			{
				int tmpY = HeaderHeight + BorderPadding;							// size of the header and the top border

				int tmpHeight = Height - HeaderHeight - (BorderPadding*2);

				return new Rectangle( BorderPadding, tmpY, Width-(this.BorderPadding*2), tmpHeight );
			}
		}


		/// <summary>
		/// Full Sized rectangle of all columns total width.
		/// </summary>
		[
		Description("Full Sized rectangle of all columns total width."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public Rectangle RowsRect
		{
			get
			{
				Rectangle rect = new Rectangle();

				rect.X = -this.hPanelScrollBar.Value + BorderPadding;
				rect.Y = HeaderHeight + BorderPadding;
				rect.Width = Columns.Width;
				rect.Height = this.VisibleRowsCount * ItemHeight;

				return rect;
			}
		}


		/// <summary>
		/// The inner rectangle of the client inside parent control taking scroll bars into account.
		/// </summary>
		[
		Description("The inner rectangle of the client inside parent control taking scroll bars into account."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public Rectangle RowsInnerClientRect
		{
			get
			{
				Rectangle innerRect = RowsClientRect;

				innerRect.Width -= vPanelScrollBar.mWidth;				// horizontal bar crosses vertical plane and vice versa
				innerRect.Height -= hPanelScrollBar.mHeight;

				if ( innerRect.Width < 0 )
					innerRect.Width = 0;
				if ( innerRect.Height < 0 )
					innerRect.Height= 0;

				return innerRect;
			}
		}


		#endregion

		#endregion

		#endregion

		#region Implementation

		#region Initialization

		/// <summary>
		/// constructor
		/// </summary>
		public GlacialList()
		{
			DW("Constructor");

			m_Columns = new GLColumnCollection( this );
			m_Columns.ChangedEvent += new ChangedEventHandler( Columns_Changed );				// listen to event changes inside the item

			m_Items = new GLItemCollection( this );
			m_Items.ChangedEvent += new ChangedEventHandler( Items_Changed );

			//components = new System.ComponentModel.Container();
			InitializeComponent();


			Debug.WriteLine( this.Items.Count.ToString() );

			if ( !this.DesignMode )
			{
				if ( AreThemesAvailable() )
					this.m_bThemesAvailable = true;
				else
					this.m_bThemesAvailable = false;
			}

			this.TabStop = true;


			SetStyle(
				ControlStyles.AllPaintingInWmPaint |
				ControlStyles.ResizeRedraw |
				ControlStyles.Opaque |
				ControlStyles.UserPaint | 
				ControlStyles.DoubleBuffer |
				ControlStyles.Selectable | 
				ControlStyles.UserMouse, 
				true
				);

			this.BackColor = SystemColors.ControlLightLight;

			this.hPanelScrollBar = new GlacialComponents.Controls.ManagedHScrollBar();
			this.vPanelScrollBar = new GlacialComponents.Controls.ManagedVScrollBar();

			this.hPanelScrollBar.Scroll += new ScrollEventHandler(OnScroll);
			this.vPanelScrollBar.Scroll += new ScrollEventHandler(OnScroll);

			//
			// Creating borders
			//

			//Debug.WriteLine( "Creating borders" );
			this.vertLeftBorderStrip = new BorderStrip();
			this.vertRightBorderStrip = new BorderStrip();
			this.horiBottomBorderStrip = new BorderStrip();
			this.horiTopBorderStrip = new BorderStrip();
			this.cornerBox = new BorderStrip();



			this.SuspendLayout();
			// 
			// hPanelScrollBar
			// 
			this.hPanelScrollBar.Anchor = System.Windows.Forms.AnchorStyles.None;
			this.hPanelScrollBar.CausesValidation = false;
			this.hPanelScrollBar.Location = new System.Drawing.Point(24, 0);
			this.hPanelScrollBar.mHeight = 16;
			this.hPanelScrollBar.mWidth = 120;
			this.hPanelScrollBar.Name = "hPanelScrollBar";
			this.hPanelScrollBar.Size = new System.Drawing.Size(120, 16);
			this.hPanelScrollBar.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hPanelScrollBar_Scroll);
			this.hPanelScrollBar.Parent = this;
			this.Controls.Add( hPanelScrollBar );

			// 
			// vPanelScrollBar
			// 
			this.vPanelScrollBar.Anchor = System.Windows.Forms.AnchorStyles.None;
			this.vPanelScrollBar.CausesValidation = false;
			this.vPanelScrollBar.Location = new System.Drawing.Point(0, 12);
			this.vPanelScrollBar.mHeight = 120;
			this.vPanelScrollBar.mWidth = 16;
			this.vPanelScrollBar.Name = "vPanelScrollBar";
			this.vPanelScrollBar.Size = new System.Drawing.Size(16, 120);
			this.vPanelScrollBar.Scroll += new System.Windows.Forms.ScrollEventHandler(this.vPanelScrollBar_Scroll);
			this.vPanelScrollBar.Parent = this;
			this.Controls.Add( vPanelScrollBar );


			this.horiTopBorderStrip.Parent = this;
			this.horiTopBorderStrip.BorderType = BorderStrip.BorderTypes.btTop;
			this.horiTopBorderStrip.Visible = true;
			this.horiTopBorderStrip.BringToFront();


			//this.horiBottomBorderStrip.BackColor=Color.Black;
			this.horiBottomBorderStrip.Parent = this;
			this.horiBottomBorderStrip.BorderType = BorderStrip.BorderTypes.btBottom;
			this.horiBottomBorderStrip.Visible = true;
			this.horiBottomBorderStrip.BringToFront();

			//this.vertLeftBorderStrip.BackColor=Color.Black;
			this.vertLeftBorderStrip.BorderType = BorderStrip.BorderTypes.btLeft;
			this.vertLeftBorderStrip.Parent = this;
			this.vertLeftBorderStrip.Visible = true;
			this.vertLeftBorderStrip.BringToFront();

			//this.vertRightBorderStrip.BackColor=Color.Black;
			this.vertRightBorderStrip.BorderType = BorderStrip.BorderTypes.btRight;
			this.vertRightBorderStrip.Parent = this;
			this.vertRightBorderStrip.Visible = true;
			this.vertRightBorderStrip.BringToFront();

			this.cornerBox.BackColor = SystemColors.Control;
			this.cornerBox.BorderType = BorderStrip.BorderTypes.btSquare;
			this.cornerBox.Visible = false;
			this.cornerBox.Parent = this;
			this.cornerBox.BringToFront();

			this.Name = "GlacialList";

			this.ResumeLayout(false);
		}


		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			Debug.WriteLine( "Disposing Glacial List." );

			if ( m_hTheme != IntPtr.Zero )
				ThemeRoutines.CloseThemeData( m_hTheme );


			if( disposing )
			{
				if( components != null )
					components.Dispose();
			}
			base.Dispose( disposing );
		}


		#endregion

		#region Activated Embedded Routines


		/// <summary>
		/// If an activated embedded control exists, remove and unload it
		/// </summary>
		private void DestroyActivatedEmbedded()
		{
			if ( this.m_ActivatedEmbeddedControl != null )
			{
				GLEmbeddedControl control = (GLEmbeddedControl)this.m_ActivatedEmbeddedControl;
				control.GLUnload();

				// must do this because the unload may call the changed callback from the items which would call this routine a second time
				if ( this.m_ActivatedEmbeddedControl != null )
				{
					this.m_ActivatedEmbeddedControl.Dispose();
					this.m_ActivatedEmbeddedControl = null;
				}
			}
		}


		/// <summary>
		/// Instance the activated embeddec control for this item/column
		/// </summary>
		/// <param name="nColumn"></param>
		/// <param name="item"></param>
		/// <param name="subItem"></param>
		protected void ActivateEmbeddedControl( int nColumn, GLItem item, GLSubItem subItem )
		{
			if ( this.m_ActivatedEmbeddedControl != null )
			{
				this.m_ActivatedEmbeddedControl.Dispose();
				this.m_ActivatedEmbeddedControl = null;
			}


			/*
			using activator.createinstance
			typeof()/GetType
			Type t = obj.GetType()
			 */

			if ( Columns[nColumn].ActivatedEmbeddedControlTemplate == null )
				return;


			Type type = Columns[nColumn].ActivatedEmbeddedControlTemplate.GetType();
			Control control = (Control)Activator.CreateInstance( type );
			GLEmbeddedControl icontrol = (GLEmbeddedControl)control;

			if ( icontrol == null )
				throw new Exception( @"Control does not implement the GLEmbeddedControl interface, can't start" );

			icontrol.GLLoad( item, subItem, this );


			//control.LostFocus += new EventHandler( ActivatedEmbbed_LostFocus );
			control.KeyPress += new KeyPressEventHandler(tb_KeyPress);

			control.Parent = this;
			this.ActivatedEmbeddedControl = control;
			//subItem.Control = control;							// seed the control


			int nYOffset = (subItem.LastCellRect.Height - m_ActivatedEmbeddedControl.Bounds.Height) / 2;
			Rectangle controlBounds;

			if ( this.GridLineStyle == GLGridLineStyles.gridNone )
			{
				// add 1 to x to give border, add 2 to Y because to account for possible grid that you must cover up
				controlBounds = new Rectangle( subItem.LastCellRect.X+1, subItem.LastCellRect.Y+1, subItem.LastCellRect.Width-3, subItem.LastCellRect.Height-2 );
			}
			else
			{
				// add 1 to x to give border, add 2 to Y because to account for possible grid that you must cover up
				controlBounds = new Rectangle( subItem.LastCellRect.X+1, subItem.LastCellRect.Y+2, subItem.LastCellRect.Width-3, subItem.LastCellRect.Height-3 );
			}
			//control.Bounds = subItem.LastCellRect;	//new Rectangle( subItem.LastCellRect.X, subItem.LastCellRect.Y + nYOffset, subItem.LastCellRect.Width, subItem.LastCellRect.Height );
			control.Bounds = controlBounds;

			control.Show();
			control.Focus();
		}


		/// <summary>
		/// check for return (if we get it, deactivate)
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void tb_KeyPress(object sender, KeyPressEventArgs e)
		{
			if ( ( e.KeyChar == (char)Keys.Return ) || ( e.KeyChar == (char)Keys.Escape ) )
				this.DestroyActivatedEmbedded();
		}


		//		/// <summary>
		//		/// handle when a control has lost focus
		//		/// </summary>
		//		/// <param name="sender"></param>
		//		/// <param name="e"></param>
		//		private void ActivatedEmbbed_LostFocus(object sender, EventArgs e)
		//		{
		//			Debug.WriteLine( "Embedded control lost focus." );
		//		}



		#endregion

		#region System Overrides

#if false
		/// <summary>
		/// stop keys from going to scrollbars
		/// </summary>
		/// <param name="m"></param>
		protected override void WndProc(ref Message msg)
		{
			base.WndProc(ref msg);
			if (msg.Msg == (int)WIN32Codes.WM_GETDLGCODE)
			{
				msg.Result = new IntPtr((int)DialogCodes.DLGC_WANTCHARS | (int)DialogCodes.DLGC_WANTARROWS | msg.Result.ToInt32());
			}
		}
#endif


		/// <summary>
		/// keep certain keys here
		/// </summary>
		/// <param name="msg"></param>
		/// <returns></returns>
		public override bool PreProcessMessage(ref Message msg)
		{
			DW( "PreProcessMessage " + msg.ToString() );
			//return base.PreProcessMessage(ref msg);


			if (msg.Msg == WM_KEYDOWN)
			{
				Keys keyCode = ((Keys) (int) msg.WParam);		// this should turn the key data off because it will match selected keys to ORA them off


				if ( keyCode == Keys.Return )
				{
					DestroyActivatedEmbedded();
					return true;
				}

				//				Debug.WriteLine("---");
				//				Debug.WriteLine( ModifierKeys.ToString() );
				Debug.WriteLine( keyCode.ToString() );

				if ( ( FocusedItem != null ) && ( Count > 0 ) && ( this.Selectable ) ) 
				{
					int nItemIndex = Items.FindItemIndex( FocusedItem );
					int nPreviousIndex = nItemIndex;

					if ( nItemIndex < 0 )
						return true; // this can't move


					if ( ( keyCode == Keys.A ) && ( (ModifierKeys & Keys.Control) == Keys.Control ) )
					{
						for (int index=0; index<Items.Count; index++ )
							Items[index].Selected = true;

						return base.PreProcessMessage(ref msg);
					}

					if (keyCode == Keys.Escape )
					{
						Items.ClearSelection();			// clear selections
						this.FocusedItem = null;

						return base.PreProcessMessage(ref msg);						
					}

					if (keyCode == Keys.Down) 
					{ // Could be a switch
						nItemIndex++;
					} 
					else if (keyCode == Keys.Up) 
					{
						nItemIndex--;
					} 
					else if (keyCode == Keys.PageDown ) 
					{
						nItemIndex+=this.VisibleRowsCount;
					} 
					else if (keyCode == Keys.PageUp ) 
					{
						nItemIndex-=this.VisibleRowsCount;
					} 
					else if (keyCode == Keys.Home) 
					{
						nItemIndex=0;
					} 
					else if (keyCode == Keys.End) 
					{
						nItemIndex = Count-1;
					} 
					else if (keyCode == Keys.Space) 
					{
						if ( !this.AllowMultiselect )
							this.Items.ClearSelection( Items[nItemIndex] );

						Items[nItemIndex].Selected = !Items[nItemIndex].Selected;

						return base.PreProcessMessage(ref msg);
					} 
					else 
					{
						return base.PreProcessMessage(ref msg);		// bail out, they only pressed a key we didn't care about (probably a modifier)
					}

					// bounds check them
					if ( nItemIndex > Count-1 )
						nItemIndex = Count-1;
					if ( nItemIndex < 0 )
						nItemIndex = 0;


					// move view.  Need to move end -1 to take into account 0 based index
					if (nItemIndex < this.vPanelScrollBar.Value) // its out of viewable, move the surface
						this.vPanelScrollBar.Value = nItemIndex;
					if ( nItemIndex > ( this.vPanelScrollBar.Value+(this.VisibleRowsCount-1) ))
						this.vPanelScrollBar.Value = nItemIndex - (this.VisibleRowsCount-1);


					if(nPreviousIndex != nItemIndex) 
					{
						if((ModifierKeys & Keys.Control) != Keys.Control && (ModifierKeys & Keys.Shift) != Keys.Shift) 
						{	// no control no shift
							m_nLastSelectionIndex = nItemIndex;
							Items[nItemIndex].Selected = true;
							Items.ClearSelection( Items[nItemIndex] );
						} 
						else if((ModifierKeys & Keys.Shift) == Keys.Shift) 
						{	// shift only
							this.Items.ClearSelection();

							// gotta catch when the multi select is NOT set
							if ( !this.AllowMultiselect )
							{
								Items[nItemIndex].Selected = !Items[nItemIndex].Selected;
							}
							else
							{
								
								if ( m_nLastSelectionIndex >= 0 )			// ie, non negative so that we have a starting point
								{
									int index = m_nLastSelectionIndex;
									do
									{
										Items[index].Selected = true;
										if ( index > nItemIndex )		index--;
										if ( index < nItemIndex )		index++;
									} while ( index != nItemIndex );

									Items[index].Selected = true;
								}
							}
						}
						else 
						{	// control only
							m_nLastSelectionIndex = nItemIndex;
						}

						// Bypass FocusedItem property, we always want to invalidate from this point
						FocusedItem = Items[nItemIndex];
					}

				}
				else
				{	// only if non selectable
					int nMoveIndex = this.vPanelScrollBar.Value;

					if (keyCode == Keys.Down) 
					{ // Could be a switch
						nMoveIndex++;
					} 
					else if (keyCode == Keys.Up) 
					{
						nMoveIndex--;
					} 
					else if (keyCode == Keys.PageDown ) 
					{
						nMoveIndex += this.VisibleRowsCount;
					} 
					else if (keyCode == Keys.PageUp ) 
					{
						nMoveIndex -= this.VisibleRowsCount;
					} 
					else if (keyCode == Keys.Home) 
					{
						nMoveIndex = 0;
					} 
					else if (keyCode == Keys.End) 
					{
						nMoveIndex = Count-this.VisibleRowsCount;
					} 
					else
						return base.PreProcessMessage(ref msg);			// we don't know how to deal with this key


					if ( nMoveIndex > ( Count-this.VisibleRowsCount ) )
						nMoveIndex = Count-this.VisibleRowsCount;
					if ( nMoveIndex < 0 )
						nMoveIndex = 0;


					if ( this.vPanelScrollBar.Value != nMoveIndex )
					{
						this.vPanelScrollBar.Value = nMoveIndex;

						DI("Calling Invalidate From PreProcessMessage");
						this.Invalidate();
					}
				}

			}
			else
				return base.PreProcessMessage(ref msg);			// handle ALL other messages


			return true;
		}


		#endregion

		#region Event Handlers

		/// <summary>
		/// Timer handler.  This mostly deals with the hover technology with events firing.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void m_TimerTick(object sender, EventArgs e)
		{
#if false
			//Control.MousePosition.X
			int nx = Control.MousePosition.X;
			int ny = Control.MousePosition.Y;
			Debug.WriteLine( "Control " + nx.ToString() + " " + ny.ToString() );


			nx = Cursor.Position.X;
			ny = Cursor.Position.Y;
			Debug.WriteLine( "Cursor " + nx.ToString() + " " + ny.ToString() );
#endif

			// make sure hover is actually inside control too
			Point pointLocalMouse;
			if ( Cursor != null )
				pointLocalMouse = this.PointToClient( Cursor.Position );
			else
				pointLocalMouse = new Point( 9999, 9999 );

			int nItem = 0, nColumn = 0, nCellX = 0, nCellY = 0;
			ListStates eState;
			GLListRegion listRegion;
			InterpretCoords( pointLocalMouse.X, pointLocalMouse.Y, out listRegion, out nCellX, out nCellY, out nItem, out nColumn, out eState );


			if ( ( pointLocalMouse == this.m_ptLastHoverSpot ) && (!m_bHoverLive) && ( listRegion != GLListRegion.nonclient )  )
			{
				Debug.WriteLine( "Firing Hover" );

				if (this.HoverEvent != null)
					this.HoverEvent( this, new HoverEventArgs( HoverTypes.HoverStart, nItem, nColumn, listRegion ) );

				m_bHoverLive = true;
			}
			else if ( ( m_bHoverLive ) && ( pointLocalMouse != this.m_ptLastHoverSpot ) )
			{
				Debug.WriteLine( "Cancelling Hover" );

				if (this.HoverEvent != null)
					this.HoverEvent( this, new HoverEventArgs( HoverTypes.HoverEnd, -1, -1, GLListRegion.nonclient ) );

				m_bHoverLive = false;
			}

			this.m_ptLastHoverSpot = pointLocalMouse;
		}


		/// <summary>
		/// Item has changed, fire event
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		protected void Items_Changed( object source, ChangedEventArgs e )
		{
			DW("GlacialList::Items_Changed");

			//Debug.WriteLine( e.ChangedType.ToString() );
			//if ( e.ChangedType != ChangedTypes.

			// kill activated embedded object
			this.DestroyActivatedEmbedded();

			if ( ItemChangedEvent != null )
				ItemChangedEvent( this, e );				// fire the column clicked event

			// only invalidate if an item that is within the visible area has changed
			if ( e.Item != null )
			{

				//				int nItemIndex = Items.FindItemIndex( e.Item );
				//				if ( ( nItemIndex >= this.vPanelScrollBar.Value ) && ( nItemIndex <  this.vPanelScrollBar.Value+this.VisibleRowsCount ) )
				if ( IsItemVisible( e.Item ) )
				{
					DI("Calling Invalidate From Items_Changed");
					Invalidate();
				}
			}
		}


		/// <summary>
		/// Column has changed, fire event
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		public void Columns_Changed( object source, ChangedEventArgs e )
		{
			DW("Columns_Changed");

			if ( e.ChangedType != ChangedTypes.ColumnStateChanged )
				this.DestroyActivatedEmbedded();			// kill activated embedded object

			if ( ColumnChangedEvent != null )
				ColumnChangedEvent( this, e );				// fire the column clicked event

			DI("Calling Invalidate From Columns_Changed");
			Invalidate();
		}


		/// <summary>
		/// When the control receives focus
		/// 
		/// this routine is the one that makes absolute certain if the embedded control loses focus then 
		/// the embedded control is destroyed
		/// </summary>
		/// <param name="e"></param>
		protected override void OnGotFocus(EventArgs e)
		{
			DestroyActivatedEmbedded();

			base.OnGotFocus (e);
		}


		#endregion

		#region HelperFunctions

		/// <summary>
		/// This is an OPTIMIZED routine to see if an item is visible.
		/// 
		/// The other method of just checking against the item index was slow becuase it had to walk the entire list, which would massively
		/// slow down the control when large numbers of items were added.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool IsItemVisible( GLItem item )
		{
			// TODO: change this to only walk to visible items list
			int nItemIndex = Items.FindItemIndex( item );
			if ( ( nItemIndex >= this.vPanelScrollBar.Value ) && ( nItemIndex <  this.vPanelScrollBar.Value+this.VisibleRowsCount ) )
				return true;

			return false;
		}

		/// <summary>
		/// Tell paint to stop worry about updates
		/// </summary>
		public void BeginUpdate()
		{
			m_bUpdating = true;
		}


		/// <summary>
		/// Tell paint to start worrying about updates again and repaint while your at it
		/// </summary>
		public void EndUpdate()
		{
			m_bUpdating = false;
			Invalidate();
		}


		/// <summary>
		/// interpret mouse coordinates
		/// 
		/// ok, I've violated the spirit of this routine a couple times (but no more!).  Do NOT put anything
		/// functional in this routine.  It is ONLY for analyzing the mouse coordinates.  Do not break this again!
		/// </summary>
		/// <param name="nScreenX"></param>
		/// <param name="nScreenY"></param>
		/// <param name="listRegion"></param>
		/// <param name="nCellX"></param>
		/// <param name="nCellY"></param>
		/// <param name="nItem"></param>
		/// <param name="nColumn"></param>
		/// <param name="nState"></param>
		public void InterpretCoords( int nScreenX, int nScreenY, out GLListRegion listRegion, out int nCellX, out int nCellY, out int nItem, out int nColumn, out ListStates nState )
		{
			DW("Interpret Coords");

			nState = ListStates.stateNone;
			nColumn = 0;		// compiler forces me to set this since it sometimes wont get set if routine falls through early
			nItem = 0;
			nCellX = 0;
			nCellY = 0;

			listRegion = GLListRegion.nonclient;

			/*
			 * Calculate horizontal subitem
			 */
			int nCurrentX = -hPanelScrollBar.Value;						// offset the starting point by the current scroll point

			for ( nColumn=0; nColumn < Columns.Count; nColumn++ )
			{
				GLColumn col = Columns[nColumn];
				// lets find the inner X for the cell
				nCellX = nScreenX - nCurrentX;

				if ( (nScreenX > nCurrentX) && (nScreenX < (nCurrentX+col.Width-RESIZE_ARROW_PADDING)) )
				{
					nState = ListStates.stateColumnSelect;

					break;
				}
				if ( (nScreenX >= (nCurrentX+col.Width-RESIZE_ARROW_PADDING)) && (nScreenX <= (nCurrentX+col.Width+RESIZE_ARROW_PADDING)) )
				{
					// here we need to check see if this is a 0 length column (which we skip to next on) or if this is the last column (which we can't skip)
					if ( (nColumn+1 == Columns.Count) || ( Columns[nColumn+1].Width != 0 ) )
					{
						if ( AllowColumnResize == true )
							nState = ListStates.stateColumnResizing;

						//Debug.WriteLine( "Sending our column number " + nColumn.ToString() );
						return;				// no need for this to fall through
					}
				}

				nCurrentX += col.Width;
			}

			if ( ( nScreenY >= RowsInnerClientRect.Y ) && ( nScreenY < RowsInnerClientRect.Bottom ) )
			{	// we are in the client area
				listRegion = GLListRegion.client;

				Columns.ClearHotStates();
				this.HotColumnIndex = -1;

				nItem = ((nScreenY - RowsInnerClientRect.Y) / ItemHeight) + vPanelScrollBar.Value;

				// get inner cell Y
				nCellY = (nScreenY - RowsInnerClientRect.Y) % ItemHeight;



				this.HotItemIndex = nItem;

				if ( ( nItem >= Items.Count ) || ( nItem > (this.vPanelScrollBar.Value + this.VisibleRowsCount) ) )
				{
					nState = ListStates.stateNone;
					listRegion = GLListRegion.nonclient;
				}
				else
				{
					nState = ListStates.stateSelecting;

					// handle case of where FullRowSelect is OFF and we click on the second part of a spanned column
					for ( int nSubIndex = 0; nSubIndex < Columns.Count; nSubIndex++ )
					{
						//if ( ( nSubIndex + (Items[nItem].SubItems[nSubIndex].Span-1) ) >= nColumn )
						if ( nSubIndex >= nColumn )
						{
							nColumn = nSubIndex;
							return;
						}
					}
				}

				//Debug.WriteLine( "returning client from interpretcoords" );

				return;
			}
			else
			{
				if ( ( nScreenY >= this.HeaderRect.Y ) && ( nScreenY < this.HeaderRect.Bottom ) )
				{
					//Debug.WriteLine( "Found header from interpret coords" );

					listRegion = GLListRegion.header;

					this.HotItemIndex = -1;			// we are in the header
					this.HotColumnIndex = nColumn;

					if ( ( ( nColumn > -1 ) && ( nColumn < Columns.Count ) ) && (!Columns.AnyPressed() ) )
						if ( Columns[nColumn].State == ColumnStates.csNone)
						{
							Columns.ClearHotStates();
							Columns[nColumn].State = ColumnStates.csHot;
						}
				}
			}
			return;
		}


		/// <summary>
		/// return the X starting point of a particular column
		/// </summary>
		/// <param name="nColumn"></param>
		/// <returns></returns>
		public int GetColumnScreenX( int nColumn )
		{
			DW("Get Column Screen X");

			if ( nColumn >= Columns.Count )
				return 0;

			int nCurrentX = -hPanelScrollBar.Value;//GetHScrollPoint();			// offset the starting point by the current scroll point
			int nColIndex = 0;
			foreach ( GLColumn col in Columns )
			{
				if ( nColIndex >= nColumn )
					return nCurrentX;

				nColIndex++;
				nCurrentX += col.Width;
			}

			return 0;		// this should never happen;
		}


		/// <summary>
		/// Sort a column.
		/// 
		/// Set to virtual so you can write your own sorting
		/// </summary>
		/// <param name="nColumn"></param>
		public virtual void SortColumn( int nColumn )
		{
			Debug.WriteLine( "Column sorting called." );

			if ( Count < 2 )			// nothing to sort
				return;


			if ( SortType == SortTypes.InsertionSort )
			{
				GLQuickSort sorter = new GLQuickSort();

				sorter.NumericCompare = Columns[nColumn].NumericSort;
				sorter.SortDirection = Columns[nColumn].LastSortState;
				sorter.SortColumn = nColumn;
				sorter.GLInsertionSort( Items, 0, Items.Count-1 );
			}
			else if ( SortType == SortTypes.MergeSort )
			{
				//this.SortIndex = nColumn;
				GLMergeSort mergesort = new GLMergeSort();

				mergesort.NumericCompare = Columns[nColumn].NumericSort;
				mergesort.SortDirection = Columns[nColumn].LastSortState;
				mergesort.SortColumn = nColumn;
				mergesort.sort( Items, 0, Items.Count-1 );
			}
			else if ( SortType == SortTypes.QuickSort )
			{
				GLQuickSort sorter = new GLQuickSort();

				sorter.NumericCompare = Columns[nColumn].NumericSort;
				sorter.SortDirection = Columns[nColumn].LastSortState;
				sorter.SortColumn = nColumn;
				sorter.sort( Items );	//.QuickSort( Items, 0, Items.Count-1 );
			}


			if ( Columns[nColumn].LastSortState == SortDirections.SortDescending )
				Columns[nColumn].LastSortState = SortDirections.SortAscending;
			else
				Columns[nColumn].LastSortState = SortDirections.SortDescending;

			//Items.Sort();
		}


		/// <summary>
		/// see if themes are available
		/// </summary>
		/// <returns></returns>
		protected bool AreThemesAvailable()
		{
			DW("AreThemesAvailable");

			//IntPtr hTheme = IntPtr.Zero;

			try
			{
				if ((ThemeRoutines.IsThemeActive() == 1)  && (m_hTheme == IntPtr.Zero)) 
				{
					m_hTheme = ThemeRoutines.OpenThemeData( m_hTheme, "HEADER" );

					return true;
				}
			}
			catch( Exception ex )
			{
				Debug.WriteLine( ex.ToString() );
			}

			return false;
		}


		#endregion

		#region Dimensions

		/// <summary>
		/// Control is resizing, handle invalidations
		/// </summary>
		/// <param name="e"></param>
		protected override void OnResize(EventArgs e)
		{
			DW("GlacialList_Resize");

			//RecalcScroll();

			DI("Calling Invalidate From OnResize");
			Invalidate();
		}


		#endregion

		#region Drawing


		/// <summary>
		/// Entry point to paint routines
		/// </summary>
		/// <param name="e"></param>
		protected override void OnPaint(PaintEventArgs e)
		{
			DW("Paint");

			if ( !this.DesignMode && ( m_bUpdating ) )		// my best guess on how to implement updating functionality				
				return;

			RecalcScroll();			// at some point I need to move this out of paint.  Doesn't really belong here.


			//Debug.WriteLine( "Redraw called " + DateTime.Now.ToLongTimeString() );
			Graphics g = e.Graphics;



			//if ( Columns.Count > 0 )
		{
			int nInsideWidth;
			if ( Columns.Width > HeaderRect.Width )
				nInsideWidth = Columns.Width;
			else
				nInsideWidth = HeaderRect.Width;

			/*
				 * draw header
				 */
			if ( HeaderVisible == true )
			{
				g.SetClip( HeaderRect );
				DrawHeader( g, new Size( HeaderRect.Width, HeaderRect.Height ) );
			}


			/*
				 * draw client area
				 */
			g.SetClip( RowsInnerClientRect );
			DrawRows( g );

			// very optimized way of removing controls that aren't visible anymore without having to iterate the entire items list
			foreach( Control control in LiveControls )
			{
				Debug.WriteLine( "Setting " + control.ToString() + " to hidden." );
				control.Visible = false;					// make sure the controls that aren't visible aren't shown
			}
			LiveControls = NewLiveControls;
			NewLiveControls = new ArrayList();
		}

			g.SetClip( this.ClientRectangle );


			base.OnPaint( e );
		}


		/// <summary>
		/// Draw Header Control
		/// </summary>
		/// <param name="graphicHeader"></param>
		/// <param name="sizeHeader"></param>
		virtual public void DrawHeader( Graphics graphicHeader, /*Bitmap bmpHeader,*/ Size sizeHeader )
		{
			DW("DrawHeader");

			if ( this.ControlStyle == GLControlStyles.SuperFlat )
			{
				SolidBrush brush = new SolidBrush( this.SuperFlatHeaderColor );
				graphicHeader.FillRectangle( brush, HeaderRect );
				brush.Dispose();
			}
			else
			{
				graphicHeader.FillRectangle( SystemBrushes.Control, HeaderRect );
			}


			if ( Columns.Count <= 0 )
				return;

			// draw vertical lines first, then horizontal lines
			int nCurrentX = (-this.hPanelScrollBar.Value) + HeaderRect.X;
			foreach ( GLColumn column in Columns )
			{
				// cull columns that won't be drawn first
				if ( ( nCurrentX + column.Width ) < 0 )
				{
					nCurrentX += column.Width;
					continue;							// skip this column, its not being drawn
				}
				if ( nCurrentX > HeaderRect.Right )		
					return;								// were past the end of the visible column, stop drawing

				if ( column.Width > 0 )
					DrawColumnHeader( graphicHeader, new Rectangle( nCurrentX, HeaderRect.Y, column.Width, HeaderHeight ), column );

				nCurrentX += column.Width;				// move the parser
			}
		}


		/// <summary>
		/// Draw column in header control
		/// </summary>
		/// <param name="graphicsColumn"></param>
		/// <param name="rectColumn"></param>
		/// <param name="column"></param>
		virtual public void DrawColumnHeader( Graphics graphicsColumn, Rectangle rectColumn, GLColumn column )
		{
			DW("DrawColumn");

			if ( this.ControlStyle == GLControlStyles.SuperFlat )
			{
				SolidBrush brush = new SolidBrush( this.SuperFlatHeaderColor );
				graphicsColumn.FillRectangle( brush, rectColumn );
				brush.Dispose();
			}
			else if (( this.ControlStyle == GLControlStyles.XP )&& this.ThemesAvailable )
			{	// this is really the only thing we care about for themeing right now inside the control
				System.IntPtr hDC = graphicsColumn.GetHdc();;

				RECT colrect = new RECT( rectColumn.X, rectColumn.Y, rectColumn.Right, rectColumn.Bottom );
				RECT cliprect = new RECT( rectColumn.X, rectColumn.Y, rectColumn.Right, rectColumn.Bottom );

				if ( column.State == ColumnStates.csNone )
				{
					//Debug.WriteLine( "Normal" );
					ThemeRoutines.DrawThemeBackground( m_hTheme, hDC, 1, 1, ref colrect, ref cliprect );
				}
				else if ( column.State == ColumnStates.csPressed )
				{
					//Debug.WriteLine( "Pressed" );
					ThemeRoutines.DrawThemeBackground( m_hTheme, hDC, 1, 3, ref colrect, ref cliprect );
				}
				else if ( column.State == ColumnStates.csHot )
				{
					//Debug.WriteLine( "Hot" );
					ThemeRoutines.DrawThemeBackground( m_hTheme, hDC, 1, 2, ref colrect, ref cliprect );
				}

				graphicsColumn.ReleaseHdc(hDC);
			}
			else		// normal state
			{
				if ( column.State != ColumnStates.csPressed )
					ControlPaint.DrawButton( graphicsColumn, rectColumn, ButtonState.Normal );
				else
					ControlPaint.DrawButton( graphicsColumn, rectColumn, ButtonState.Pushed );
			}


			// if there is an image, this routine will RETURN with exactly the space left for everything else after the image is drawn (or not drawn due to lack of space)
			if ( (column.ImageIndex > -1) && (ImageList != null) && (column.ImageIndex < this.ImageList.Images.Count) )
				rectColumn = DrawCellGraphic( graphicsColumn, rectColumn, this.ImageList.Images[ column.ImageIndex ], HorizontalAlignment.Left );

			DrawCellText( graphicsColumn, rectColumn, column.Text, column.TextAlignment, this.ForeColor, false, HeaderWordWrap );
		}



		/// <summary>
		/// Draw client rows of list control
		/// </summary>
		/// <param name="graphicsRows"></param>
		virtual public void DrawRows( Graphics graphicsRows )
		{
			DW("DrawRows");


			SolidBrush brush = new SolidBrush( this.BackColor );
			graphicsRows.FillRectangle( brush, this.RowsClientRect );
			brush.Dispose();

			// if they have a background image, then display it
			if ( this.BackgroundImage != null )
			{
				if ( this.BackgroundStretchToFit )
					graphicsRows.DrawImage( this.BackgroundImage, this.RowsInnerClientRect.X, this.RowsInnerClientRect.Y, this.RowsInnerClientRect.Width, this.RowsInnerClientRect.Height );
				else
					graphicsRows.DrawImage( this.BackgroundImage, this.RowsInnerClientRect.X, this.RowsInnerClientRect.Y );
			}


			// determine start item based on whether or not we have a vertical scrollbar present
			int nStartItem;				// which item to start with in this visible pane
			if ( this.vPanelScrollBar.Visible == true )
				nStartItem = this.vPanelScrollBar.Value;
			else
				nStartItem = 0;


			Rectangle rectRow = this.RowsRect;	
			rectRow.Height = ItemHeight;

			/* Draw Rows */
			for ( int nItem = 0; ((nItem < (VisibleRowsCount +1) ) && ((nItem+nStartItem) < Items.Count )); nItem++ )
			{
				DrawRow( graphicsRows, rectRow, this.Items[ nItem+nStartItem ], nItem+nStartItem );
				rectRow.Y += ItemHeight;
			}


			if ( GridLineStyle != GLGridLineStyles.gridNone )
				DrawGridLines( graphicsRows, this.RowsInnerClientRect );


			// draw hot tracking column overlay
			if ( this.HotColumnTracking && ( this.HotColumnIndex != -1 ) && ( HotColumnIndex < Columns.Count ) )
			{
				int nXCursor = -this.hPanelScrollBar.Value;
				for ( int nColumnIndex = 0; nColumnIndex < this.HotColumnIndex; nColumnIndex++ )
					nXCursor += Columns[nColumnIndex].Width;

				Brush hotBrush = new SolidBrush(Color.FromArgb( 75, this.HotTrackingColor.R, this.HotTrackingColor.G, this.HotTrackingColor.B ) );
				graphicsRows.FillRectangle( hotBrush, nXCursor, RowsInnerClientRect.Y, Columns[HotColumnIndex].Width+1, RowsInnerClientRect.Height-1 );

				hotBrush.Dispose();
			}

		}


		/// <summary>
		/// Draw row at specified coordinates
		/// </summary>
		/// <param name="graphicsRow"></param>
		/// <param name="rectRow"></param>
		/// <param name="item"></param>
		/// <param name="nItemIndex"></param>
		virtual public void DrawRow( Graphics graphicsRow, Rectangle rectRow, GLItem item, int nItemIndex )
		{
			DW("DrawRow");


			// row background, if its selected, that trumps all, if not then see if we are using alternating colors, if not draw normal
			// note, this can all be overridden by the sub item background property
			// make sure anything can even be selected before drawing selection rects
			if ( item.Selected && this.Selectable )
			{
				SolidBrush brushBK;
				brushBK = new SolidBrush( Color.FromArgb( 255, SelectionColor.R, SelectionColor.G, SelectionColor.B ) );

				// need to check for full row select here
				if ( !FullRowSelect )
				{	// calculate how far into the control it goes
					int nWidthFR = -this.hPanelScrollBar.Value + Columns.Width;
					graphicsRow.FillRectangle( brushBK, this.RowsInnerClientRect.X, rectRow.Y, nWidthFR, rectRow.Height );
				}
				else
					graphicsRow.FillRectangle( brushBK, this.RowsInnerClientRect.X, rectRow.Y, this.RowsInnerClientRect.Width, rectRow.Height );

				brushBK.Dispose();
			}
			else
			{

				// if the back color of the list doesn't match the back color of the item (AND) the back color isn't white, then override it
				if ( ( item.BackColor.ToArgb() != this.BackColor.ToArgb() ) && (item.BackColor != Color.White ) )
				{
					SolidBrush brushBK = new SolidBrush( item.BackColor );
					graphicsRow.FillRectangle( brushBK, this.RowsInnerClientRect.X, rectRow.Y, this.RowsInnerClientRect.Width, rectRow.Height );
					brushBK.Dispose();
				}	// check for full row alternate color
				else if ( this.AlternatingColors )
				{	// alternating colors are only shown if the row isn't selected
					int nACItemIndex = Items.FindItemIndex( item );
					if ( ( nACItemIndex % 2 ) > 0 )
					{
						SolidBrush brushBK = new SolidBrush( this.AlternateBackground );

						if ( !FullRowSelect )
						{	// calculate how far into the control it goes
							int nWidthFR = -this.hPanelScrollBar.Value + Columns.Width;
							graphicsRow.FillRectangle( brushBK, this.RowsInnerClientRect.X, rectRow.Y, nWidthFR, rectRow.Height );
						}
						else
							graphicsRow.FillRectangle( brushBK, this.RowsInnerClientRect.X, rectRow.Y, this.RowsInnerClientRect.Width, rectRow.Height );

						brushBK.Dispose();
					}
				}
			}


			// draw the row of sub items
			int nXCursor = -this.hPanelScrollBar.Value + this.BorderPadding;
			for ( int nSubItem = 0; nSubItem < Columns.Count; nSubItem++ )
			{
				Rectangle rectSubItem = new Rectangle( nXCursor, rectRow.Y, Columns[nSubItem].Width, rectRow.Height );

				// avoid drawing items that are not in the visible region
				if ( ( rectSubItem.Right < 0 ) || ( rectSubItem.Left > this.RowsInnerClientRect.Right ) )
					Debug.Write( "" );
				else
					DrawSubItem( graphicsRow, rectSubItem, item, item.SubItems[nSubItem], nSubItem );

				nXCursor += Columns[nSubItem].Width;
			}


			// post draw for focus rect and hot tracking
			if ( ( nItemIndex == this.HotItemIndex ) && HotItemTracking )									// handle hot tracking of items
			{
				Color transparentColor = Color.FromArgb( 75, this.HotTrackingColor.R, this.HotTrackingColor.G, this.HotTrackingColor.B );		// 182, 189, 210 );
				Brush hotBrush = new SolidBrush(transparentColor);

				graphicsRow.FillRectangle( hotBrush, this.RowsInnerClientRect.X, rectRow.Y, this.RowsInnerClientRect.Width, rectRow.Height );

				hotBrush.Dispose();
			}


			// draw row borders
			if ( item.RowBorderSize > 0 )
			{
				Pen penBorder = new Pen( item.RowBorderColor, item.RowBorderSize );
				penBorder.Alignment = PenAlignment.Inset;
				graphicsRow.DrawRectangle( penBorder, rectRow );
				penBorder.Dispose();
			}


			// make sure anything can even be selected before drawing selection rects
			if ( this.Selectable )
				if ( this.ShowFocusRect && (FocusedItem == item) )												// deal with focus rect
					ControlPaint.DrawFocusRectangle( graphicsRow, new Rectangle( this.RowsInnerClientRect.X+1, rectRow.Y, this.RowsInnerClientRect.Width-1, rectRow.Height ) );
		}


		/// <summary>
		/// Draw Sub Item (Cell) at location specified
		/// </summary>
		/// <param name="graphicsSubItem"></param>
		/// <param name="rectSubItem"></param>
		/// <param name="item"></param>
		/// <param name="subItem"></param>
		/// <param name="nColumn"></param>
		virtual public void DrawSubItem( Graphics graphicsSubItem, Rectangle rectSubItem, GLItem item, GLSubItem subItem, int nColumn )
		{
			DW("DrawSubItem");

			// precheck to make sure this is big enough for the things we want to do inside it
			Rectangle subControlRect = new Rectangle( rectSubItem.X, rectSubItem.Y, rectSubItem.Width, rectSubItem.Height );


			if ( ( subItem.Control != null ) && (!subItem.ForceText ) )
			{	// custom embedded control here

				Control control = subItem.Control;

				if ( control.Parent != this )		// *** CRUCIAL *** this makes sure the parent is the list control
					control.Parent = this;

				//				Rectangle subrc = new Rectangle( 
				//					subControlRect.X+this.CellPaddingSize, 
				//					subControlRect.Y+this.CellPaddingSize, 
				//					subControlRect.Width-this.CellPaddingSize*2,
				//					subControlRect.Height-this.CellPaddingSize*2 );


				Rectangle subrc = new Rectangle( 
					subControlRect.X, 
					subControlRect.Y+1, 
					subControlRect.Width,
					subControlRect.Height-1 );


				Type tp = control.GetType();
				PropertyInfo pi = control.GetType().GetProperty( "PreferredHeight" );
				if ( pi != null )
				{
					int PreferredHeight = (int)pi.GetValue( control, null );

					if ( ( (PreferredHeight + this.CellPaddingSize*2)> this.ItemHeight ) && AutoHeight )
						this.ItemHeight = PreferredHeight + this.CellPaddingSize*2;

					subrc.Y = subControlRect.Y + ((subControlRect.Height - PreferredHeight)/2);
				}

				NewLiveControls.Add( control );						// put it in the new list, remove from old list
				if ( LiveControls.Contains( control ) )				// make sure its in the old list first
				{
					LiveControls.Remove( control );			// remove it from list so it doesn't get put down
				}


				if ( control.Bounds.ToString() != subrc.ToString() )
					control.Bounds = subrc;							// this will force an invalidation

				if ( control.Visible != true )
					control.Visible = true;
			}
			else	// not control based
			{
				// if the sub item color is not the same as the back color fo the control, AND the item is not selected, then color this sub item background
				
				if ( ( subItem.BackColor.ToArgb() != this.BackColor.ToArgb() ) && (!item.Selected) && ( subItem.BackColor != Color.White ) )
				{
					SolidBrush bbrush = new SolidBrush( subItem.BackColor );
					graphicsSubItem.FillRectangle( bbrush, rectSubItem );
					bbrush.Dispose();
				}

				// do we need checkboxes in this column or not?
				if ( this.Columns[ nColumn ].CheckBoxes )
					rectSubItem = DrawCheckBox( graphicsSubItem, rectSubItem, subItem.Checked );

				// if there is an image, this routine will RETURN with exactly the space left for everything else after the image is drawn (or not drawn due to lack of space)
				if ( (subItem.ImageIndex > -1) && (ImageList != null) && (subItem.ImageIndex < this.ImageList.Images.Count) )
					rectSubItem = DrawCellGraphic( graphicsSubItem, rectSubItem, this.ImageList.Images[ subItem.ImageIndex ], subItem.ImageAlignment );

				// deal with text color in a box on whether it is selected or not
				Color textColor;
				if ( item.Selected && this.Selectable )
					textColor = this.SelectedTextColor;
				else
				{
					textColor = this.ForeColor;
					if ( item.ForeColor.ToArgb() != this.ForeColor.ToArgb() )
						textColor = item.ForeColor;
					else if ( subItem.ForeColor.ToArgb() != this.ForeColor.ToArgb() )
						textColor = subItem.ForeColor;
				}

				DrawCellText( graphicsSubItem, rectSubItem, subItem.Text, Columns[nColumn].TextAlignment, textColor, item.Selected, ItemWordWrap );

				subItem.LastCellRect = rectSubItem;			// important to ONLY catch the area where the text is drawn
			}

		}


		/// <summary>
		/// Draw a checkbox on the sub item
		/// </summary>
		/// <param name="graphicsCell"></param>
		/// <param name="rectCell"></param>
		/// <param name="bChecked"></param>
		/// <returns></returns>
		virtual public Rectangle DrawCheckBox( Graphics graphicsCell, Rectangle rectCell, bool bChecked )
		{
			int th, ty, tw, tx;

			th = CHECKBOX_SIZE + (CellPaddingSize*2);
			tw = CHECKBOX_SIZE + (CellPaddingSize*2);
			MaxHeight = th;										// this will only set if autosize is true

			if ( ( tw > rectCell.Width ) || ( th > rectCell.Height ) )
				return rectCell;					// not enough room to draw the image, bail out


			ty = rectCell.Y + CellPaddingSize + ((rectCell.Height-th)/2);
			tx = rectCell.X + CellPaddingSize;

			if ( bChecked )
				graphicsCell.DrawImage( this.imageList1.Images[1], tx, ty );
				//graphicsCell.FillRectangle( Brushes.YellowGreen, tx, ty, CHECKBOX_SIZE, CHECKBOX_SIZE );
			else
				graphicsCell.DrawImage( this.imageList1.Images[0], tx, ty );
			//graphicsCell.FillRectangle( Brushes.Red, tx, ty, CHECKBOX_SIZE, CHECKBOX_SIZE );

			// remove the width that we used for the graphic from the cell
			rectCell.Width -= (CHECKBOX_SIZE + (CellPaddingSize*2));
			rectCell.X += tw;

			return rectCell;
		}


		/// <summary>
		/// draw the contents of a cell, do not draw any background or associated things
		/// </summary>
		/// <param name="graphicsCell"></param>
		/// <param name="rectCell"></param>
		/// <param name="img"></param>
		/// <param name="alignment"></param>
		/// <returns>
		/// returns the area of the cell that is left for you to put anything else on.
		/// </returns>
		virtual public Rectangle DrawCellGraphic( Graphics graphicsCell, Rectangle rectCell, Image img, HorizontalAlignment alignment )
		{
			int th, ty, tw, tx;

			th = img.Height + (CellPaddingSize*2);
			tw = img.Width + (CellPaddingSize*2);
			MaxHeight = th;										// this will only set if autosize is true

			if ( ( tw > rectCell.Width ) || ( th > rectCell.Height ) )
				return rectCell;					// not enough room to draw the image, bail out

			if ( alignment == HorizontalAlignment.Left )
			{
				ty = rectCell.Y + CellPaddingSize + ((rectCell.Height-th)/2);
				tx = rectCell.X + CellPaddingSize;

				graphicsCell.DrawImage( img, tx, ty );

				// remove the width that we used for the graphic from the cell
				rectCell.Width -= (img.Width + (CellPaddingSize*2));
				rectCell.X += tw;
			}
			else if ( alignment == HorizontalAlignment.Center )
			{
				ty = rectCell.Y + CellPaddingSize + ((rectCell.Height-th)/2);
				tx = rectCell.X + CellPaddingSize + ((rectCell.Width-tw)/2);;

				graphicsCell.DrawImage( img, tx, ty );

				// remove the width that we used for the graphic from the cell
				//rectCell.Width -= (img.Width + (CellPaddingSize*2));
				//rectCell.X += (img.Width + (CellPaddingSize*2));
				rectCell.Width = 0;
			}
			else if ( alignment == HorizontalAlignment.Right )
			{
				ty = rectCell.Y + CellPaddingSize + ((rectCell.Height-th)/2);
				tx = rectCell.Right - tw;

				graphicsCell.DrawImage( img, tx, ty );

				// remove the width that we used for the graphic from the cell
				rectCell.Width -= tw;
			}

			return rectCell;
		}


		/// <summary>
		/// Draw cell text is used by header and cell to draw properly aligned text in subitems.
		/// </summary>
		/// <param name="graphicsCell"></param>
		/// <param name="rectCell"></param>
		/// <param name="strCellText"></param>
		/// <param name="alignment"></param>
		/// <param name="textColor"></param>
		/// <param name="bSelected"></param>
		/// <param name="bWordWrap"></param>
		virtual public void DrawCellText( Graphics graphicsCell, Rectangle rectCell, string strCellText, ContentAlignment alignment, Color textColor, bool bSelected, bool bWordWrap  )
		{
			int nInteriorWidth = rectCell.Width - (CellPaddingSize*2);
			int nInteriorHeight = rectCell.Height - (CellPaddingSize*2);


			// cell text color will be inverted or changed from caller if this item is already selected
			SolidBrush textBrush;
			textBrush = new SolidBrush( textColor );


			// convert property editor friendly alignment to an alignment we can use for strings
			StringFormat sf = new StringFormat();
			sf.Alignment = GLStringHelpers.ConvertContentAlignmentToHorizontalStringAlignment( alignment );
			sf.LineAlignment = GLStringHelpers.ConvertContentAlignmentToVerticalStringAlignment( alignment );

			SizeF measuredSize;
			if ( bWordWrap )
			{
				sf.FormatFlags = 0;	// word wrapping is on by default for drawing
				measuredSize = graphicsCell.MeasureString( strCellText, Font, new Point( CellPaddingSize, CellPaddingSize ), sf );
			}
			else
			{	// they aren't word wrapping so we need to put the ...'s where necessary
				sf.FormatFlags = StringFormatFlags.NoWrap;
				measuredSize = graphicsCell.MeasureString( strCellText, Font, new Point( CellPaddingSize, CellPaddingSize ), sf );
				if ( measuredSize.Width > nInteriorWidth )		// dont truncate if we are doing word wrap
					strCellText = GLStringHelpers.TruncateString( strCellText, nInteriorWidth, graphicsCell, Font );
			}

			MaxHeight = (int)measuredSize.Height + (CellPaddingSize*2);													// this will only set if autosize is true
			graphicsCell.DrawString( strCellText, Font, textBrush, rectCell /*rectCell.X+this.CellPaddingSize, rectCell.Y+this.CellPaddingSize*/, sf );

			textBrush.Dispose();
		}


		/// <summary>
		/// Draw grid lines in client area
		/// </summary>
		/// <param name="RowsDC"></param>
		/// <param name="rect"></param>
		virtual public void DrawGridLines( Graphics RowsDC, Rectangle rect )
		{
			DW("DrawGridLines");

			int nStartItem = this.vPanelScrollBar.Value;			/* Draw Rows */
			int nYCursor = rect.Y;
			//for (int nItem = 0; ((nItem < (VisibleRowsCount +1) ) && ((nItem+nStartItem) < Items.Count )); nItem++ )

			Pen p = new Pen( this.GridColor );
			if ( this.GridLineStyle == GLGridLineStyles.gridDashed )
				p.DashStyle = DashStyle.Dash;
			else if ( this.GridLineStyle == GLGridLineStyles.gridSolid )
				p.DashStyle = DashStyle.Solid;
			else
				p.DashStyle = DashStyle.Solid;


			if ( ( this.GridLines == GLGridLines.gridBoth ) || ( this.GridLines == GLGridLines.gridHorizontal ) )
			{
				int nRowsToDraw = VisibleRowsCount + 1;
				if ( this.GridTypes == GLGridTypes.gridOnExists)
					if ( VisibleRowsCount > this.Count )
						nRowsToDraw = this.Count;


				for (int nItem = 0; nItem < nRowsToDraw; nItem++ )
				{	//Debug.WriteLine( "ItemCount " + Items.Count.ToString() + " Item Number " + nItem.ToString() );
					nYCursor += ItemHeight;
					RowsDC.DrawLine( p, 0, nYCursor, rect.Width, nYCursor );					// draw horizontal line
				}
			}

			if ( ( this.GridLines == GLGridLines.gridBoth ) || ( this.GridLines == GLGridLines.gridVertical ) )
			{
				int nXCursor = -this.hPanelScrollBar.Value;
				for ( int nColumn = 0; nColumn < Columns.Count; nColumn++ )
				{
					nXCursor += Columns[nColumn].Width;
					RowsDC.DrawLine( p, nXCursor+1, rect.Y, nXCursor+1, rect.Bottom );				// draw vertical line
				}
			}

			p.Dispose();
		}


		#endregion // drawing

		#region Keyboard

#if false
		protected override void OnKeyPress(KeyPressEventArgs e)
		{
			e.Handled = true;
		}
		protected override void OnKeyDown(KeyEventArgs e)
		{
			e.Handled = true;
		}
#endif

		#endregion

		#region Scrolling


		private void OnScroll(object sender, ScrollEventArgs e)
		{
			DI("Calling Invalidate From OnScroll");

			this.DestroyActivatedEmbedded();

			Invalidate();
		}


		/// <summary>
		/// Recalculate scroll bars and control size
		/// </summary>
		private void RecalcScroll( )//Graphics g )
		{
			DW("RecalcScroll");


			int nSomethingHasGoneVeryWrongSoBreakOut = 0;
			bool bSBChanged;
			do					// this loop is to handle changes and rechanges that happen when oen or the other changes
			{
				DW("Begin scrolbar updates loop");
				bSBChanged = false;

				if ( (Columns.Width > RowsInnerClientRect.Width) && (hPanelScrollBar.Visible == false) )
				{	// total width of all the rows is less than the visible rect
					hPanelScrollBar.mVisible = true;
					hPanelScrollBar.Value = 0;
					bSBChanged = true;

					DI("Calling Invalidate From RecalcScroll");
					Invalidate();

					DW("showing hscrollbar");
				}

				if ( (Columns.Width <= RowsInnerClientRect.Width) && (hPanelScrollBar.Visible == true) )
				{	// total width of all the rows is less than the visible rect
					hPanelScrollBar.mVisible = false;
					hPanelScrollBar.Value = 0;
					bSBChanged = true;
					DI("Calling Invalidate From RecalcScroll");
					Invalidate();

					DW("hiding hscrollbar");
				}

				if ( (TotalRowHeight > RowsInnerClientRect.Height) && (vPanelScrollBar.Visible == false) )
				{  // total height of all the rows is greater than the visible rect
					vPanelScrollBar.mVisible = true;
					hPanelScrollBar.Value = 0;
					bSBChanged = true;
					DI("Calling Invalidate From RecalcScroll");
					Invalidate();

					DW("showing vscrollbar");
				}

				if ( (TotalRowHeight <= RowsInnerClientRect.Height) && (vPanelScrollBar.Visible == true) )
				{	// total height of all rows is less than the visible rect
					vPanelScrollBar.mVisible = false;
					vPanelScrollBar.Value = 0;
					bSBChanged = true;
					DI("Calling Invalidate From RecalcScroll");
					Invalidate();

					DW("hiding vscrollbar");
				}

				DW("End scrolbar updates loop");

				// *** WARNING *** WARNING *** Kludge.  Not sure why this is sometimes hanging.  Fix this.
				if ( ++nSomethingHasGoneVeryWrongSoBreakOut > 4 )
					break;

			} while ( bSBChanged == true );		// this should never really run more than twice


			//Rectangle headerRect = HeaderRect;		// tihs is an optimization so header rect doesnt recalc every time we call it
			Rectangle rectClient = RowsInnerClientRect;

			/*
			 *  now that we know which scrollbars are showing and which aren't, resize the scrollbars to fit those windows
			 */
			if ( vPanelScrollBar.Visible == true )
			{
				vPanelScrollBar.mTop = rectClient.Y;
				vPanelScrollBar.mLeft = rectClient.Right;
				vPanelScrollBar.mHeight = rectClient.Height;
				vPanelScrollBar.mLargeChange = VisibleRowsCount;
				vPanelScrollBar.mMaximum = Count-1;

				if ( ((vPanelScrollBar.Value + VisibleRowsCount ) > Count) )		// catch all to make sure the scrollbar isnt going farther than visible items
				{
					DW("Changing vpanel value");
					vPanelScrollBar.Value = Count - VisibleRowsCount;				// an item got deleted underneath somehow and scroll value is larger than can be displayed
				}
			}

			if ( hPanelScrollBar.Visible == true )
			{
				hPanelScrollBar.mLeft = rectClient.Left;
				hPanelScrollBar.mTop = rectClient.Bottom;
				hPanelScrollBar.mWidth = rectClient.Width;

				hPanelScrollBar.mLargeChange = rectClient.Width;	// this reall is the size we want to move
				hPanelScrollBar.mMaximum = Columns.Width;

				if ( (hPanelScrollBar.Value + hPanelScrollBar.LargeChange) > hPanelScrollBar.Maximum )
				{
					DW("Changing vpanel value");
					hPanelScrollBar.Value = hPanelScrollBar.Maximum - hPanelScrollBar.LargeChange;
				}
			}


			if ( BorderPadding > 0 ) 
			{
				horiBottomBorderStrip.Bounds = new Rectangle( 0, this.ClientRectangle.Bottom-this.BorderPadding, this.ClientRectangle.Width, this.BorderPadding ) ;		// horizontal bottom picture box
				horiTopBorderStrip.Bounds = new Rectangle( 0, this.ClientRectangle.Top, this.ClientRectangle.Width, this.BorderPadding ) ;		// horizontal bottom picture box

				vertLeftBorderStrip.Bounds = new Rectangle( 0, 0, this.BorderPadding, this.ClientRectangle.Height ) ;		// horizontal bottom picture box
				vertRightBorderStrip.Bounds = new Rectangle( this.ClientRectangle.Right-this.BorderPadding, 0, this.BorderPadding, this.ClientRectangle.Height ) ;		// horizontal bottom picture box
			}
			else
			{
				if ( this.horiBottomBorderStrip.Visible )
					this.horiBottomBorderStrip.Visible = false;
				if ( this.horiTopBorderStrip.Visible )
					this.horiTopBorderStrip.Visible = false;
				if ( this.vertLeftBorderStrip.Visible )
					this.vertLeftBorderStrip.Visible = false;
				if ( this.vertRightBorderStrip.Visible )
					this.vertRightBorderStrip.Visible = false;
			}

			if ( hPanelScrollBar.Visible && vPanelScrollBar.Visible )
			{
				if ( !cornerBox.Visible )
					cornerBox.Visible = true;

				cornerBox.Bounds = new Rectangle( hPanelScrollBar.Right, vPanelScrollBar.Bottom, vPanelScrollBar.Width, hPanelScrollBar.Height );
			}
			else
			{
				if ( cornerBox.Visible )
					cornerBox.Visible = false;
			}


		}


		/// <summary>
		/// Handle vertical scroll bar movement
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void vPanelScrollBar_Scroll(object sender, System.Windows.Forms.ScrollEventArgs e)
		{
			DW("vPanelScrollBar_Scroll");
			//this.Focus();

			DI("Calling Invalidate From vPanelScrollBar_Scroll");
			Invalidate();
		}


		/// <summary>
		/// Handle horizontal scroll bar movement
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void hPanelScrollBar_Scroll(object sender, System.Windows.Forms.ScrollEventArgs e)
		{
			DW("hPanelScrollBar_Scroll");
			//this.Focus();

			DI("Calling Invalidate From hPanelScrollBar_Scroll");

			Invalidate();
		}


		#endregion

		#region Mouse


		/// <summary>
		/// OnDoubleclick
		/// 
		/// if someone double clicks on an area, we need to start a control potentially
		/// </summary>
		/// <param name="e"></param>
		protected override void OnDoubleClick(EventArgs e)
		{
			DW("GlacialList.OnDoubleClick");

			Point pointLocalMouse = this.PointToClient( Cursor.Position );

			//Debug.WriteLine( "Double Click Called" );
			//Debug.WriteLine( "At Cords X " + pointLocalMouse.X.ToString() + " Y " + pointLocalMouse .Y.ToString() );


			int nItem = 0, nColumn = 0, nCellX = 0, nCellY = 0;
			ListStates eState;
			GLListRegion listRegion;
			InterpretCoords( pointLocalMouse.X, pointLocalMouse.Y, out listRegion, out nCellX, out nCellY, out nItem, out nColumn, out eState );

			//Debug.WriteLine( "listRegion " + listRegion.ToString() );

			if ( ( listRegion == GLListRegion.client ) && ( nColumn < Columns.Count ) )
			{
				ActivateEmbeddedControl( nColumn, Items[nItem], Items[nItem].SubItems[nColumn] );
			}

			base.OnDoubleClick( e );
		}


		/// <summary>
		/// had to put this routine in because of overriden protection level being unchangable
		/// </summary>
		/// <param name="Sender"></param>
		/// <param name="e"></param>
		protected void OnMouseDownFromSubItem( object Sender, MouseEventArgs e )
		{
			DW("OnMouseDownFromSubItem");
			//Debug.WriteLine( "OnMouseDownFromSubItem called " + e.X.ToString() + " " + e.Y.ToString() );
			Point cp = this.PointToClient( new Point( Control.MousePosition.X, Control.MousePosition.Y ) );
			e = new MouseEventArgs( e.Button, e.Clicks, cp.X, cp.Y, e.Delta );
			//Debug.WriteLine( "after " + cp.X.ToString() + " " + cp.Y.ToString() );
			OnMouseDown( e );
		}


		/// <summary>
		/// Mouse has left the control area
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseLeave(EventArgs e)
		{
			// clear all the hot tracking
			this.Columns.ClearHotStates();	// this is the HEADER hot state
			this.HotItemIndex = -1;
			this.HotColumnIndex = -1;

			base.OnMouseLeave (e);
		}



		/// <summary>
		/// mouse button pressed
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			DW("GlacialList_MouseDown");


			//Debug.WriteLine( "Real " + e.X.ToString() + " " + e.Y.ToString() );

			int nItem = 0, nColumn = 0, nCellX = 0, nCellY = 0;
			ListStates eState;
			GLListRegion listRegion;
			InterpretCoords( e.X, e.Y, out listRegion, out nCellX, out nCellY, out nItem, out nColumn, out eState );

			//Debug.WriteLine( nCellX.ToString() + " - " + nCellY.ToString() );


			if ( e.Button == MouseButtons.Right )			// if its the right button then we don't really care till its released
			{
				base.OnMouseDown( e );
				return;
			}


			//-----------------------------------------------------------------------------------------
			if ( eState == ListStates.stateColumnSelect )										// Column select
			{
				m_nState = ListStates.stateNone;


				if ( SortType != SortTypes.None )
				{
					Columns[ nColumn ].State = ColumnStates.csPressed;
					this.SortColumn( nColumn );
				}

				if ( ColumnClickedEvent != null )
					ColumnClickedEvent( this, new ClickEventArgs( nItem, nColumn ) );				// fire the column clicked event

				//Invalidate();
				base.OnMouseDown( e );
				return;
			}
			//---Resizing -----------------------------------------------------------------------------------
			if ( eState == ListStates.stateColumnResizing )										// resizing
			{
				Cursor.Current = Cursors.VSplit;
				m_nState = ListStates.stateColumnResizing;

				m_pointColumnResizeAnchor = new Point( GetColumnScreenX(nColumn), e.Y );		// deal with moving column sizes
				m_nResizeColumnNumber = nColumn;


				base.OnMouseDown( e );
				return;
			}
			//--Item check, if no items exist go no further--
			//if ( Items.Count == 0 )
			//return;

			//---Items --------------------------------------------------------------------------------------
			if ( eState == ListStates.stateSelecting )
			{	// ctrl based multi select ------------------------------------------------------------

				// whatever else this does, it needs to first check to see if the state of the checkbox is changing
				if ( ( nColumn < Columns.Count ) && ( this.Columns[ nColumn ].CheckBoxes ) )
				{	// there is a checkbox on this control, lets see if the click came in the region
					if ( 
						( nCellX > this.CellPaddingSize ) && 
						( nCellX < (this.CellPaddingSize + CHECKBOX_SIZE ) ) &&
						( nCellY > this.CellPaddingSize ) && 
						( nCellY < (this.CellPaddingSize + CHECKBOX_SIZE ) )
						)
					{	// toggle the checkbox
						if ( Items[nItem].SubItems[nColumn].Checked )
							Items[nItem].SubItems[nColumn].Checked = false;
						else
							Items[nItem].SubItems[nColumn].Checked = true;
					}

				}


				m_nState = ListStates.stateSelecting;

				this.FocusedItem = Items[nItem];



				if ( (( ModifierKeys & Keys.Control) == Keys.Control ) && ( AllowMultiselect == true ) )
				{
					m_nLastSelectionIndex = nItem;

					if ( Items[nItem].Selected == true )
						Items[nItem].Selected = false;
					else
						Items[nItem].Selected = true;


					base.OnMouseDown( e );
					return;
				}

				// shift based multi row select -------------------------------------------------------
				if ( (( ModifierKeys & Keys.Shift) == Keys.Shift ) && ( AllowMultiselect == true ) )
				{
					Items.ClearSelection();
					if ( m_nLastSelectionIndex >= 0 )			// ie, non negative so that we have a starting point
					{
						int index = m_nLastSelectionIndex;
						do
						{
							Items[index].Selected = true;
							if ( index > nItem )		index--;
							if ( index < nItem )		index++;
						} while ( index != nItem );

						Items[index].Selected = true;
					}

					base.OnMouseDown( e );
					return;
				}

				// the normal single select -----------------------------------------------------------
				Items.ClearSelection( Items[nItem] );

				// following two if statements deal ONLY with non multi=select where a singel sub item is being selected
				if ( ( m_nLastSelectionIndex < Count ) && ( m_nLastSubSelectionIndex < Columns.Count ) )
					Items[m_nLastSelectionIndex].SubItems[m_nLastSubSelectionIndex].Selected = false;
				if ( ( FullRowSelect == false ) && ( ( nItem < Count ) && ( nColumn < Columns.Count ) ) )
					Items[nItem].SubItems[nColumn].Selected = true;


				m_nLastSelectionIndex = nItem;
				m_nLastSubSelectionIndex = nColumn;
				Items[nItem].Selected = true;
			}


			base.OnMouseDown( e );
			return;
		}


		/// <summary>
		/// when mouse moves
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			DW("GlacialList_MouseMove");


			try
			{
				if ( m_nState == ListStates.stateColumnResizing )
				{
					Cursor.Current = Cursors.VSplit;


					int nWidth;
					nWidth = e.X - m_pointColumnResizeAnchor.X;

					if ( nWidth <= MINIMUM_COLUMN_SIZE )
					{
						nWidth = MINIMUM_COLUMN_SIZE;
					}

					GLColumn col;
					col = (GLColumn)Columns[m_nResizeColumnNumber];
					col.Width = nWidth;

					base.OnMove( e );
					return;
				}


				int nItem = 0, nColumn = 0, nCellX = 0, nCellY = 0;
				ListStates eState;
				GLListRegion listRegion;
				InterpretCoords( e.X, e.Y, out listRegion, out nCellX, out nCellY, out nItem, out nColumn, out eState );


				if ( eState == ListStates.stateColumnResizing )
				{
					Cursor.Current = Cursors.VSplit;

					base.OnMove( e );
					return;
				}

				Cursor.Current = Cursors.Arrow;

			}
			catch( Exception ex )
			{
				Debug.WriteLine("Exception throw in GlobalList_MouseMove with text : " + ex.ToString() );

			}

			base.OnMove( e );
			return;
		}


		/// <summary>
		/// mouse up
		/// </summary>
		/// <param name="e"></param>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			DW("MouseUp");

			Cursor.Current = Cursors.Arrow;
			Columns.ClearStates();


			int nItem = 0, nColumn = 0, nCellX = 0, nCellY = 0;
			ListStates eState;
			GLListRegion listRegion;
			InterpretCoords( e.X, e.Y, out listRegion, out nCellX, out nCellY, out nItem, out nColumn, out eState );


			m_nState = ListStates.stateNone;

			base.OnMouseUp( e );
		}


		#endregion

		#endregion  // functionality
	}

}


