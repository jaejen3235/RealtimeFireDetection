
namespace RealtimeFireDetection
{
    partial class Form1
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btnCapture = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.txtUrl = new System.Windows.Forms.TextBox();
            this._statusTextBox = new System.Windows.Forms.TextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.txtResult = new System.Windows.Forms.TextBox();
            this.streamControl1 = new WebEye.StreamControl.WinForms.StreamControl();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.pbScreen = new System.Windows.Forms.PictureBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbScreen)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCapture
            // 
            this.btnCapture.Location = new System.Drawing.Point(411, 78);
            this.btnCapture.Name = "btnCapture";
            this.btnCapture.Size = new System.Drawing.Size(84, 23);
            this.btnCapture.TabIndex = 16;
            this.btnCapture.Text = "&Capture";
            this.btnCapture.UseVisualStyleBackColor = true;
            this.btnCapture.Click += new System.EventHandler(this.btnCapture_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(411, 45);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(84, 23);
            this.btnStop.TabIndex = 15;
            this.btnStop.Text = "&Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnPlay
            // 
            this.btnPlay.Location = new System.Drawing.Point(411, 16);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(84, 23);
            this.btnPlay.TabIndex = 14;
            this.btnPlay.Text = "&Play";
            this.btnPlay.UseVisualStyleBackColor = true;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // txtUrl
            // 
            this.txtUrl.Location = new System.Drawing.Point(6, 20);
            this.txtUrl.Name = "txtUrl";
            this.txtUrl.Size = new System.Drawing.Size(399, 21);
            this.txtUrl.TabIndex = 13;
            this.txtUrl.Text = "http://localhost/stream.m3u8";
            // 
            // _statusTextBox
            // 
            this._statusTextBox.Location = new System.Drawing.Point(6, 49);
            this._statusTextBox.Name = "_statusTextBox";
            this._statusTextBox.Size = new System.Drawing.Size(399, 21);
            this._statusTextBox.TabIndex = 12;
            // 
            // timer1
            // 
            this.timer1.Interval = 10000;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // txtResult
            // 
            this.txtResult.Location = new System.Drawing.Point(6, 80);
            this.txtResult.Name = "txtResult";
            this.txtResult.Size = new System.Drawing.Size(399, 21);
            this.txtResult.TabIndex = 17;
            // 
            // streamControl1
            // 
            this.streamControl1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.streamControl1.AutoSize = true;
            this.streamControl1.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.streamControl1.Location = new System.Drawing.Point(5, 19);
            this.streamControl1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.streamControl1.Name = "streamControl1";
            this.streamControl1.PreserveStreamAspectRatio = false;
            this.streamControl1.Size = new System.Drawing.Size(492, 263);
            this.streamControl1.TabIndex = 18;
            this.streamControl1.DoubleClick += new System.EventHandler(this.streamControl1_DoubleClick);
            this.streamControl1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.streamControl1_MouseUp);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.pbScreen);
            this.groupBox1.Controls.Add(this.streamControl1);
            this.groupBox1.Location = new System.Drawing.Point(7, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(994, 287);
            this.groupBox1.TabIndex = 19;
            this.groupBox1.TabStop = false;
            // 
            // pbScreen
            // 
            this.pbScreen.Location = new System.Drawing.Point(505, 19);
            this.pbScreen.Name = "pbScreen";
            this.pbScreen.Size = new System.Drawing.Size(481, 262);
            this.pbScreen.TabIndex = 19;
            this.pbScreen.TabStop = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.txtUrl);
            this.groupBox2.Controls.Add(this._statusTextBox);
            this.groupBox2.Controls.Add(this.txtResult);
            this.groupBox2.Controls.Add(this.btnPlay);
            this.groupBox2.Controls.Add(this.btnCapture);
            this.groupBox2.Controls.Add(this.btnStop);
            this.groupBox2.Location = new System.Drawing.Point(7, 305);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(994, 119);
            this.groupBox2.TabIndex = 20;
            this.groupBox2.TabStop = false;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(1007, 429);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbScreen)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCapture;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.TextBox txtUrl;
        private System.Windows.Forms.TextBox _statusTextBox;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TextBox txtResult;
        private WebEye.StreamControl.WinForms.StreamControl streamControl1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.PictureBox pbScreen;
    }
}

