#if STATS

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

using MonoTorrent.Client;
using MonoTorrent.Common;

namespace SampleClient.Stats
{
    /// <summary>
    /// blue = block received
    /// red = normal
    /// green = outstanding block (> 1 block request)
    /// </summary>
    internal partial class PieceView : UserControl
    {
        private Piece piece;

        public Piece Piece
        {
            get { return this.piece; }
            set
            {
                this.piece = value;
                InitializePieceView();
            }
        }

        public PieceView()
            : this(null)
        { }


        public PieceView(Piece p)
        {
            InitializeComponent();

            this.Piece = p;
        }


        private void InitializePieceView()
        {
            foreach (Control c in panel1.Controls)
            {
                c.Dispose();
            }

            Utils.PerformControlOperation(this.panel1, new NoParam(ClearBlocks));

            if (this.piece != null)
            {
                int startX = 0;

                for (int x = 0; x < piece.BlockCount; x++)
                {
                    Label label = new Label();

                    label.Location = new System.Drawing.Point(startX, 2);
                    label.Text = x.ToString();
                    label.Size = new System.Drawing.Size(10, 17);
                    label.TabIndex = 0;
                    label.TabStop = false;
                    label.BackColor = GetBlockColor(piece.Blocks[x]);

                    Utils.PerformControlOperation(this.panel1, delegate { panel1.Controls.Add(label); });

                    startX += 15;
                }

            }
        }


        private void ClearBlocks()
        {
            for (int i = 0; i < this.panel1.Controls.Count; )
            {
                if (this.panel1.Controls[i] is PictureBox)
                {
                    this.panel1.Controls[i].Dispose();
                    this.panel1.Controls.RemoveAt(i);
                }
                else
                    i++;
            }
        }

        /// <summary>
        /// Update the block coloring with the status of the piece
        /// </summary>
        public void UpdateBlock()
        {
            Utils.PerformControlOperation(this.panel1, delegate
            {
                for (int x = 0; x < piece.BlockCount; x++)
                {
                    Control c = panel1.Controls[x];
                    Block b = piece[x];

                    c.BackColor = GetBlockColor(b);
                }
            });
        }

        private Color GetBlockColor(Block b)
        {
            if (b.Received)
            {
                return Color.Blue;
            }
            else if (b.Requested)
            {
                return Color.Green;
            }
            else // Normal
            {
                return Color.Red;
            }
        }
    }
}
#endif
