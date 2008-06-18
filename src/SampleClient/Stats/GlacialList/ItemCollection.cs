/***************************************************
 * GlacialList v1.30
 * 
 * Written By Allen Anderson
 * http://www.glacialcomponents.com
 * 
 * February 10th, 2004
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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Text;




namespace GlacialComponents.Controls
{
	/// <summary>
	/// SubItems which make up a GLItem
	/// </summary>
	[
	DesignTimeVisible(true),
	TypeConverter("GlacialComponents.Controls.GLSubItemConverter")
	]
	public class GLSubItem
	{
		#region Events and Delegates


		/// <summary>
		/// Sub Item has changed.
		/// </summary>
		public event ChangedEventHandler ChangedEvent;

		#endregion

		#region Properties and Variables

		private string					m_Text = "";
		private Color					m_ForeColor =	Color.Black;


		//sf.LineAlignment = System.Drawing.StringAlignment.Near;
		//sf.FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.MeasureTrailingSpaces;

		private int						m_nImageIndex = -1;
		private HorizontalAlignment	m_ImageAlignment = HorizontalAlignment.Left;

		private Color					m_BackColor;			// = Color.White;
		//private int					m_nSpan = 1;								// always span 1 unless changed
		private bool					m_bSelected = false;
		private object					m_Tag = null;
		private bool					m_bForceText = false;						// for this specific item, show it as normal instead of control based.
		private Control				m_Control = null;
		private GlacialList			m_Parent = null;							// so we can always access the columns
		private bool					m_bChecked = false;							// if the sub item has a check box, then this holds the value
		private Hashtable			m_EmbeddedControProperties = null;			// only create hashtable if someone starts to use it (saves memory)
		private Rectangle			m_LastCellRect = new Rectangle( 0,0,0,0 );	// last rectangle that text was drawn into



		/// <summary>
		/// last rectangle that text was drawn into
		/// </summary>
		[
		Browsable( false ),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden )
		]
		public Rectangle LastCellRect
		{
			get	{ return m_LastCellRect; }
			set { m_LastCellRect = value; }
		}


		/// <summary>
		/// is the checkbox checked or not
		/// </summary>
		[
		Browsable( true ),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Description( "Item Check State" )
		]
		public bool Checked
		{
			get	{ return m_bChecked; }
			set
			{
				if ( m_bChecked != value )
				{
					m_bChecked = value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );
				}
			}
		}


		/// <summary>
		/// pointer to the primary Parent on top
		/// </summary>
		[
		Browsable(false),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden )
		]
		public GlacialList Parent
		{
			get	{ return m_Parent; }
			set	{ m_Parent = value;	}
		}


		/// <summary>
		/// Properties of the embedded controls in the listview
		/// 
		/// this is brilliant because it also allows people to set properties of controls that I don't know about
		/// 
		/// the reason I'm even doing this is so many standard control types don't have to be shown
		/// </summary>
		/// 
		[
		Browsable(false),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden )
		]
		public Hashtable EmbeddedControProperties
		{
			get 
			{
				if ( m_EmbeddedControProperties == null )
					m_EmbeddedControProperties = new Hashtable();

				return m_EmbeddedControProperties; 
			}
			//			set 
			//			{ 
			//				m_EmbeddedControProperties = value; 
			//			}
		}


		/// <summary>
		/// Force the sub item display to default to text only
		/// 
		/// This will override everything.
		/// </summary>
		[
		Description("We can choose to NOT display the control override coming from the column."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable( true ),
		Category("Appearance")
		]
		public bool ForceText
		{
			get	{ return m_bForceText; }
			set
			{
				if ( m_bForceText != value )
				{
					m_bForceText = value;
					ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, null ) );
				}
			}
		}


		/// <summary>
		/// Embedded Control
		/// </summary>
		[
		Description("Embed control."),
		Browsable( false ),
		Category("Control"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden )
		]
		public Control Control
		{
			get	{ return m_Control; }
			set	
			{
				if ( m_Control != value )
				{
					m_Control = value;

					// do all other default control setups here
					m_Control.Visible = false;
					//m_Control.Parent = this.Parent;
					//m_Control.MouseDown += new MouseEventHandler( this.ListView.OnMouseDownFromSubItem );

					//if ( ChangedEvent != null )
					//ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );
				}
			}
		}


#if false
		subitems[e.IndexChanged].MouseDown += new MouseEventHandler(OnSubItemMouseDown);

		private void OnSubItemMouseDown(object sender, MouseEventArgs e)
		{
			if (MouseDown != null)
				MouseDown(this, e);
		}
#endif

		/// <summary>
		/// Index of image
		/// </summary>
		[
		Description("Index of image to display from imagelist.  This assumes that an imagelist exists.  If it does not, this will do nothing."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable( true ),
		Category("ImageIndex")
		]
		public int ImageIndex
		{
			get	{ return m_nImageIndex; }
			set	
			{
				if ( m_nImageIndex != value )
				{
					m_nImageIndex = value;

					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );
				}
			}
		}


		/// <summary>
		/// Alignment of the image within the subitem
		/// </summary>
		[
		Description("Image info for the sub item."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable( true ),
		Category("Image")
		]
		public HorizontalAlignment ImageAlignment
		{
			get	{ return m_ImageAlignment; }
			set
			{
				if ( m_ImageAlignment != (HorizontalAlignment)value )
				{
					m_ImageAlignment = (HorizontalAlignment)value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );				// fire the column clicked event
				}
			}
		}


		/// <summary>
		/// Extra user information
		/// </summary>
		[
		Description("Extra user information"),
		Browsable( false ),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Category("Data")
		]
		public object Tag
		{
			get { return m_Tag; }
			set { m_Tag = value; }
		}


		/// <summary>
		/// Text
		/// </summary>
		[
		Description("Text"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable( true ),
		Category("Data")
		]
		public string Text
		{
			get 
			{
				// need to put a check here if there is an activated embedded control that is live

				if ( ( this.Parent != null ) && ( this.Parent.ActivatedEmbeddedControl != null ) )			// see if we can even see the parent
				{
					GLEmbeddedControl ctrl = (GLEmbeddedControl)this.Parent.ActivatedEmbeddedControl;
					if ( ( ctrl != null ) && ( ctrl.SubItem == this ) )		// just in case this isn't really an activated embedded control
					{
						Debug.WriteLine( ctrl.GLReturnText() );

						return ctrl.GLReturnText();
					}
				}

				return m_Text; 
			}
			set
			{
				if ( m_Text != value )
				{
					m_Text = value;

					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );
				}
			}
		}


		/// <summary>
		/// Color of text in item
		/// </summary>
		[
		Description("Color of the text"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable( true ),
		Category("Appearance")
		]
		public Color ForeColor
		{
			get	{ return m_ForeColor; }
			set
			{
				if ( m_ForeColor != value )
				{
					m_ForeColor = (Color)value;

					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );
				}
			}
		}


		/// <summary>
		/// Background color
		/// </summary>
		[
		Description("Color of background"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable( true ),
		Category("Appearance")
		]
		public Color BackColor
		{
			get	{ return m_BackColor; }
			set
			{
				if ( m_BackColor != value )
				{
					m_BackColor = (Color)value;

					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, null, null, this ) );
				}
			}
		}


		//		[
		//		Description("Columns spanned."),
		//		Browsable( true ),
		//		Category("Appearance")
		//		]
		//		public int Span
		//		{
		//			get	{ return m_nSpan; }
		//			set	
		//			{
		//				if ( m_nSpan != value )
		//				{
		//					m_nSpan = value;
		//
		//					if ( ChangedEvent != null )
		//						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemChanged, "" ) );
		//				}
		//			}
		//		}

		
		/// <summary>
		/// Indicates when the item is selected
		/// </summary>
		[
		Description("Sub item selection state."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable( false ),
		Category("Appearance")
		]
		public bool Selected		// sub item
		{
			get	{ return m_bSelected; }
			set
			{
				if ( m_bSelected != value )
				{
					// this could be more complicated actually


					m_bSelected = value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ItemChanged, null, null, this ) );
				}
			}
		}


		#endregion

		#region Functionality

#if false
		public void UnsetControlSettings( Control control )
		{


		}

		public void SetControlSettings( Control control )
		{
			// use reflection heavily ?
			IDictionaryEnumerator ide = EmbeddedControProperties.GetEnumerator();

			Type ctype = control.GetType();
			PropertyInfo cpropinfo = ctype.GetProperty( ide.Key.ToString() );
			object test = cpropinfo.GetValue( ide.Key.ToString(), null );
			Debug.WriteLine( test.ToString() );

			//control.ref. ide.Key
		}
#endif


		#endregion
	}


	/// <summary>
	/// Sub Item collection
	/// </summary>
	public class GLSubItemCollection : CollectionBase
	{
		#region Initialization

		/// <summary>
		/// Constructor
		/// </summary>
		public GLSubItemCollection()
		{
		}

		/// <summary>
		/// Constructor that accepts parent pointer
		/// </summary>
		/// <param name="parent"></param>
		public GLSubItemCollection( GlacialList parent )
		{
			m_Parent = parent;
		}

		#endregion

		#region Events and Delegates

		//public delegate void ChangedEventHandler( object source, ChangedEventArgs e );				//int nItem, int nSubItem );

		/// <summary>
		/// Sub Item Collection has changed
		/// </summary>
		public event ChangedEventHandler ChangedEvent;

		/// <summary>
		/// Sub Item changed handler.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		public void SubItem_Changed( object source, ChangedEventArgs e )
		{	// this gets called when an item internally changes
			GlacialList.DW("GLSubItemCollection::SubItem_Changed");

			if ( ChangedEvent != null )
				ChangedEvent( source, e );				// fire the column clicked event
		}

		/// <summary>
		/// Clear called against subitems in sub item collection
		/// </summary>
		protected override void OnClear()
		{
			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ItemCollectionChanged, null, null, null ) );				// fire the column clicked event
		}
		
		#endregion

		#region Properties and Variables

		private GlacialList				m_Parent;


		/// <summary>
		/// Internal pointer to parent class
		/// </summary>
		[
		Browsable(false),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden )
		]
		public GlacialList Parent
		{
			get	
			{ 
				return m_Parent; 
			}
			set
			{ 
				m_Parent = value;	

				foreach ( GLSubItem subItem in List )
					subItem.Parent = value;
			}
		}


		/// <summary>
		/// Indexer
		/// </summary>
		public GLSubItem this[ int nItemIndex ]
		{
			get
			{
				//				int nTmpCount = 0;
				//				if ( Parent == null )		// rare case when there is no parent set (almost always during collection editor)
				//					nTmpCount = 255;		// basically infinite during collection editor
				//				else
				//					nTmpCount = Parent.Columns.Count;

				int nBailout = 0;

				// check to make sure it exists first
				while ( List.Count <= nItemIndex )
				{
					GLSubItem newitem = new GLSubItem();
					newitem.ChangedEvent += new ChangedEventHandler( SubItem_Changed );
					newitem.Parent = this.m_Parent;
					//newitem.Control = Parent.Columns[ nItemIndex ]

					List.Add( newitem );							// if the index doesn't yet exist, fill in the subitems till it does

					if ( nBailout++ > 25 )
						break;
				}

				return (GLSubItem)List[nItemIndex];
			}
		}


		#endregion

		#region CollectionEditor Support Routines


		/// <summary>
		/// Add range of sub items
		/// </summary>
		/// <param name="subItems"></param>
		public void AddRange( GLSubItem[] subItems)
		{
			lock(List.SyncRoot)
			{
				for (int i=0; i<subItems.Length; i++)
					Add( subItems[i] );
			}
		}


		/// <summary>
		/// add an item to the end of the list
		/// </summary>
		/// <param name="strItemText"></param>
		/// <returns></returns>
		public GLSubItem Add( string strItemText )
		{
			return Insert( -1, strItemText );
		}


		/// <summary>
		/// add an itemto the items collection
		/// </summary>
		/// <param name="subItem"></param>
		/// <returns></returns>
		public int Add( GLSubItem subItem )
		{
			return Insert( -1, subItem );
		}


		/// <summary>
		/// insert an item into the list at specified index
		/// </summary>
		/// <param name="nIndex"></param>
		/// <param name="strItemText"></param>
		/// <returns></returns>
		public GLSubItem Insert( int nIndex, string strItemText )
		{
			GLSubItem subItem = new GLSubItem();			//GLItem item = new GLItem(Parent);
			//item.SubItems[0].Text = strItemText;

			nIndex = Insert( nIndex, subItem );

			return subItem;
		}


		/// <summary>
		/// lowest level of add/insert.  All add and insert routines eventually call this
		/// 
		/// in the future always have routines call this one as well to keep one point of entry
		/// </summary>
		/// <param name="nIndex"></param>
		/// <param name="subItem"></param>
		/// <returns></returns>
		public int Insert( int nIndex, GLSubItem subItem )
		{
			subItem.Parent = this.m_Parent;

			subItem.ChangedEvent += new ChangedEventHandler( SubItem_Changed );

			if ( nIndex < 0 )
				nIndex = List.Add( subItem );			// add the subItem itself
			else
				List.Insert( nIndex, subItem );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemCollectionChanged, null, null, subItem ) );

			return nIndex;
		}


		/// <summary>
		/// remove an item from the list
		/// </summary>
		/// <param name="nSubItemIndex"></param>
		public void Remove( int nSubItemIndex )
		{
			if ( ( nSubItemIndex >= this.Count ) || (nSubItemIndex < 0) )
				return;			// error

			List.RemoveAt( nSubItemIndex );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemCollectionChanged, null, null, null ) );
		}


		/// <summary>
		/// remove an item from the list
		/// </summary>
		/// <param name="subItem"></param>
		public void Remove( GLSubItem subItem )
		{
			List.Remove( subItem );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SubItemCollectionChanged, null, null, null ) );
		}


		/// <summary>
		/// Clears all selection bits in the item structure
		/// </summary>
		public void ClearSelection()
		{
			for ( int index = 0; index<List.Count; index++ )
				this[index].Selected = false;												// changed will generate an invalidation by themselves
		}


		#endregion
	}


	/// <summary>
	/// GLItem which corresponds to rows in the list view
	/// </summary>
	[
	DesignTimeVisible(true),
	TypeConverter("GlacialComponents.Controls.GLItemConverter")
	]
	public class GLItem								// : IComparable
	{
		#region Events and Delegates

		//public delegate void ChangedEventHandler( object source, ChangedEventArgs e );				//int nItem, int nSubItem );

		/// <summary>
		/// GLItem changed event
		/// </summary>
		public event ChangedEventHandler ChangedEvent;

		/// <summary>
		/// Sub Item collection changed handler
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		public void SubItemCollection_Changed( object source, ChangedEventArgs e )
		{	// this gets called when an item internally changes
			GlacialList.DW("GLItem::SubItemCollection_Changed");

			if ( ChangedEvent != null )
			{
				e.Item = this;							// add which item this came through
				ChangedEvent( this, e );				// fire the column clicked event
			}
		}

		#endregion

		#region Initialization

		/// <summary>
		/// Constructor
		/// </summary>
		public GLItem()
		{
			if ( this.Parent != null )
				m_SubItems = new GLSubItemCollection( this.Parent );
			else
				m_SubItems = new GLSubItemCollection();

			SubItems.ChangedEvent += new ChangedEventHandler( SubItemCollection_Changed );		// this will only happen when a new item is created
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="parent"></param>
		public GLItem( GlacialList parent )
		{
			m_SubItems = new GLSubItemCollection( parent );
			m_SubItems.Parent = parent;

			Parent = parent;		// this has to be after the sub item set becuase it tries to use the subitems

			SubItems.ChangedEvent += new ChangedEventHandler( SubItemCollection_Changed );		// this will only happen when a new item is created
		}


		#endregion

		#region Properties and Variables

		private GLSubItemCollection				m_SubItems;
		private bool										m_bSelected = false;
		private object										m_Tag = null;
		private Color										m_ForeColor = Color.Black;
		private GlacialList								m_Parent = null;
		private Color										m_RowBorderColor = Color.Black;
		private Color										m_BackColor = Color.White;
		private int											m_nRowBorderSize = 0;



		/// <summary>
		/// row border size
		/// </summary>
		/// 
		[
		Description("Size of a border on each row."),
		Category("Behavior"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable(true)
		]
		public int RowBorderSize
		{
			get { return m_nRowBorderSize; }
			set { m_nRowBorderSize = value; }
		}


		/// <summary>
		/// Text color for item
		/// </summary>
		[
		Category("Behavior"),
		Description("Sub Items"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Content),
		//Editor(typeof(SubItemCollectionEditor), typeof(UITypeEditor)),
		Editor(typeof(CollectionEditor), typeof(UITypeEditor)),
		Browsable(true)
		]
		public GLSubItemCollection	SubItems
		{
			get 
			{
				return m_SubItems;
			}
		}


		/// <summary>
		/// Row border color
		/// </summary>
		/// 
		[
		Description("Set the back color for an entire row." ),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable(true)
		]
		public Color BackColor
		{
			get { return m_BackColor; }
			set { m_BackColor = value; }
		}


		/// <summary>
		/// Row border color
		/// </summary>
		/// 
		[
		Description("If you have row border size set to something other than 0 then it will take on this color."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable(true)
		]
		public Color RowBorderColor
		{
			get { return m_RowBorderColor; }
			set { m_RowBorderColor = value; }
		}


		/// <summary>
		/// Text for cell 0 (added by popular request)
		/// </summary>
		public string Text
		{
			get { return SubItems[0].Text; }
			set { SubItems[0].Text = value; }
		}


		/// <summary>
		/// User defineable object
		/// </summary>
		[
		Description("Extra user information."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public object Tag
		{
			get { return m_Tag; }
			set { m_Tag = value; }
		}


		/// <summary>
		/// Text color for item
		/// </summary>
		[
		Description("Text Color override for item."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable(true)
		]
		public Color ForeColor
		{
			get { return m_ForeColor; }
			set { m_ForeColor = value; }
		}


		/// <summary>
		/// pointer to parent
		/// </summary>
		[
		Description("Parent"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public GlacialList Parent
		{
			get	
			{ 
				return this.m_Parent; 
			}
			set	
			{ 
				this.m_Parent = value;

				SubItems.Parent = value; 
			}
		}


		/// <summary>
		/// Selected
		/// </summary>
		[
		Description(""),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		DefaultValue(false),
		Browsable(false)
		]
		public bool Selected
		{
			get { return m_bSelected; }
			set
			{
				if ( m_bSelected != value )
				{
					if ( m_Parent != null && !m_Parent.AllowMultiselect && !m_Parent.Items.SuspendEvents )
					{
						m_Parent.Items.SuspendEvents = true;
						m_Parent.Items.ClearSelection();
						m_Parent.Items.SuspendEvents = false;
					}

					m_bSelected = value;
					ChangedEvent( this, new ChangedEventArgs( ChangedTypes.SelectionChanged, null, this, null ) );
				}
			}
		}


		#endregion
	}


	/// <summary>
	/// Collection of GLItems
	/// </summary>
	public class GLItemCollection : CollectionBase
	{
		#region Events and Delegates


		/// <summary>
		/// Fires when a change occurs to the data
		/// </summary>
		public event ChangedEventHandler ChangedEvent;


		/// <summary>
		/// item has changed
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		public void Item_Changed( object source, ChangedEventArgs e )
		{	// this gets called when an item internally changes
			GlacialList.DW("GLItemCollection::Item_Changed");

			if ( ChangedEvent != null  && !SuspendEvents )
				ChangedEvent( source, e );				// fire the column clicked event
		}


		/// <summary>
		/// Items have been cleared event
		/// </summary>
		protected override void OnClear()
		{
			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ItemCollectionChanged, null, null, null ) );
		}
		

		#endregion

		#region Initialization

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="newParent"></param>
		public GLItemCollection( GlacialList newParent )
		{
			Parent = newParent;
		}


		#endregion

		#region Properties And Variables

		private GlacialList					m_Parent;
		private bool						m_bSuspendEvents = false;



		/// <summary>
		/// this is used for operations where you are changing multiple items consecutively and don't want to send 
		/// a larger number of change events than necessary.
		/// </summary>
		[
		Description("Extra user information."),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public bool SuspendEvents
		{
			set { m_bSuspendEvents = value; }
			get { return m_bSuspendEvents; }
		}


		/// <summary>
		/// Sets the parent variable so we know what to refresh when there is a change
		/// </summary>
		/// 
		[
		Description("Parent"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public GlacialList Parent
		{
			get 
			{ 
				return m_Parent; 
			}
			set 
			{ 
				m_Parent = value;

				// iterate through the children and send them all the parent
				foreach ( GLItem item in this.List )
					item.Parent = m_Parent;						// hopefully this will propogate
			}
		}


		/// <summary>
		/// Indexer that allows the use of Items by []
		/// </summary>
		/// 
		[
		Description("Item Collection"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Visible ),
		Browsable(true)
		]
		public GLItem this[ int nItemIndex ]
		{
			get	
			{	
				return (GLItem)List[nItemIndex];	
			}
			set
			{
				List[nItemIndex] = value;
			}
		}


		/// <summary>
		/// returns a list of only the selected items
		/// </summary>
		[
		Description("Selected Items Array"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public ArrayList SelectedItems
		{
			get
			{
				ArrayList listSelectedItems = new ArrayList();

				// go through list and add only selected items
				for ( int nIndex = 0; nIndex < Count; nIndex++ )
					if ( this[nIndex].Selected == true )
						listSelectedItems.Add( this[nIndex] );

				return listSelectedItems;
			}
		}


		/// <summary>
		/// returns a list of only the selected items indexes
		/// </summary>
		[
		Description("Selected Items Array Of Indicies"),
		DesignerSerializationVisibility( DesignerSerializationVisibility.Hidden ),
		Browsable(false)
		]
		public ArrayList SelectedIndicies
		{
			get
			{
				ArrayList listSelectedItems = new ArrayList();

				// go through list and add only selected items
				for ( int nIndex = 0; nIndex < Count; nIndex++ )
					if ( this[nIndex].Selected == true )
						listSelectedItems.Add( nIndex );

				return listSelectedItems;
			}
		}


		#endregion

		#region Methods

#if false
		/// <summary>
		/// Use the built in sorting mechanism to sort the list
		/// </summary>
//		public void Sort()
//		{
//			this.InnerList.Sort();
//		}
#endif

		/// <summary>
		/// Compatability with collection editor
		/// </summary>
		/// <param name="items"></param>
		public void AddRange( GLItem[] items)
		{
			lock(List.SyncRoot)
			{
				for (int i=0; i<items.Length; i++)
					Add( items[i] );
			}
		}

		/// <summary>
		/// add an item to the end of the list
		/// </summary>
		/// <param name="strItemText"></param>
		/// <returns></returns>
		public GLItem Add( string strItemText )
		{
			return Insert( -1, strItemText );
		}


		/// <summary>
		/// add an itemto the items collection
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public int Add( GLItem item )
		{
			return Insert( -1, item );
		}


		/// <summary>
		/// insert an item into the list at specified index
		/// </summary>
		/// <param name="nIndex"></param>
		/// <param name="strItemText"></param>
		/// <returns></returns>
		public GLItem Insert( int nIndex, string strItemText )
		{
			GLItem item = new GLItem( Parent );			//GLItem item = new GLItem(Parent);
			item.SubItems[0].Text = strItemText;

			nIndex = Insert( nIndex, item );

			return item;
		}

		/// <summary>
		/// lowest level of add/insert.  All add and insert routines eventually call this
		/// 
		/// in the future always have routines call this one as well to keep one point of entry
		/// </summary>
		/// <param name="nIndex"></param>
		/// <param name="item"></param>
		/// <returns></returns>
		public int Insert( int nIndex, GLItem item )
		{
			item.Parent = this.Parent;

			item.ChangedEvent += new ChangedEventHandler( Item_Changed );

			if ( nIndex < 0 )
				nIndex = List.Add( item );			// add the item itself
			else
				List.Insert( nIndex, item );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ItemCollectionChanged, null, null, null ) );

			return nIndex;
		}

		/// <summary>
		/// remove an item from the list
		/// </summary>
		/// <param name="nItemIndex"></param>
		public void Remove( int nItemIndex )
		{
			if ( ( nItemIndex >= this.Count ) || (nItemIndex < 0) )
				return;			// error

			List.RemoveAt( nItemIndex );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ItemCollectionChanged, null, null, null ) );
		}


		/// <summary>
		/// remove an item from the list
		/// </summary>
		/// <param name="item"></param>
		public void Remove( GLItem item )
		{
			List.Remove( item );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ItemCollectionChanged, null, null, null ) );
		}


		/// <summary>
		/// Clears all selection bits in the item structure
		/// </summary>
		public void ClearSelection()
		{
			for ( int index = 0; index<List.Count; index++ )
				this[index].Selected = false;												// changed will generate an invalidation by themselves
		}

		/// <summary>
		/// Clears all selection bits in the item structure
		/// 
		/// this overload is an optimization to stop a redraw on a re-selection
		/// </summary>
		public void ClearSelection( GLItem itemIgnore )
		{
			for ( int index = 0; index<List.Count; index++ )
			{
				GLItem citem = this[index];
				if ( citem != itemIgnore )
					citem.Selected = false;												// changed will generate an invalidation by themselves
			}
		}

		/// <summary>
		/// Find the next item index
		/// 
		/// set startindex to -1 to start at the beginning
		/// </summary>
		/// <param name="nStartIndex"></param>
		/// <param name="nColumn"></param>
		/// <param name="strItemText"></param>
		/// <returns></returns>
		public int FindNextItemIndex( int nStartIndex, int nColumn, string strItemText )
		{
			if ( ( nStartIndex < 0 ) || ( nStartIndex > Count ) )
				nStartIndex = 0;

			for ( int nIndex = nStartIndex; nIndex < Count; nIndex++ )
			{
				if ( strItemText == this[nIndex].SubItems[nColumn].Text )
					return nIndex;
			}

			return -1;			// couldn't find it
		}


		/// <summary>
		/// Get the next selected item index.
		/// 
		/// set startindex to -1 to start at the beginning
		/// </summary>
		/// <param name="nStartIndex"></param>
		/// <returns></returns>
		public int GetNextSelectedItemIndex( int nStartIndex )		// use -1 to have it start from the beginning of the list
		{
			if ( ( nStartIndex < 0 ) || ( nStartIndex > Count ) )
				nStartIndex = -1;

			for ( int nIndex = nStartIndex+1; nIndex < Count; nIndex++ )
			{
				if ( this[nIndex].Selected == true )
					return nIndex;
			}

			return -1;			// couldn't find it
		}


		/// <summary>
		/// Find the index of a specified item
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public int FindItemIndex( GLItem item )		// use -1 to have it start from the beginning of the list
		{
			for ( int nIndex = 0; nIndex < Count; nIndex++ )
				if ( item == this[nIndex] )
					return nIndex;

			return -1;			// couldn't find it
		}


		#endregion
	}


	#region Item Converter

	/// <summary>
	/// GLItemConverter
	/// </summary>
	/// 
	public class GLItemConverter : TypeConverter
	{
		/// <summary>
		/// Conversion from Item to string
		/// </summary>
		/// <param name="context"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(InstanceDescriptor))
			{
				return true;
			}
			return base.CanConvertTo(context, destinationType);
		}

		/// <summary>
		/// Conversion to string
		/// </summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(InstanceDescriptor) && value is GLItem)
			{
				GLItem column = (GLItem)value;
              
				ConstructorInfo ci = typeof(GLItem).GetConstructor(new Type[] {});
				if (ci != null)
				{
					return new InstanceDescriptor(ci, null, false);
				}
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}

	/// <summary>
	/// GLSubItemConverter
	/// </summary>
	public class GLSubItemConverter : TypeConverter
	{
		/// <summary>
		/// Conversion from Item to string
		/// </summary>
		/// <param name="context"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(InstanceDescriptor))
			{
				return true;
			}
			return base.CanConvertTo(context, destinationType);
		}

		/// <summary>
		/// Conversion to string
		/// </summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(InstanceDescriptor) && value is GLSubItem)
			{
				//GLItem column = (GLItem)value;
              
				ConstructorInfo ci = typeof(GLSubItem).GetConstructor(new Type[] {});
				if (ci != null)
				{
					return new InstanceDescriptor(ci, null, false);
				}
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}

	#endregion

}
