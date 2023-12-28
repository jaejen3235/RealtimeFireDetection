using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Point = OpenCvSharp.Point;

namespace RealtimeFireDetection
{
    public partial class FormRoi : Form
    {
        MainForm mainForm;
        Bitmap BackImage;
        const float OnePixelCentimeter = 37.79f;
        const int VerticalLineHeight = 50;
        private Pen gridLineColor;
        List<Point> TempPointList = new List<Point>();
        Point currentPont;
        Pen TempPolyPen = new Pen(Color.Red, 1);

        List<string> roiList;
        List<string> nonRoiList;

        enum DrawState
        {
            Empty,
            Started,
            Closed
        }
        DrawState drawAreaState;

        public FormRoi(Bitmap bitmap, MainForm mainForm)
        {
            InitializeComponent();
            this.BackImage = bitmap;
            this.mainForm = mainForm;
            drawAreaState = DrawState.Empty;
        }

        private void FormRoi_Load(object sender, EventArgs e)
        {
            this.pbArea.Image = BackImage;
            this.pbArea.Paint += pbArea_PaintEventHandler;
            this.SizeChanged += Form_SizeChangedEventHandler;
            this.gridLineColor = Pens.Yellow;

            roiList = mainForm.DicRoiList.Keys.ToList();
            nonRoiList = mainForm.DicNonRoiList.Keys.ToList();

            lbRoi.DataSource = roiList;
            lbNonRoi.DataSource = nonRoiList;
        }

        private void Form_SizeChangedEventHandler(object sender, EventArgs e)
        {
            this.pbArea.Invalidate();
        }

        private void pbArea_PaintEventHandler(object sender, PaintEventArgs e)
        {
            PictureBox pb = (PictureBox)sender;
            Graphics g = e.Graphics;

            float centerYPosition = pb.Height / 2.0f;

            g.DrawLine(Pens.Yellow, 0f, centerYPosition, pb.Width, centerYPosition);

            double cmStep = pb.Width / OnePixelCentimeter;

            int verticalLineHeightHalf = VerticalLineHeight / 2;

            for (float i = 0; i < pb.Width; i += OnePixelCentimeter)
            {
                PointF beginPoint = new PointF(i, centerYPosition - verticalLineHeightHalf);
                PointF endPoint = new PointF(i, centerYPosition + verticalLineHeightHalf);
                g.DrawLine(gridLineColor, beginPoint, endPoint);
            }
            if (TempPointList.Count > 1)
            {
                int i = 0;
                for (i = 0; i < TempPointList.Count - 1; i++)
                {
                    g.DrawLine(TempPolyPen, TempPointList.ElementAt(i).X, TempPointList.ElementAt(i).Y, TempPointList.ElementAt(i + 1).X, TempPointList.ElementAt(i + 1).Y);
                }

                if (drawAreaState == DrawState.Started)
                {
                    g.DrawLine(TempPolyPen, TempPointList.ElementAt(i).X, TempPointList.ElementAt(i).Y, currentPont.X, currentPont.Y);
                }
                if (drawAreaState == DrawState.Closed)
                {
                }
            }
        }

        private void refreshRoiList()
        {
            roiList = mainForm.DicRoiList.Keys.ToList();
            lbRoi.DataSource = null;
            lbRoi.DataSource = roiList;
            lbRoi.Invalidate();
        }

        private void refreshNonRoiList()
        {
            nonRoiList = mainForm.DicNonRoiList.Keys.ToList();
            lbNonRoi.DataSource = null;
            lbNonRoi.DataSource = nonRoiList;
            lbNonRoi.Invalidate();
        }

        private void FormRoi_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("닫습니까?", "Form close", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Dispose();
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void pbArea_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (drawAreaState != DrawState.Closed)
                {
                    if (TempPointList.Count == 0) drawAreaState = DrawState.Started;
                    TempPointList.Add(new Point(e.X, e.Y));
                    pbArea.Invalidate();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                TempPointList.Clear();
                drawAreaState = DrawState.Empty;
                pbArea.Invalidate();
            }
            else//middle
            {
                if (drawAreaState == DrawState.Started)
                {
                    if (TempPointList.Count >= 3)
                    {
                        TempPointList.Add(new Point(TempPointList.ElementAt(0).X, TempPointList.ElementAt(0).Y));
                        drawAreaState = DrawState.Closed;
                    }
                    else
                    {
                        TempPointList.Clear();
                        drawAreaState = DrawState.Empty;
                    }
                    pbArea.Invalidate();
                }
            }
        }

        private void FormRoi_KeyDown(object sender, KeyEventArgs e)
        {
            if (TempPointList.Count > 0 && drawAreaState == DrawState.Started)
            {
                if ((Keys)e.KeyValue == Keys.Escape)
                {
                    TempPointList.RemoveAt(TempPointList.Count - 1);
                    pbArea.Invalidate();
                }
            }
        }

        private void pbArea_MouseMove(object sender, MouseEventArgs e)
        {
            currentPont.X = e.X;
            currentPont.Y = e.Y;
            if (TempPointList.Count == 0) return;
            pbArea.Invalidate();
        }

        private void checkDictionary(Dictionary<string, List<Point>> dic)
        {
            foreach (KeyValuePair<string, List<Point>> kv in dic)
            {
                Console.WriteLine("Key: {0}, Value: {1} PCS", kv.Key, kv.Value.Count);
            }
        }

        private void btRoiAdd_Click(object sender, EventArgs e)
        {
            if (drawAreaState == DrawState.Closed)
            {
                string key = "";
                if (InputBox("Key value", "Input Key value", ref key) == DialogResult.OK)
                {
                    if (key.Length == 0) return;
                    if (mainForm.DicRoiList.ContainsKey(key))
                    {
                        MessageBox.Show("사용중인 Key 입니다.");
                        return;
                    }
                    mainForm.DicRoiList.Add(key, TempPointList.ToList());
                    checkDictionary(mainForm.DicRoiList);
                    TempPointList.Clear();
                    drawAreaState = DrawState.Empty;
                    pbArea.Invalidate();
                    refreshRoiList();
                }
            }
        }

        private void btRoiRemove_Click(object sender, EventArgs e)
        {
            if (mainForm.DicRoiList.Count() > 0)
            {
                mainForm.DicRoiList.Remove((string)lbRoi.SelectedItem);

                refreshRoiList();
            }
        }

        private void btRoiSave_Click(object sender, EventArgs e)
        {

        }

        private void btNonAdd_Click(object sender, EventArgs e)
        {
            if (drawAreaState == DrawState.Closed)
            {
                string key = "";
                if (InputBox("Key value", "Input Key value", ref key) == DialogResult.OK)
                {
                    if (key.Length == 0) return;
                    if (mainForm.DicRoiList.ContainsKey(key))
                    {
                        MessageBox.Show("사용중인 Key 입니다.");
                        return;
                    }
                    mainForm.DicNonRoiList.Add(key, TempPointList.ToList());
                    checkDictionary(mainForm.DicNonRoiList);
                    TempPointList.Clear();
                    drawAreaState = DrawState.Empty;
                    pbArea.Invalidate();
                    refreshNonRoiList();
                }
            }
        }

        private void btNonRemove_Click(object sender, EventArgs e)
        {
            if (mainForm.DicNonRoiList.Count() > 0)
            {
                mainForm.DicNonRoiList.Remove((string)lbNonRoi.SelectedItem);

                refreshNonRoiList();
            }
        }

        private void btNonSave_Click(object sender, EventArgs e)
        {

        }

        public DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new System.Drawing.Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new System.Drawing.Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }
    }
}
