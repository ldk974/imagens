namespace WallpaperSyncTest2
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.FlowLayoutPanel flpGrid;
        private System.Windows.Forms.CheckBox chkShowPreviews;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnUndo;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ListBox listWallpapers;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            flpGrid = new FlowLayoutPanel();
            chkShowPreviews = new CheckBox();
            btnRefresh = new Button();
            btnUndo = new Button();
            lblStatus = new Label();
            SuspendLayout();
            // 
            // flpGrid
            // 
            flpGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            flpGrid.AutoScroll = true;
            flpGrid.BackColor = Color.FromArgb(30, 31, 34);
            flpGrid.Location = new Point(12, 42);
            flpGrid.Name = "flpGrid";
            flpGrid.Size = new Size(760, 380);
            flpGrid.TabIndex = 0;
            // 
            // listWallpapers
            // 
            this.listWallpapers = new System.Windows.Forms.ListBox();
            this.listWallpapers.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.listWallpapers.ForeColor = System.Drawing.Color.White;
            this.listWallpapers.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.listWallpapers.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listWallpapers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listWallpapers.Visible = false;
            this.listWallpapers.IntegralHeight = false;
            this.listWallpapers.SelectionMode = System.Windows.Forms.SelectionMode.One;
            this.listWallpapers.ItemHeight = 22;
            this.listWallpapers.DoubleClick += new System.EventHandler(this.listWallpapers_DoubleClick);
            // 
            // chkShowPreviews
            // 
            chkShowPreviews.AutoSize = true;
            chkShowPreviews.Checked = true;
            chkShowPreviews.CheckState = CheckState.Checked;
            chkShowPreviews.ForeColor = Color.White;
            chkShowPreviews.Location = new Point(12, 12);
            chkShowPreviews.Name = "chkShowPreviews";
            chkShowPreviews.Size = new Size(107, 19);
            chkShowPreviews.TabIndex = 1;
            chkShowPreviews.Text = "Mostrar prévias";
            chkShowPreviews.UseVisualStyleBackColor = true;
            chkShowPreviews.CheckedChanged += chkShowPreviews_CheckedChanged;
            // 
            // btnRefresh
            // 
            btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRefresh.Location = new Point(676, 8);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(96, 28);
            btnRefresh.TabIndex = 2;
            btnRefresh.Text = "Atualizar";
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnUndo
            // 
            btnUndo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUndo.Location = new Point(574, 8);
            btnUndo.Name = "btnUndo";
            btnUndo.Size = new Size(96, 28);
            btnUndo.TabIndex = 3;
            btnUndo.Text = "Restaurar";
            btnUndo.UseVisualStyleBackColor = true;
            btnUndo.Click += btnUndo_Click;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.ForeColor = Color.White;
            lblStatus.Location = new Point(12, 430);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(760, 23);
            lblStatus.TabIndex = 4;
            lblStatus.Text = "Aguardando...";
            // 
            // MainForm
            // 
            BackColor = Color.FromArgb(30, 31, 34);
            ClientSize = new Size(784, 462);
            Controls.Add(lblStatus);
            Controls.Add(btnUndo);
            Controls.Add(btnRefresh);
            Controls.Add(chkShowPreviews);
            Controls.Add(flpGrid);
            Controls.Add(this.listWallpapers);
            Font = new Font("Segoe UI", 9F);
            Name = "MainForm";
            Text = "NSFW Wallpaper Selector";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
