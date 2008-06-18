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
using System.Windows.Forms.Design;



namespace GlacialComponents.Controls
{
	/// <summary>
	/// Column State enumerations
	/// </summary>
	public enum ColumnStates 
	{ 
		/// <summary>
		/// Column is in normal state
		/// </summary>
		csNone, 
		/// <summary>
		/// Column is showing pressed state
		/// </summary>
		csPressed, 
		/// <summary>
		/// Mouse cursor is over column header, but not pressed
		/// </summary>
		csHot 
	}



	/// <summary>
	/// Summary description for Column.
	/// </summary>
	/// 
	[
	DesignTimeVisible(true),
	TypeConverter("GlacialComponents.Controls.GLColumnConverter")
	]
	public class GLColumn
	{
		#region Construction

		/// <summary>
		/// Default constructor for use with the collection editor (only)
		/// </summary>
		public GLColumn()
		{
		}

		/// <summary>
		/// Constructor for GLColumn
		/// </summary>
		/// <param name="name"></param>
		public GLColumn( string name )
		{
			this.Name = name;
			this.Text = name;
		}

		#endregion

		#region Events and Delegates


		/// <summary>
		/// Column has changed event
		/// </summary>
		public event ChangedEventHandler ChangedEvent;

		#endregion

		#region VarEnumProperties

		private int								m_nWidth = 100;
		private string							m_strName = "Name";
		private string							m_strText = "Column";
		private ColumnStates					m_State = ColumnStates.csNone;
		private SortDirections					m_LastSortDirection = SortDirections.SortDescending;
		private ArrayList						m_ActiveControlItems = new ArrayList();		
		private ContentAlignment				m_TextAlignment = ContentAlignment.MiddleLeft;
		private int								m_ImageIndex = -1;
		private bool							m_bCheckBoxes = false;
		private GlacialList						m_Parent = null;

		private Control							m_ActivatedEmbeddedControlTemplate = null;
		private GLActivatedEmbeddedTypes		m_ActivatedEmbeddedType = GLActivatedEmbeddedTypes.None;


		private bool							m_bNumericSort = false;







		/// <summary>
		/// Not sure if I'm going to end up using this
		/// </summary>
		[
		Description("Activated embedded control types available."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public Control ActivatedEmbeddedControlTemplate 
		{
			get { return m_ActivatedEmbeddedControlTemplate; }
			set 
			{	// interogate control to make sure it has the correct interface
				m_ActivatedEmbeddedControlTemplate = value; 
			}
		}


		/// <summary>
		/// Type of activated embedded control to use
		/// </summary>
		/// <remarks>
		/// This comes with some built in basic types but is also useable with the User type if you want to put your
		/// own type in.
		/// </remarks>
		[
		Description("Type of system embedded control you would like activated in place here."),
		Category("Behavior"),
		Browsable( true )
		]
		public GLActivatedEmbeddedTypes ActivatedEmbeddedType
		{
			get 
			{
				return m_ActivatedEmbeddedType;
			}
			set
			{
				// set the activated embedded control template here
				m_ActivatedEmbeddedType = value;

				// only handle system types
				if ( value == GLActivatedEmbeddedTypes.TextBox )
				{
					this.ActivatedEmbeddedControlTemplate = new GLTextBox();
				}
				else if ( value == GLActivatedEmbeddedTypes.ComboBox )
				{
					this.ActivatedEmbeddedControlTemplate = new GLComboBox();
				}
				else if ( value == GLActivatedEmbeddedTypes.DateTimePicker )
				{
					this.ActivatedEmbeddedControlTemplate = new GLDateTimePicker();
				}
				else if ( value == GLActivatedEmbeddedTypes.None )
				{
					this.ActivatedEmbeddedControlTemplate = null;
				}

				// if its none or user control them leave it alone
			}
		}


#if false
		[
		Browsable(false)
		]
		public ImageList ImageList
		{
			get { return Parent.ImageList; }
		}
#endif

		/// <summary>
		/// Whether or not NumericSort are visible in this column
		/// </summary>
		[
		Description("When sort turned on, only compare numeric values in cells."),
		Category("Behavior"),
		Browsable( true )
		]
		public bool NumericSort
		{
			get { return m_bNumericSort; }
			set { m_bNumericSort = value; }
		}

		/// <summary>
		/// pointer to parent
		/// </summary>
		[
		Description("Parent"),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable(false)
		]
		public GlacialList Parent
		{
			get	{ return this.m_Parent; }
			set	{ this.m_Parent = value; }
		}


		/// <summary>
		/// Whether or not checkboxes are visible in this column
		/// </summary>
		[
		Description("Whether or not checkboxes are visible in this column."),
		Category("Behavior"),
		Browsable( true )
		]
		public bool CheckBoxes
		{
			get { return m_bCheckBoxes; }
			set { m_bCheckBoxes = value; }
		}


		/// <summary>
		/// Image Index
		/// </summary>
		/// <remarks>
		/// Index of image based on image list included in main list
		/// </remarks>
		[
		TypeConverter( typeof( System.Windows.Forms.ImageIndexConverter ) )
		]
		public int ImageIndex
		{
			get { return m_ImageIndex; }
			set { m_ImageIndex = value; }
		}


		/// <summary>
		/// Alignment of text in the header and in the cells
		/// </summary>
		[
		Description("Text alignment inside column header."),
		Browsable( true )
		]
		public ContentAlignment TextAlignment
		{
			get { return m_TextAlignment; }
			set { m_TextAlignment = value; }
		}


		/// <summary>
		/// ActiveControlItems
		/// </summary>
		/// <remarks>
		/// this holds references to items that currently contain live controls.  
		/// This is an optimization so I don't have to iterate the entire Items list
		/// each draw cycle to remove controls no longer visible
		/// </remarks>
		[
		Description("Array of items that have live controls."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable( false )
		]
		public ArrayList ActiveControlItems
		{
			get { return m_ActiveControlItems; }
			set { m_ActiveControlItems = value; }
		}



		/// <summary>
		/// Last sort state
		/// </summary>
		[
		Description("Last time sort was done, which direction."),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden),
		Browsable( false )
		]
		public SortDirections LastSortState
		{
			get { return m_LastSortDirection; }
			set { m_LastSortDirection = value; }
		}


		/// <summary>
		/// Width of column
		/// </summary>
		[
		Category("Design"),
		Browsable( true )
		]
		public int Width
		{
			get
			{
				return m_nWidth;
			}
			set
			{
				if ( m_nWidth != value )
				{
					m_nWidth = value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnChanged, this, null, null ) );				// fire the column clicked event
				}
			}
		}


		/// <summary>
		/// Text 
		/// </summary>
		[
		Category("Misc"),
		Description("Text to be displayed in header."),
		Browsable( true )
		]
		public string Text
		{
			get
			{
				return m_strText;
			}
			set
			{
				if ( m_strText != (string)value )
				{
					m_strText = (string)value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnChanged, this, null, null ) );				// fire the column clicked event
				}
			}
		}

		/// <summary>
		/// Name of the column internally
		/// </summary>
		[
		Category("Design"),
		Browsable( true )
		]
		public string Name
		{
			get
			{
				return m_strName;
			}
			set
			{
				if ( m_strName != (string)value )
				{
					m_strName = (string)value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnChanged, this, null, null ) );				// fire the column clicked event
				}
			}
		}


		/// <summary>
		/// State of the column
		/// </summary>
		[
		Browsable(false),
		DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)
		]
		public ColumnStates State
		{
			get
			{
				return m_State;
			}
			set
			{
				if ( m_State != value)
				{
					m_State = value;
					if ( ChangedEvent != null )
						ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnStateChanged, this, null, null ) );				// fire the column clicked event
				}
			}
		}


		#endregion
	}


	/// <summary>
	/// gl column collection
	/// </summary>
	public class GLColumnCollection : CollectionBase
	{
		#region Construction

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="parent"></param>
		public GLColumnCollection( GlacialList parent )
		{
			this.Parent = parent;
		}

		#endregion

		#region Events and Delegates


		/// <summary>
		/// Column or Column Collection has changed 
		/// </summary>
		public event ChangedEventHandler ChangedEvent;


		/// <summary>
		/// Column has changed.  Pass Event up the chain.
		/// </summary>
		/// <param name="source"></param>
		/// <param name="e"></param>
		public void GLColumn_Changed( object source, ChangedEventArgs e )
		{	// this gets called when an item internally changes

			if ( ChangedEvent != null )
				ChangedEvent( source, e );				// fire the column clicked event
		}

		#endregion

		#region VarsPropertiesAndEnums

		private GlacialList			m_Parent = null;

		/// <summary>
		/// pointer to parent
		/// </summary>
		[
		Description("Parent"),
		Browsable(false)
		]
		public GlacialList Parent
		{
			get	{ return this.m_Parent; }
			set	{ this.m_Parent = value; }
		}


		/// <summary>
		/// Indexer
		/// </summary>
		public GLColumn this[ int nColumnIndex ]
		{
			get
			{
				return List[nColumnIndex] as GLColumn;
			}
		}


		/// <summary>
		/// Index by column name
		/// </summary>
		public GLColumn this[ string strColumnName ]
		{
			get
			{
				return (GLColumn)List[ GetColumnIndex( strColumnName ) ];			// make sure the column is seeded with which one it is before we call it
			}
		}


		/// <summary>
		/// Get the column index that corresponds to the column name
		/// </summary>
		/// <param name="strColumnName"></param>
		/// <returns></returns>
		public int GetColumnIndex( string strColumnName )
		{

			for ( int index = 0; index < List.Count; index++ )
			{
				GLColumn column = (GLColumn)List[index];
				if ( column.Name == strColumnName )
					return index;
			}

			return -1;
		}


		/// <summary>
		/// the combined width of all of the columns
		/// </summary>
		public int Width
		{
			get
			{
				int nTotalWidth = 0;
				GLColumn col;
				for (int index=0; index<List.Count; index++)
				{
					col = (GLColumn)List[index];
					nTotalWidth += col.Width;
				}

				return nTotalWidth;
			}
		}
		#endregion

		#region Functionality

		/// <summary>
		/// Get Span Size for column spanning
		/// </summary>
		/// <param name="strStartColumnName"></param>
		/// <param name="nColumnsSpanned"></param>
		/// <returns></returns>
		public int GetSpanSize( string strStartColumnName, int nColumnsSpanned )
		{
			int nStartColumn = GetColumnIndex( strStartColumnName );

			int nSpanSize = 0;

			if ( (nColumnsSpanned+nStartColumn) > Count )
				nColumnsSpanned = (Count-nStartColumn);

			for ( int nIndex = nStartColumn; nIndex<(nStartColumn+nColumnsSpanned); nIndex++ )
				nSpanSize += this[nIndex].Width;

			return nSpanSize;
		}


		/// <summary>
		/// Add a column to collection
		/// </summary>
		/// <param name="newColumn"></param>
		public void Add( GLColumn newColumn )
		{
			newColumn.Parent = Parent;

			//item.ChangedEvent += new BSLItem.ChangedEventHandler( BSLItem_Changed );				// listen to event changes inside the item
			newColumn.ChangedEvent += new ChangedEventHandler( GLColumn_Changed );

			while ( GetColumnIndex( newColumn.Name ) != -1 )
				newColumn.Name += "x";					// change the name till it is not the same

			int nIndex = List.Add( newColumn );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnCollectionChanged, newColumn, null, null ) );				// fire the column clicked event
		}

		/// <summary>
		/// Add Column to collection
		/// </summary>
		/// <param name="strColumnName"></param>
		/// <param name="nColumnWidth"></param>
		public GLColumn Add( string strColumnName, int nColumnWidth )
		{
			GLColumn newColumn = new GLColumn();
			newColumn.Text = strColumnName;
			newColumn.Name = strColumnName;
			newColumn.Width = nColumnWidth;
			newColumn.State = ColumnStates.csNone;
			newColumn.TextAlignment = ContentAlignment.MiddleLeft;
			newColumn.Parent = Parent;

			Add( newColumn );

			return newColumn;
		}

		/// <summary>
		/// Add Column to collection
		/// </summary>
		/// <param name="strColumnName"></param>
		/// <param name="nColumnWidth"></param>
		/// <param name="align"></param>
		public GLColumn Add( string strColumnName, int nColumnWidth, HorizontalAlignment align )
		{
			GLColumn newColumn = new GLColumn();
			newColumn.Text = strColumnName;
			newColumn.Name = strColumnName;
			newColumn.Width = nColumnWidth;
			newColumn.State = ColumnStates.csNone;
			newColumn.TextAlignment = ContentAlignment.MiddleLeft;

			Add( newColumn );

			return newColumn;
		}

		/// <summary>
		/// Add Range of columns to collection
		/// </summary>
		/// <param name="columns"></param>
		public void AddRange( GLColumn[] columns)
		{
			lock(List.SyncRoot)
			{
				for (int i=0; i<columns.Length; i++)
					Add( columns[i] );
			}
		}

		/// <summary>
		/// Remove Column from collection
		/// </summary>
		/// <param name="nColumnIndex"></param>
		public void Remove( int nColumnIndex )
		{
			if ( ( nColumnIndex >= this.Count ) || (nColumnIndex < 0) )
				return;			// error

			List.RemoveAt( nColumnIndex );

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnCollectionChanged, null, null, null ) );				// fire the column clicked event
		}

		/// <summary>
		/// Remove all columns from collection
		/// </summary>
		public new void Clear()
		{
			List.Clear();

			if ( ChangedEvent != null )
				ChangedEvent( this, new ChangedEventArgs( ChangedTypes.ColumnCollectionChanged, null, null, null ) );				// fire the column clicked event
		}

		/// <summary>
		/// Return index of column in collection
		/// </summary>
		/// <param name="column"></param>
		/// <returns></returns>
		public int IndexOf( GLColumn column )
		{
			return List.IndexOf( column );
		}

		/// <summary>
		/// Clear column states
		/// </summary>
		/// <remarks>
		/// Primarily used to clear pressed / hot states
		/// </remarks>
		public void ClearStates()
		{
			foreach ( GLColumn column in List )
				column.State = ColumnStates.csNone;
		}

		/// <summary>
		/// Clear only hot states from column collection
		/// </summary>
		public void ClearHotStates()
		{
			foreach ( GLColumn column in List )
			{
				if ( column.State == ColumnStates.csHot )
					column.State = ColumnStates.csNone;
			}
		}

		/// <summary>
		/// if any of the columns are in a pressed state then disable all hotting
		/// </summary>
		/// <returns></returns>
		public bool AnyPressed()
		{
			foreach ( GLColumn column in List )
				if ( column.State == ColumnStates.csPressed )
					return true;

			return false;
		}

		#endregion
	}


	#region Collection Editors

	/// <summary>
	/// Class created so we can force an invalidation/update on the control when the column editor returns
	/// </summary>
	internal class CustomCollectionEditor : CollectionEditor
	{
		private int m_nUnique = 1;

		/// <summary>
		/// Default Constructor for custom column collection editor
		/// </summary>
		/// <param name="type"></param>
		public CustomCollectionEditor(Type type) : base(type)
		{
			
		}

		/// <summary>
		/// Called to edit a value in collection editor
		/// </summary>
		/// <param name="context"></param>
		/// <param name="isp"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public override object EditValue(ITypeDescriptorContext context, IServiceProvider isp, object value)
		{
			GlacialList originalControl = (GlacialList)context.Instance;

			object returnObject = base.EditValue( context, isp, value );

			originalControl.Refresh();//.Invalidate( true );
			return returnObject;
		}

		/// <summary>
		/// Creates a new instance of a column for custom collection
		/// </summary>
		/// <param name="itemType"></param>
		/// <returns></returns>
		protected override object CreateInstance(Type itemType)
		{
			// here we are making sure that we generate a unique column name every time
			object[] cols;
			string strTmpColName;
			do
			{
				strTmpColName = "Column" + m_nUnique.ToString();
				cols = this.GetItems( strTmpColName );

				m_nUnique++;
			} while ( cols.Length != 0 );

			// instance the column and set its ident name
			object col = base.CreateInstance (itemType);
			((GLColumn)col).Name = strTmpColName;

			return col;
		}


	}

	/// <summary>
	/// GLColumnConverter
	/// </summary>
	/// 
	public class GLColumnConverter : TypeConverter
	{
		/// <summary>
		/// Required for correct collection editor use
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
		/// Required for correct collection editor use
		/// </summary>
		/// <param name="context"></param>
		/// <param name="culture"></param>
		/// <param name="value"></param>
		/// <param name="destinationType"></param>
		/// <returns></returns>
		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(InstanceDescriptor) && value is GLColumn)
			{
				GLColumn column = (GLColumn)value;
              
				ConstructorInfo ci = typeof(GLColumn).GetConstructor(new Type[] {});
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
