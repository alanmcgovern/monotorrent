#if STATS

namespace SampleClient.Stats
{
    partial class PeerMessagesDisplay
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
            lock (this)
            {
                if (disposed)
                    return;

                disposed = true;

                if (disposing && (components != null))
                {
                    components.Dispose();
                }
                base.Dispose( disposing );
            }
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent( )
        {
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.FormattingEnabled = true;
            this.listBox1.Location = new System.Drawing.Point( 0, 0 );
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size( 397, 355 );
            this.listBox1.TabIndex = 0;
            // 
            // PeerMessagesDisplay
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size( 397, 356 );
            this.Controls.Add( this.listBox1 );
            this.Name = "PeerMessagesDisplay";
            this.Text = "PeerMessagesDisplay";
            this.ResumeLayout( false );

        }

        #endregion

        private System.Windows.Forms.ListBox listBox1;


    }
}

#endif