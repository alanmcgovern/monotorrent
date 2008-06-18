using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;

using log4net;
using GlacialComponents.Controls;
using MonoTorrent.Client;

namespace SampleClient.Stats
{
    public partial class Pieces : Form
    {
        private TorrentManager manager;
        private Dictionary<int, PieceView> pieceViews;

        private int blocksRequested;
        private int blocksReceived;
        private int blocksCancelled;

        private int completePieces;

        private int outstandingBlockRequests;

        public TorrentManager Manager
        {
            get { return this.manager; }
            set
            {
                if (this.manager != null)
                {
                    this.manager.PieceHashed -= new EventHandler<PieceHashedEventArgs>(PieceHashedHandler);
                    this.manager.PieceManager.BlockReceived -= new EventHandler<BlockEventArgs>(BlockReceivedHandler);
                    this.manager.PieceManager.BlockRequestCancelled -= new EventHandler<BlockEventArgs>(BlockRequestCancelledHandler);
                    this.manager.PieceManager.BlockRequested -= new EventHandler<BlockEventArgs>(BlockRequestedHandler);

                    foreach (PieceView view in pieceViews.Values)
                        view.Dispose();

                    Utils.PerformControlOperation(this.glacialList1, new NoParam(this.glacialList1.Items.Clear));
                }

                this.blocksRequested = 0;
                this.blocksReceived = 0;
                this.blocksCancelled = 0;
                this.completePieces = 0;
                this.outstandingBlockRequests = 0;

                this.manager = value;

                if (this.manager != null)
                {
                    this.manager.PieceHashed += new EventHandler<PieceHashedEventArgs>(PieceHashedHandler);
                    this.manager.PieceManager.BlockReceived += new EventHandler<BlockEventArgs>(BlockReceivedHandler);
                    this.manager.PieceManager.BlockRequestCancelled += new EventHandler<BlockEventArgs>(BlockRequestCancelledHandler);
                    this.manager.PieceManager.BlockRequested += new EventHandler<BlockEventArgs>(BlockRequestedHandler);

                    Utils.PerformControlOperation(this, delegate
                        {
                            this.Text = "Pieces:" + manager.ToString();
                        });
                }
            }
        }


        public Pieces()
        {
            InitializeComponent();

            this.pieceViews = new Dictionary<int, PieceView>();

            Utils.PerformControlOperation(this.glacialList1, new NoParam(delegate
                {
                    this.glacialList1.Columns.Add("Index", 50);
                    this.glacialList1.Columns.Add("Blocks", 500);

                    this.glacialList1.Columns[0].NumericSort = true;
                }));

            this.glacialList1.ItemHeight = 20;

            this.Show();

            SetPiecesText();
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void PieceHashedHandler(object sender, PieceHashedEventArgs args)
        {
            if (args.HashPassed && !this.pieceViews.ContainsKey(args.PieceIndex))
            {
                /*
                // only handle it if we don't have a PieceView for it yet (i.e. we hashed the piece straight from disk,
                // there was no downloading involved)
                Piece p = new Piece(args.PieceIndex, args.TorrentManager.Torrent);

                for (int i = 0; i < p.BlockCount; i++)
                {
                    p.Blocks[i].Received = true;
                }

                PieceView pv = new PieceView(p);
                AddPieceView(new PieceView(p));
                //*/

                //Utils.PerformControlOperation(this.glacialList1, new Action<int>(this.glacialList1.RemoveOldPieces),
    //this.manager.PieceManager.HighPrioritySetStart);

                this.completePieces++;
                SetPiecesText();
            }

            // remove the pieceview to prevent the control from lagging
            Utils.PerformControlOperation(this.glacialList1, delegate { this.glacialList1.RemovePieceView(args.PieceIndex); });
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockReceivedHandler(object sender, BlockEventArgs args)
        {
            this.blocksReceived++;
            this.outstandingBlockRequests--;

            if (args.Piece.TotalReceived == args.Piece.BlockCount)
                this.completePieces++;

            BlockEventHandler(sender, args);
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockRequestCancelledHandler(object sender, BlockEventArgs args)
        {
            this.blocksCancelled++;
            this.outstandingBlockRequests--;
            BlockEventHandler(sender, args);
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockRequestedHandler(object sender, BlockEventArgs args)
        {
            this.blocksRequested++;
            this.outstandingBlockRequests++;
            BlockEventHandler(sender, args);
        }


        private void BlockEventHandler(object sender, BlockEventArgs args)
        {
            PieceView view;

            if (!this.pieceViews.ContainsKey(args.Piece.Index))
            {
                view = new PieceView(args.Piece);
                view.Size = new Size(500, 40);

                this.pieceViews[args.Piece.Index] = view;

                AddPieceView(view);
            }
            else
            {
                view = this.pieceViews[args.Piece.Index];
            }

            view.UpdateBlock();

            //Utils.PerformControlOperation(this.glacialList1, new Action<int>(((PieceList)this.glacialList1).RemoveOldPieces),
            //    this.manager.PieceManager.HighPrioritySetStart);

            Utils.PerformControlOperation(this.glacialList1, new NoParam(this.glacialList1.Refresh));

            SetPiecesText();
        }


        /// <summary>
        /// Add a PieceView to the PieceList
        /// </summary>
        /// <param name="view"></param>
        private void AddPieceView(PieceView view)
        {
            Utils.PerformControlOperation(this.glacialList1, delegate { ((PieceList)this.glacialList1).AddPieceView(view); });
        }


        /// <summary>
        /// Set the display text
        /// </summary>
        private void SetPiecesText()
        {
            StringBuilder text = new StringBuilder();
            text.AppendFormat("Blocks Requested: {0}  Blocks Received: {1}  Blocks Cancelled: {2}\r\n",
                 this.blocksRequested, this.blocksReceived, this.blocksCancelled);

            text.AppendFormat("Outstanding block requests: {0}  Complete pieces: {1}\r\n", this.outstandingBlockRequests, this.completePieces);

            if (this.manager != null)
            {
                // commented this out for now until the SlidingWindowPicker code hits SVN
                /*
                text.AppendFormat("High priority set start: {0}  High Pri Set Size: {1}  Medium Pri Set Size: {2}",
                    this.manager.PieceManager.HighPrioritySetStart, this.manager.PieceManager.HighPrioritySetSize,
                    this.manager.PieceManager.MediumPrioritySetSize);
                */
            }

            //TODO: Write down other statistics here

            Utils.PerformControlOperation(this.textBox1, delegate { this.textBox1.Text = text.ToString(); });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Pieces_SizeChanged(object sender, EventArgs e)
        {
            panel1.Height = ((Pieces)sender).Size.Height - this.textBox1.Height - 60;
        }

    }


    /// <summary>
    /// Extends GlacialList to provide customized sorting of pieces
    /// </summary>
    class PieceList : GlacialList
    {
        /// <summary>
        /// Return PieceView at given index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public PieceView this[int index]
        {
            get { return Items[index].SubItems[1].Control as PieceView; }
        }


        public void RemovePieceView(int pieceIndex)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (this[i] != null && this[i].Piece.Index == pieceIndex)
                {
                    Items.Remove(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Remove pieces before the high priority start
        /// </summary>
        /// <param name="startIndex"></param>
        public void RemoveOldPieces(int startIndex)
        {
            for (int i = 0; i < Items.Count; )
            {
                if (this[i].Piece.Index < startIndex)
                    Items.Remove(i);
                else
                    i++;
            }
        }


        /// <summary>
        /// Add a PieceView and keep the list sorted
        /// </summary>
        /// <param name="view"></param>
        public void AddPieceView(PieceView view)
        {
            // find the place where it should be inserted
            int insertIndex;

            for (insertIndex = 0; insertIndex < Items.Count; insertIndex++)
            {
                try
                {
                    if (view.Piece.Index < this[insertIndex].Piece.Index)
                        break;
                }
                catch (NullReferenceException nre)
                {
                    LogManager.GetLogger("error").Error("", nre);
                }
            }

            // create the new item
            GLItem item = new GLItem();
            item.SubItems[0].Text = view.Piece.Index.ToString();
            item.SubItems[1].Control = view;

            Items.Add(item);

            // everything below the specified index has to be copied down
            for (int i = Items.Count - 2; i >= insertIndex; i--)
            {
                Items[i + 1] = Items[i];
            }

            Items[insertIndex] = item;
        }
    }
}
