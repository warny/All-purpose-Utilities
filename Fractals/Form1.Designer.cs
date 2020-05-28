namespace Fractals
{
	partial class Form1
	{
		/// <summary>
		/// Variable nécessaire au concepteur.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Nettoyage des ressources utilisées.
		/// </summary>
		/// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Code généré par le Concepteur Windows Form

		/// <summary>
		/// Méthode requise pour la prise en charge du concepteur - ne modifiez pas
		/// le contenu de cette méthode avec l'éditeur de code.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.toolStripContainer1 = new System.Windows.Forms.ToolStripContainer();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.tabs = new System.Windows.Forms.TabControl();
			this.tabMandelbrot = new System.Windows.Forms.TabPage();
			this.tabJulia = new System.Windows.Forms.TabPage();
			this.DisplayJulia = new System.Windows.Forms.PictureBox();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.fichierToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.mnuQuit = new System.Windows.Forms.ToolStripMenuItem();
			this.timer1 = new System.Windows.Forms.Timer(this.components);
			this.DisplayMandelbrot = new System.Windows.Forms.PictureBox();
			this.toolStripContainer1.BottomToolStripPanel.SuspendLayout();
			this.toolStripContainer1.ContentPanel.SuspendLayout();
			this.toolStripContainer1.TopToolStripPanel.SuspendLayout();
			this.toolStripContainer1.SuspendLayout();
			this.tabs.SuspendLayout();
			this.tabMandelbrot.SuspendLayout();
			this.tabJulia.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.DisplayJulia)).BeginInit();
			this.menuStrip1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.DisplayMandelbrot)).BeginInit();
			this.SuspendLayout();
			// 
			// toolStripContainer1
			// 
			// 
			// toolStripContainer1.BottomToolStripPanel
			// 
			this.toolStripContainer1.BottomToolStripPanel.Controls.Add(this.statusStrip1);
			// 
			// toolStripContainer1.ContentPanel
			// 
			this.toolStripContainer1.ContentPanel.Controls.Add(this.tabs);
			this.toolStripContainer1.ContentPanel.Size = new System.Drawing.Size(800, 404);
			this.toolStripContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.toolStripContainer1.Location = new System.Drawing.Point(0, 0);
			this.toolStripContainer1.Name = "toolStripContainer1";
			this.toolStripContainer1.Size = new System.Drawing.Size(800, 450);
			this.toolStripContainer1.TabIndex = 0;
			this.toolStripContainer1.Text = "toolStripContainer1";
			// 
			// toolStripContainer1.TopToolStripPanel
			// 
			this.toolStripContainer1.TopToolStripPanel.Controls.Add(this.menuStrip1);
			// 
			// statusStrip1
			// 
			this.statusStrip1.Dock = System.Windows.Forms.DockStyle.None;
			this.statusStrip1.Location = new System.Drawing.Point(0, 0);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(800, 22);
			this.statusStrip1.TabIndex = 0;
			// 
			// tabs
			// 
			this.tabs.Controls.Add(this.tabMandelbrot);
			this.tabs.Controls.Add(this.tabJulia);
			this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabs.Location = new System.Drawing.Point(0, 0);
			this.tabs.Name = "tabs";
			this.tabs.SelectedIndex = 0;
			this.tabs.Size = new System.Drawing.Size(800, 404);
			this.tabs.TabIndex = 1;
			// 
			// tabMandelbrot
			// 
			this.tabMandelbrot.Controls.Add(this.DisplayMandelbrot);
			this.tabMandelbrot.Location = new System.Drawing.Point(4, 22);
			this.tabMandelbrot.Name = "tabMandelbrot";
			this.tabMandelbrot.Padding = new System.Windows.Forms.Padding(3);
			this.tabMandelbrot.Size = new System.Drawing.Size(792, 378);
			this.tabMandelbrot.TabIndex = 0;
			this.tabMandelbrot.Text = "Mandelbrot";
			this.tabMandelbrot.UseVisualStyleBackColor = true;
			// 
			// tabJulia
			// 
			this.tabJulia.Controls.Add(this.DisplayJulia);
			this.tabJulia.Location = new System.Drawing.Point(4, 22);
			this.tabJulia.Name = "tabJulia";
			this.tabJulia.Padding = new System.Windows.Forms.Padding(3);
			this.tabJulia.Size = new System.Drawing.Size(792, 378);
			this.tabJulia.TabIndex = 1;
			this.tabJulia.Text = "Julia";
			this.tabJulia.UseVisualStyleBackColor = true;
			// 
			// DisplayJulia
			// 
			this.DisplayJulia.Dock = System.Windows.Forms.DockStyle.Fill;
			this.DisplayJulia.Location = new System.Drawing.Point(3, 3);
			this.DisplayJulia.Name = "DisplayJulia";
			this.DisplayJulia.Size = new System.Drawing.Size(786, 372);
			this.DisplayJulia.TabIndex = 3;
			this.DisplayJulia.TabStop = false;
			// 
			// menuStrip1
			// 
			this.menuStrip1.Dock = System.Windows.Forms.DockStyle.None;
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fichierToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(800, 24);
			this.menuStrip1.TabIndex = 0;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// fichierToolStripMenuItem
			// 
			this.fichierToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuQuit});
			this.fichierToolStripMenuItem.Name = "fichierToolStripMenuItem";
			this.fichierToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
			this.fichierToolStripMenuItem.Text = "&Fichier";
			// 
			// mnuQuit
			// 
			this.mnuQuit.Name = "mnuQuit";
			this.mnuQuit.Size = new System.Drawing.Size(111, 22);
			this.mnuQuit.Text = "&Quitter";
			this.mnuQuit.Click += new System.EventHandler(this.mnuQuit_Click);
			// 
			// timer1
			// 
			this.timer1.Enabled = true;
			this.timer1.Interval = 10;
			this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
			// 
			// DisplayMandelbrot
			// 
			this.DisplayMandelbrot.Dock = System.Windows.Forms.DockStyle.Fill;
			this.DisplayMandelbrot.Location = new System.Drawing.Point(3, 3);
			this.DisplayMandelbrot.Name = "DisplayMandelbrot";
			this.DisplayMandelbrot.Size = new System.Drawing.Size(786, 372);
			this.DisplayMandelbrot.TabIndex = 0;
			this.DisplayMandelbrot.TabStop = false;
			// 
			// Form1
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(800, 450);
			this.Controls.Add(this.toolStripContainer1);
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "Form1";
			this.Text = "Form1";
			this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
			this.Load += new System.EventHandler(this.Form1_Load);
			this.toolStripContainer1.BottomToolStripPanel.ResumeLayout(false);
			this.toolStripContainer1.BottomToolStripPanel.PerformLayout();
			this.toolStripContainer1.ContentPanel.ResumeLayout(false);
			this.toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
			this.toolStripContainer1.TopToolStripPanel.PerformLayout();
			this.toolStripContainer1.ResumeLayout(false);
			this.toolStripContainer1.PerformLayout();
			this.tabs.ResumeLayout(false);
			this.tabMandelbrot.ResumeLayout(false);
			this.tabJulia.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.DisplayJulia)).EndInit();
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.DisplayMandelbrot)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ToolStripContainer toolStripContainer1;
		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem fichierToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem mnuQuit;
		private System.Windows.Forms.Timer timer1;
		private System.Windows.Forms.TabControl tabs;
		private System.Windows.Forms.TabPage tabMandelbrot;
		private System.Windows.Forms.TabPage tabJulia;
		private System.Windows.Forms.PictureBox DisplayJulia;
		private System.Windows.Forms.PictureBox DisplayMandelbrot;
	}
}

