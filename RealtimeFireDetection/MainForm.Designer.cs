﻿
namespace RealtimeFireDetection
{
    partial class MainForm
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
            this.components = new System.ComponentModel.Container();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.btResTest = new System.Windows.Forms.Button();
            this.tbText = new System.Windows.Forms.TextBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.btOpenCam = new System.Windows.Forms.Button();
            this.btRoiConfig = new System.Windows.Forms.Button();
            this.cbVitualFlame = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.pbResult = new System.Windows.Forms.PictureBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.pbScreen = new System.Windows.Forms.PictureBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.lbLog = new System.Windows.Forms.ListBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.groupBox1.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbResult)).BeginInit();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbScreen)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.groupBox6);
            this.groupBox1.Controls.Add(this.groupBox5);
            this.groupBox1.Controls.Add(this.groupBox4);
            this.groupBox1.Controls.Add(this.groupBox3);
            this.groupBox1.Location = new System.Drawing.Point(3, 1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(1345, 487);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "영상";
            // 
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.btResTest);
            this.groupBox6.Controls.Add(this.tbText);
            this.groupBox6.Location = new System.Drawing.Point(672, 422);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(663, 57);
            this.groupBox6.TabIndex = 11;
            this.groupBox6.TabStop = false;
            // 
            // btResTest
            // 
            this.btResTest.Location = new System.Drawing.Point(15, 17);
            this.btResTest.Name = "btResTest";
            this.btResTest.Size = new System.Drawing.Size(75, 29);
            this.btResTest.TabIndex = 1;
            this.btResTest.Text = "RES Test";
            this.btResTest.UseVisualStyleBackColor = true;
            this.btResTest.Click += new System.EventHandler(this.btResTest_Click);
            // 
            // tbText
            // 
            this.tbText.Enabled = false;
            this.tbText.Location = new System.Drawing.Point(96, 22);
            this.tbText.Name = "tbText";
            this.tbText.Size = new System.Drawing.Size(556, 21);
            this.tbText.TabIndex = 0;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.btOpenCam);
            this.groupBox5.Controls.Add(this.btRoiConfig);
            this.groupBox5.Controls.Add(this.cbVitualFlame);
            this.groupBox5.Controls.Add(this.button1);
            this.groupBox5.Location = new System.Drawing.Point(8, 422);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(658, 57);
            this.groupBox5.TabIndex = 10;
            this.groupBox5.TabStop = false;
            // 
            // btOpenCam
            // 
            this.btOpenCam.Location = new System.Drawing.Point(16, 18);
            this.btOpenCam.Name = "btOpenCam";
            this.btOpenCam.Size = new System.Drawing.Size(100, 27);
            this.btOpenCam.TabIndex = 12;
            this.btOpenCam.Text = "Start";
            this.btOpenCam.UseVisualStyleBackColor = true;
            this.btOpenCam.Click += new System.EventHandler(this.btOpenCam_Click);
            // 
            // btRoiConfig
            // 
            this.btRoiConfig.Location = new System.Drawing.Point(131, 17);
            this.btRoiConfig.Name = "btRoiConfig";
            this.btRoiConfig.Size = new System.Drawing.Size(79, 28);
            this.btRoiConfig.TabIndex = 11;
            this.btRoiConfig.Text = "ROI Config";
            this.btRoiConfig.UseVisualStyleBackColor = true;
            this.btRoiConfig.Click += new System.EventHandler(this.btRoiConfig_Click);
            // 
            // cbVitualFlame
            // 
            this.cbVitualFlame.AutoSize = true;
            this.cbVitualFlame.Location = new System.Drawing.Point(472, 24);
            this.cbVitualFlame.Name = "cbVitualFlame";
            this.cbVitualFlame.Size = new System.Drawing.Size(76, 16);
            this.cbVitualFlame.TabIndex = 10;
            this.cbVitualFlame.Text = "가상 화염";
            this.cbVitualFlame.UseVisualStyleBackColor = true;
            this.cbVitualFlame.CheckedChanged += new System.EventHandler(this.cbVitualFlame_CheckedChanged);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(566, 17);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(84, 29);
            this.button1.TabIndex = 9;
            this.button1.Text = "Clear Flame";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.pbResult);
            this.groupBox4.Location = new System.Drawing.Point(672, 20);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(663, 396);
            this.groupBox4.TabIndex = 8;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "분석 결과";
            // 
            // pbResult
            // 
            this.pbResult.Location = new System.Drawing.Point(12, 20);
            this.pbResult.Name = "pbResult";
            this.pbResult.Size = new System.Drawing.Size(640, 360);
            this.pbResult.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pbResult.TabIndex = 6;
            this.pbResult.TabStop = false;
            this.pbResult.Paint += new System.Windows.Forms.PaintEventHandler(this.pbResult_Paint);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.pbScreen);
            this.groupBox3.Location = new System.Drawing.Point(6, 20);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(660, 396);
            this.groupBox3.TabIndex = 7;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "원본";
            // 
            // pbScreen
            // 
            this.pbScreen.Location = new System.Drawing.Point(12, 20);
            this.pbScreen.Name = "pbScreen";
            this.pbScreen.Size = new System.Drawing.Size(640, 360);
            this.pbScreen.TabIndex = 6;
            this.pbScreen.TabStop = false;
            this.pbScreen.MouseClick += new System.Windows.Forms.MouseEventHandler(this.pbScreen_MouseClick);
            this.pbScreen.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pbScreen_MouseMove);
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.lbLog);
            this.groupBox2.Location = new System.Drawing.Point(5, 494);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(1344, 319);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "로그";
            // 
            // lbLog
            // 
            this.lbLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lbLog.FormattingEnabled = true;
            this.lbLog.ItemHeight = 12;
            this.lbLog.Location = new System.Drawing.Point(9, 17);
            this.lbLog.Name = "lbLog";
            this.lbLog.Size = new System.Drawing.Size(1328, 292);
            this.lbLog.TabIndex = 0;
            // 
            // timer1
            // 
            this.timer1.Interval = 1000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1352, 824);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "MainForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbResult)).EndInit();
            this.groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pbScreen)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.PictureBox pbScreen;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ListBox lbLog;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.PictureBox pbResult;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.CheckBox cbVitualFlame;
        private System.Windows.Forms.TextBox tbText;
        private System.Windows.Forms.Button btRoiConfig;
        private System.Windows.Forms.Button btResTest;
        private System.Windows.Forms.Button btOpenCam;
        private System.Windows.Forms.Timer timer1;
    }
}