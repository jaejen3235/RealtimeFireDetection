
namespace RealtimeFireDetection
{
    partial class FormRoi
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.btNonSave = new System.Windows.Forms.Button();
            this.btNonRemove = new System.Windows.Forms.Button();
            this.btNonAdd = new System.Windows.Forms.Button();
            this.lbNonRoi = new System.Windows.Forms.ListBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.pbArea = new System.Windows.Forms.PictureBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.btRoiSave = new System.Windows.Forms.Button();
            this.btRoiRemove = new System.Windows.Forms.Button();
            this.btRoiAdd = new System.Windows.Forms.Button();
            this.lbRoi = new System.Windows.Forms.ListBox();
            this.cbRoiShow = new System.Windows.Forms.CheckBox();
            this.cbNonRoiShow = new System.Windows.Forms.CheckBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.tsMousePoint = new System.Windows.Forms.ToolStripStatusLabel();
            this.tsState = new System.Windows.Forms.ToolStripStatusLabel();
            this.groupBox1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbArea)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.groupBox4);
            this.groupBox1.Controls.Add(this.groupBox3);
            this.groupBox1.Controls.Add(this.groupBox2);
            this.groupBox1.Location = new System.Drawing.Point(4, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(932, 412);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.cbNonRoiShow);
            this.groupBox4.Controls.Add(this.btNonSave);
            this.groupBox4.Controls.Add(this.btNonRemove);
            this.groupBox4.Controls.Add(this.btNonAdd);
            this.groupBox4.Controls.Add(this.lbNonRoi);
            this.groupBox4.Location = new System.Drawing.Point(800, 9);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(122, 396);
            this.groupBox4.TabIndex = 12;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "NonROI";
            // 
            // btNonSave
            // 
            this.btNonSave.Location = new System.Drawing.Point(6, 362);
            this.btNonSave.Name = "btNonSave";
            this.btNonSave.Size = new System.Drawing.Size(105, 21);
            this.btNonSave.TabIndex = 3;
            this.btNonSave.Text = "Save";
            this.btNonSave.UseVisualStyleBackColor = true;
            this.btNonSave.Click += new System.EventHandler(this.btNonSave_Click);
            // 
            // btNonRemove
            // 
            this.btNonRemove.Location = new System.Drawing.Point(6, 335);
            this.btNonRemove.Name = "btNonRemove";
            this.btNonRemove.Size = new System.Drawing.Size(105, 21);
            this.btNonRemove.TabIndex = 2;
            this.btNonRemove.Text = "Remove";
            this.btNonRemove.UseVisualStyleBackColor = true;
            this.btNonRemove.Click += new System.EventHandler(this.btNonRemove_Click);
            // 
            // btNonAdd
            // 
            this.btNonAdd.Location = new System.Drawing.Point(7, 308);
            this.btNonAdd.Name = "btNonAdd";
            this.btNonAdd.Size = new System.Drawing.Size(105, 21);
            this.btNonAdd.TabIndex = 1;
            this.btNonAdd.Text = "Add";
            this.btNonAdd.UseVisualStyleBackColor = true;
            this.btNonAdd.Click += new System.EventHandler(this.btNonAdd_Click);
            // 
            // lbNonRoi
            // 
            this.lbNonRoi.FormattingEnabled = true;
            this.lbNonRoi.ItemHeight = 12;
            this.lbNonRoi.Location = new System.Drawing.Point(7, 20);
            this.lbNonRoi.Name = "lbNonRoi";
            this.lbNonRoi.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.lbNonRoi.Size = new System.Drawing.Size(106, 256);
            this.lbNonRoi.TabIndex = 0;
            this.lbNonRoi.SelectedIndexChanged += new System.EventHandler(this.lbNonRoi_SelectedIndexChanged);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.pbArea);
            this.groupBox3.Location = new System.Drawing.Point(134, 9);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(660, 396);
            this.groupBox3.TabIndex = 8;
            this.groupBox3.TabStop = false;
            // 
            // pbArea
            // 
            this.pbArea.Location = new System.Drawing.Point(12, 20);
            this.pbArea.Name = "pbArea";
            this.pbArea.Size = new System.Drawing.Size(640, 360);
            this.pbArea.TabIndex = 6;
            this.pbArea.TabStop = false;
            this.pbArea.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pbArea_MouseDown);
            this.pbArea.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pbArea_MouseMove);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.cbRoiShow);
            this.groupBox2.Controls.Add(this.btRoiSave);
            this.groupBox2.Controls.Add(this.btRoiRemove);
            this.groupBox2.Controls.Add(this.btRoiAdd);
            this.groupBox2.Controls.Add(this.lbRoi);
            this.groupBox2.Location = new System.Drawing.Point(6, 9);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(122, 396);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "ROI";
            // 
            // btRoiSave
            // 
            this.btRoiSave.Location = new System.Drawing.Point(6, 362);
            this.btRoiSave.Name = "btRoiSave";
            this.btRoiSave.Size = new System.Drawing.Size(105, 21);
            this.btRoiSave.TabIndex = 3;
            this.btRoiSave.Text = "Save";
            this.btRoiSave.UseVisualStyleBackColor = true;
            this.btRoiSave.Click += new System.EventHandler(this.btRoiSave_Click);
            // 
            // btRoiRemove
            // 
            this.btRoiRemove.Location = new System.Drawing.Point(6, 335);
            this.btRoiRemove.Name = "btRoiRemove";
            this.btRoiRemove.Size = new System.Drawing.Size(105, 21);
            this.btRoiRemove.TabIndex = 2;
            this.btRoiRemove.Text = "Remove";
            this.btRoiRemove.UseVisualStyleBackColor = true;
            this.btRoiRemove.Click += new System.EventHandler(this.btRoiRemove_Click);
            // 
            // btRoiAdd
            // 
            this.btRoiAdd.Location = new System.Drawing.Point(7, 308);
            this.btRoiAdd.Name = "btRoiAdd";
            this.btRoiAdd.Size = new System.Drawing.Size(105, 21);
            this.btRoiAdd.TabIndex = 1;
            this.btRoiAdd.Text = "Add";
            this.btRoiAdd.UseVisualStyleBackColor = true;
            this.btRoiAdd.Click += new System.EventHandler(this.btRoiAdd_Click);
            // 
            // lbRoi
            // 
            this.lbRoi.FormattingEnabled = true;
            this.lbRoi.ItemHeight = 12;
            this.lbRoi.Location = new System.Drawing.Point(7, 20);
            this.lbRoi.Name = "lbRoi";
            this.lbRoi.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.lbRoi.Size = new System.Drawing.Size(106, 256);
            this.lbRoi.TabIndex = 0;
            this.lbRoi.SelectedIndexChanged += new System.EventHandler(this.lbRoi_SelectedIndexChanged);
            // 
            // cbRoiShow
            // 
            this.cbRoiShow.AutoSize = true;
            this.cbRoiShow.Location = new System.Drawing.Point(7, 286);
            this.cbRoiShow.Name = "cbRoiShow";
            this.cbRoiShow.Size = new System.Drawing.Size(56, 16);
            this.cbRoiShow.TabIndex = 4;
            this.cbRoiShow.Text = "Show";
            this.cbRoiShow.UseVisualStyleBackColor = true;
            // 
            // cbNonRoiShow
            // 
            this.cbNonRoiShow.AutoSize = true;
            this.cbNonRoiShow.Location = new System.Drawing.Point(7, 286);
            this.cbNonRoiShow.Name = "cbNonRoiShow";
            this.cbNonRoiShow.Size = new System.Drawing.Size(56, 16);
            this.cbNonRoiShow.TabIndex = 5;
            this.cbNonRoiShow.Text = "Show";
            this.cbNonRoiShow.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.tsMousePoint,
            this.tsState});
            this.statusStrip1.Location = new System.Drawing.Point(0, 433);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(945, 24);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(39, 19);
            this.toolStripStatusLabel1.Text = "Point";
            // 
            // tsMousePoint
            // 
            this.tsMousePoint.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.tsMousePoint.BorderStyle = System.Windows.Forms.Border3DStyle.Etched;
            this.tsMousePoint.Name = "tsMousePoint";
            this.tsMousePoint.Size = new System.Drawing.Size(125, 19);
            this.tsMousePoint.Text = "toolStripStatusLabel2";
            this.tsMousePoint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tsState
            // 
            this.tsState.Name = "tsState";
            this.tsState.Size = new System.Drawing.Size(735, 19);
            this.tsState.Spring = true;
            this.tsState.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // FormRoi
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(945, 457);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Name = "FormRoi";
            this.Text = "관심영역 설정";
            this.Load += new System.EventHandler(this.FormRoi_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormRoi_KeyDown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbArea)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.PictureBox pbArea;
        private System.Windows.Forms.Button btRoiSave;
        private System.Windows.Forms.Button btRoiRemove;
        private System.Windows.Forms.Button btRoiAdd;
        private System.Windows.Forms.ListBox lbRoi;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button btNonSave;
        private System.Windows.Forms.Button btNonRemove;
        private System.Windows.Forms.Button btNonAdd;
        private System.Windows.Forms.ListBox lbNonRoi;
        private System.Windows.Forms.CheckBox cbNonRoiShow;
        private System.Windows.Forms.CheckBox cbRoiShow;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel tsMousePoint;
        private System.Windows.Forms.ToolStripStatusLabel tsState;
    }
}