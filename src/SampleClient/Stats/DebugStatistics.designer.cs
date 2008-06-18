#if STATS

using System.Windows.Forms;

namespace SampleClient.Stats
{
    partial class DebugStatistics
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            lock (this.managerLock)
            {
                if (this.disposed)
                    return;

                this.disposed = true;
                
                this.dataGridView1.Dispose();
                this.statsBox.Dispose();

                if(this.stopwatch != null)
                    this.stopwatch.Stop();

                if (disposing && (components != null))
                {
                    components.Dispose();

                    this.connectionLog = null;
                    this.dataGridView1 = null;
                    this.loggers = null;
                    this.manager = null;
                    this.peerList = null;
                    this.peerLogDir = null;
                    this.statsBox = null;
                    this.statsLog = null;
                    this.stopwatch = null;
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent( )
        {
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point( 0, 0 );
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.Size = new System.Drawing.Size( 1272, 530 );
            this.dataGridView1.TabIndex = 8;
            this.dataGridView1.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.dataGridView1_ColumnHeaderMouseClick );
            this.dataGridView1.CellContentDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler( this.dataGridView1_CellContentDoubleClick );
            // 
            // DebugStatistics
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 1272, 530 );
            this.Controls.Add( this.dataGridView1 );
            this.Name = "DebugStatistics";
            this.Text = "Statistics";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout( false );

        }

        #endregion

        public System.Windows.Forms.DataGridView dataGridView1;

    }
}
#endif