using IPC4Fire;
using OpenCvSharp;
using RealtimeFireDetection.Yolov5;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebEye;

namespace RealtimeFireDetection
{
    public partial class Form1 : Form
    {
        private Bitmap _currentFrame;
        private WebEye.Stream _stream;
        YoloDetector detector;
        RemoteObject remoteObject;
        int remoteMessageCount;
        string remoteMessage;

        public Form1()
        {
            InitializeComponent();
            detector = new YoloDetector("best_yolov5.onnx");
        }

        public WebEye.Stream Stream
        {
            get { return _stream; }
            set
            {
                UnSubscribeFromStreamEvents(_stream);
                _stream = value;
                SubscribeToStreamEvents(_stream);
            }
        }

        private void SubscribeToStreamEvents(WebEye.Stream stream)
        {
            if (stream == null)
            {
                return;
            }

            stream.StreamFailed += HandleStreamFailed;
            stream.StreamStopped += HandleStreamStopped;
            stream.FrameRecieved += HandleFrameRecieved;
        }

        private void UnSubscribeFromStreamEvents(WebEye.Stream stream)
        {
            if (stream == null)
            {
                return;
            }

            stream.StreamFailed -= HandleStreamFailed;
            stream.StreamStopped -= HandleStreamStopped;
            stream.FrameRecieved -= HandleFrameRecieved;
        }

        private void HandleFrameRecieved(object sender, FrameRecievedEventArgs e)
        {
            BeginInvoke(new MethodInvoker(delegate ()
            {
                _currentFrame = e.Frame;

                if (_statusTextBox.Text != "Playing")
                {
                    _statusTextBox.Text = "Playing";
                    UpdateButtons();
                }
            }));
        }

        private void HandleStreamStopped(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(delegate ()
            {
                _statusTextBox.Text = "Stopped";
                UpdateButtons();
            }));

        }

        private void HandleStreamFailed(object sender, StreamFailedEventArgs e)
        {
            BeginInvoke(new MethodInvoker(delegate ()
            {
                _statusTextBox.Text = $"Failed: {e.Error}";
                UpdateButtons();
            }));
        }

        private void UpdateButtons()
        {
            btnPlay.Enabled = !string.IsNullOrEmpty(txtUrl.Text);
            btnStop.Enabled = Stream != null;
            btnCapture.Enabled = _currentFrame != null;

        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            //_currentFrame?.Save("capture" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg", ImageFormat.Jpeg);
            Bitmap captureImg = (Bitmap)_currentFrame.Clone();
            //pictureBox1.Image = captureImg;
            using (var image = OpenCvSharp.Extensions.BitmapConverter.ToMat(captureImg))
            {
                float ratio = 0.0f;
                OpenCvSharp.Point diff1 = new OpenCvSharp.Point();
                OpenCvSharp.Point diff2 = new OpenCvSharp.Point();
                var letter_image = YoloDetector.CreateLetterbox(image, new OpenCvSharp.Size(640, 384), new Scalar(114, 114, 114), out ratio, out diff1, out diff2);
                var result = detector.objectDetection(image);

                string strNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                using (var dispImage = image.Clone())
                {
                    if (result.Count > 0)
                    {
                        StreamWriter writer;
                        writer = File.CreateText("../result/result_" + strNow + ".txt");
                        foreach (var obj in result)
                        {
                            Cv2.Rectangle(dispImage, new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin), new OpenCvSharp.Point(obj.Box.Xmax, obj.Box.Ymax), new Scalar(0, 0, 255), 2);
                            Cv2.PutText(dispImage, obj.Label + " " + obj.Confidence.ToString("F2"), new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin - 5), HersheyFonts.HersheySimplex, 1, new Scalar(0, 0, 255), 2);
                            //writer.WriteLine(obj.Box.Xmin + ", " + obj.Box.Xmax + "," + obj.Box.Ymin + "," + obj.Box.Ymax + "," + obj.Confidence.ToString("F2"));
                            remoteMessage = Math.Floor(obj.Box.Xmin + (obj.Box.Xmax - obj.Box.Xmin) * 0.5) + "," + // 중심점 x 좌표
                                Math.Floor(obj.Box.Ymin + (obj.Box.Ymax - obj.Box.Ymin) * 0.5) + "," + // 중심점 y 좌표
                                Math.Floor((obj.Box.Xmax - obj.Box.Xmin)) + "," +                      // 영역 가로 길이
                                Math.Floor((obj.Box.Ymax - obj.Box.Ymin)) + "," +                      // 영역 세로 길이
                                obj.Confidence.ToString("F2");
                            writer.WriteLine(remoteMessage);

                            UpdateRemoteObject("DETECT," + remoteMessage + "," + strNow);
                        }
                        writer.Close();

                        //Cv2.NamedWindow("RESULT", WindowFlags.AutoSize);
                        //Cv2.ImShow("RESULT", dispImage);
                        Bitmap bmpResult = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(dispImage);
                        bmpResult.Save("../result/img_" + strNow + ".jpg", ImageFormat.Jpeg);
                        txtResult.Text = "Detect Fire " + result.Count;
                        UpdateRemoteObject("FIRE," + strNow);
                    }
                    else
                    {
                        txtResult.Text = "No Fire";
                    }
                }
                //Cv2.WaitKey();
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {

            var uri = new Uri(txtUrl.Text);
            Stream = WebEye.Stream.FromUri(uri);

            //streamControl1.AttachStream(Stream);

            Stream.Start();
            _statusTextBox.Text = "Connecting...";
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (timer1.Enabled)
                timer1.Stop();

            Stream?.Stop();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RemoteObject.CreateServer();
            remoteObject = new RemoteObject();

            //Bitmap bitmap = (Bitmap)Image.FromFile(@"c:\temp\test.png");
            //this.Icon = Icon.FromHandle(bitmap.GetHicon()).Save(stream);
            this.Icon = Icon.FromHandle(Properties.Resources.Icon.GetHicon());
            DirectoryInfo di = new DirectoryInfo("../result");

            if (di.Exists == false)
                di.Create();

            btnPlay.PerformClick();
            timer1.Start();
        }

        private void UpdateRemoteObject(string msg)
        {
            remoteObject.Str = remoteMessage;
            remoteObject.Count = remoteMessageCount++;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_statusTextBox.Text.Equals("Playing"))
            {

                btnCapture.PerformClick();
            }
            remoteMessage = string.Format("Remote Message count: {0}", remoteMessageCount);
            UpdateRemoteObject(remoteMessage);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Stream?.Stop();

            if (timer1.Enabled)
                timer1.Stop();
        }
    }
}
