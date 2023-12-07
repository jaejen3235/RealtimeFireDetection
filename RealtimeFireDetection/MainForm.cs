using OpenCvSharp;
using RealtimeFireDetection.Logger;
using RealtimeFireDetection.Yolov5;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RealtimeFireDetection
{
    public partial class MainForm : Form
    {
        string APP_NAME = "FireDetector";
        string VER = "1.0.0";
        //public static string LocalName = "";

        private bool doPlay;
        Thread CamThread;
        YoloDetector detector;
        Image[] flame = new Image[4];
        List<System.Drawing.Point> flameList = new List<System.Drawing.Point>();
        object flameListLock = new object();
        //System.Drawing.Point tempFireAt = new System.Drawing.Point();
        bool drawFlame = false;

        LogMessage message;

        private int removeDays = 1;
        public string targetRecordPath;
        public string targetCapturePath;
        public string targetFirePath;

        private int fireCheckDuration = 10;
        private int recordDuration = 10;
        IniFile ini;
        string CamUri;
        Queue<Mat> matQueue = new Queue<Mat>();
        DateTime receivedEventTime = DateTime.Now;

        int countWatchFlame = 0;
        double scaleDnW;
        double scaleDnH;
        double scaleUpW;
        double scaleUpH;

        enum DetectorState
        {
            NO_FIRE,
            DETECT_FLAME,
            MONITORING_FIRE,
            ALERT
        }
        DetectorState detectorState = DetectorState.NO_FIRE;

        public MainForm()
        {
            InitializeComponent();
            initApp();
        }

        private Bitmap saveFlameInfo(Mat image, List<Prediction> result, bool save)
        {
            string strNow = DateTime.Now.ToString("yyyyMMddHHmmss");
            StreamWriter writer;
            string level = "";
            switch (detectorState)
            {
                case DetectorState.DETECT_FLAME: level = "01"; break;
                case DetectorState.MONITORING_FIRE: level = "02"; break;
                case DetectorState.ALERT: level = "03"; break;
                case DetectorState.NO_FIRE: level = "00"; break;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var obj in result)
            {
                Cv2.Rectangle(image, new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin), new OpenCvSharp.Point(obj.Box.Xmax, obj.Box.Ymax), new Scalar(0, 0, 255), 2);
                Cv2.PutText(image, obj.Label + " " + obj.Confidence.ToString("F2"), new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin - 5), HersheyFonts.HersheySimplex, 1, new Scalar(0, 0, 255), 2);
                //writer.WriteLine(obj.Box.Xmin + ", " + obj.Box.Xmax + "," + obj.Box.Ymin + "," + obj.Box.Ymax + "," + obj.Confidence.ToString("F2"));
                string tmp = Math.Floor(obj.Box.Xmin + (obj.Box.Xmax - obj.Box.Xmin) * 0.5) + "," + // 중심점 x 좌표
                    Math.Floor(obj.Box.Ymin + (obj.Box.Ymax - obj.Box.Ymin) * 0.5) + "," + // 중심점 y 좌표
                    Math.Floor((obj.Box.Xmax - obj.Box.Xmin)) + "," +                      // 영역 가로 길이
                    Math.Floor((obj.Box.Ymax - obj.Box.Ymin)) + "," +                      // 영역 세로 길이
                    obj.Confidence.ToString("F2");
                sb.Append(tmp).Append("/");
            }
            //Cv2.NamedWindow("RESULT", WindowFlags.AutoSize);
            //Cv2.ImShow("RESULT", dispImage);

            Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
            Graphics g = Graphics.FromImage(bmp);
            string[] tmps = sb.ToString().Trim().Split('/');
            int h0 = 0;
            int h1 = 0;

            if (save)
            {
                writer = File.CreateText(targetFirePath + "/" + APP_NAME + "_" + level + "_result_" + strNow + ".txt");
                Font fnt = new Font("Arial", 12, FontStyle.Bold);
                int cnt = 1;
                foreach (string s in tmps)
                {
                    if (s == null || s.Length == 0) break;
                    writer.WriteLine(s);
                    
                    string[] ss = s.Trim().Split(',');
                    foreach (string ks in ss)
                    {
                        if (ks == null || ks.Length == 0) break;
                        Console.WriteLine("Draw flame info: " + ks);
                    }
                    string sw = string.Format("위치{0:D2} X:{1}  Y:{2}  W:{3}  H:{4}  Confidence:{5:0.00}", cnt++, ss[0], ss[1], ss[2], ss[3], ss[4]);
                    g.DrawString(sw, fnt, new SolidBrush(Color.Black), 12, h0 += 62);
                    g.DrawString(sw, fnt, new SolidBrush(Color.Yellow), 10, h1 += 60);
                }
                writer.Close();
                bmp.Save(targetFirePath + "/" + APP_NAME + "_" + level + "_img_" + strNow + ".jpg", ImageFormat.Jpeg);
            }
            return bmp;
        }

        private void checkFlame(Mat image)
        {
            //float ratio = 0.0f;
            //OpenCvSharp.Point diff1 = new OpenCvSharp.Point();
            //OpenCvSharp.Point diff2 = new OpenCvSharp.Point();
            ////var letter_image = YoloDetector.CreateLetterbox(image, new OpenCvSharp.Size(640, 384), new Scalar(114, 114, 114), out ratio, out diff1, out diff2);
            //var result = detector.objectDetection(image);
            var result = DoYoLo(image);
            string msg = "";
            if (result.Count > 0)
            {
                detectorState = DetectorState.DETECT_FLAME;
                Bitmap bmpResult = saveFlameInfo(image, result, true);
                System.Drawing.Size resize = new System.Drawing.Size(pbResult.Width, pbResult.Height);
                Bitmap bmp = new Bitmap(bmpResult, resize);
                pbResult.Image = bmp;

                countWatchFlame = 0;
                msg = string.Format("Detect:{0}", result.Count);
                Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] Detected fire: {0} ", result.Count), true);
                AddLogMessage(message);
            }
            else
            {
                //Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "No fire."), true);
                //AddLogMessage(message);
                msg = string.Format("{0}", "No fire");
            }
            //Cv2.WaitKey();
        }

        private void watchFlame(Mat image)
        {
            //float ratio = 0.0f;
            //OpenCvSharp.Point diff1 = new OpenCvSharp.Point();
            //OpenCvSharp.Point diff2 = new OpenCvSharp.Point();
            ////var letter_image = YoloDetector.CreateLetterbox(image, new OpenCvSharp.Size(640, 384), new Scalar(114, 114, 114), out ratio, out diff1, out diff2);
            //var result = detector.objectDetection(image);
            var result = DoYoLo(image);
            if (result.Count > 0)
            {
                Bitmap bmpResult = saveFlameInfo(image, result, false);
                countWatchFlame++;
                System.Drawing.Size resize = new System.Drawing.Size(pbResult.Width, pbResult.Height);
                Bitmap bmp = new Bitmap(bmpResult, resize);
                pbResult.Image = bmp;
                if(countWatchFlame > 5)
                {
                    detectorState = DetectorState.MONITORING_FIRE;
                    saveFlameInfo(image, result, true);
                    Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "Change state to MONITORING_FIRE"), true);
                    AddLogMessage(message);
                    countWatchFlame = 0;
                }
            }
            else
            {
                countWatchFlame--;
                if(countWatchFlame <= -5)
                {
                    resetState();
                }
            }
        }

        private void monitorFlame(Mat image)
        {
            var result = DoYoLo(image);
            if (result.Count > 0)
            {
                countWatchFlame++;
                Bitmap bmpResult = saveFlameInfo(image, result, false);
                System.Drawing.Size resize = new System.Drawing.Size(pbResult.Width, pbResult.Height);
                Bitmap bmp = new Bitmap(bmpResult, resize);
                pbResult.Image = bmp;
                if (countWatchFlame > 5)
                {
                    detectorState = DetectorState.ALERT;
                    saveFlameInfo(image, result, true);
                    Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "Change state to ALERT"), true);
                    AddLogMessage(message);
                    countWatchFlame = 0;
                }
            }
            else
            {
                countWatchFlame--;
                if (countWatchFlame <= -5)
                {
                    resetState();
                }
            }
        }
        private void alertFlame(Mat image)
        {
            var result = DoYoLo(image);
            if (result.Count <= 0)
            {
                countWatchFlame--;
                if (countWatchFlame <= -5)
                {
                    resetState();
                }
            }
        }

        private void resetState()
        {
            pbResult.Image = null;
            pbResult.Invalidate();
            detectorState = DetectorState.NO_FIRE;
            countWatchFlame = 0;
            Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "Change state to NO_FIRE"), true);
            AddLogMessage(message);
        }

        private List<Prediction> DoYoLo(Mat image)
        {
            float ratio = 0.0f;
            OpenCvSharp.Point diff1 = new OpenCvSharp.Point();
            OpenCvSharp.Point diff2 = new OpenCvSharp.Point();
            //var letter_image = YoloDetector.CreateLetterbox(image, new OpenCvSharp.Size(640, 384), new Scalar(114, 114, 114), out ratio, out diff1, out diff2);
            return detector.objectDetection(image);
        }

        private void playCam()
        {
            detector = new YoloDetector("best_yolov5.onnx");
            VideoCapture video = new VideoCapture();
            //video.Open("rtsp://admin:Dainlab2306@169.254.9.51:554/H.264/media.smp");
            video.Open(CamUri);
            //VideoWriter vWriter = null;
            //bool isFirstFrame = true;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Mat matImage = null;
            bool isFirst = true;

            using (Mat image = new Mat())
            {
                while (doPlay)
                {
                    if (!video.Read(image))
                    {
                        Cv2.WaitKey();
                    }
                    if (!image.Empty())
                    {
                        if (isFirst)
                        {
                            scaleDnW = (double)pbScreen.Width / (double)image.Width;
                            scaleDnH = (double)pbScreen.Height / (double)image.Height;
                            scaleUpW = (double)image.Width / (double)pbScreen.Width;
                            scaleUpH = (double)image.Height / (double)pbScreen.Height;
                            isFirst = false;
                        }

                        string dt = DateTime.Now.ToString(@"yyyy\/MM\/dd HH:mm:ss.fff");
                        //string fdt = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        drawAnyOnTheMat(image, dt);
                        lock (flameListLock)
                        {
                            if(flameList.Count() > 0)
                            {
                                matImage = drawFire(image);
                            }
                            else matImage = image;
                        }

                        Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(matImage);
                        System.Drawing.Size resize = new System.Drawing.Size(pbScreen.Width, pbScreen.Height);
                        bmp = new Bitmap(bmp, resize);
                        pbScreen.Image = bmp;

                        if (stopwatch.ElapsedMilliseconds > 1000 * fireCheckDuration)
                        {
                            stopwatch.Stop();
                            //Logger.Logger.WriteLog(out message, LogType.Info, "[YOLO] Start image analysis", true);
                            //AddLogMessage(message);

                            Thread t;

                            switch (detectorState)
                            {
                                case DetectorState.NO_FIRE:
                                    t = new Thread(() => {
                                        checkFlame(matImage.Clone());
                                    });
                                    t.Start();
                                    break;

                                case DetectorState.DETECT_FLAME:
                                    t = new Thread(() => {
                                        watchFlame(matImage.Clone());
                                    });
                                    t.Start();
                                    break;
                                
                                case DetectorState.MONITORING_FIRE:
                                    t = new Thread(() => {
                                        monitorFlame(matImage.Clone());
                                    });
                                    t.Start();
                                    break;
                                
                                case DetectorState.ALERT:
                                    t = new Thread(() => {
                                        alertFlame(matImage.Clone());
                                    });
                                    t.Start();
                                    break;
                            }
                            
                            
                            
                            stopwatch.Reset();
                            stopwatch.Start();
                        }
                    }
                    //if (Cv2.WaitKey(1) >= 0) break;
                }
                //if (vWriter != null) vWriter.Release();
            }
            video = null;
        }

        class FlameZone
        {
            public Rectangle zone { get; set; }
            public double DiagonalLength { get; set; }
            public FlameZone(Rectangle zone)
            {
                this.zone = zone;
                DiagonalLength = Math.Sqrt(Math.Pow(this.zone.Width, 2) + Math.Pow(this.zone.Height, 2));
            }

            public bool IsSameFlame(Rectangle rec)
            {
                return zone.IntersectsWith(rec);
            }
        }

        private Mat drawFire(Mat image)
        {
            Bitmap background = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
            Graphics bg = Graphics.FromImage(background);
            
            Random random = new Random();
            Image flameImage = flame[random.Next(0, 4)];
            //Image flameImage = flame[2];
            lock (flameListLock)
            {
                foreach(System.Drawing.Point p in flameList)
                {
                    bg.DrawImage(flameImage, p.X - (flameImage.Width / 2), p.Y - (flameImage.Height / 2), flameImage.Width, flameImage.Height);
                }
            }
            return OpenCvSharp.Extensions.BitmapConverter.ToMat(background);
        }

        private void drawAnyOnTheMat(Mat frame, string dt)
        {
            //Cv2.Line(frame, 10, 10, 630, 10, Scalar.Red, 10, LineTypes.AntiAlias);
            //Cv2.Line(frame, new OpenCvSharp.Point(10, 30), new OpenCvSharp.Point(630, 30), Scalar.Orange, 10, LineTypes.AntiAlias);
            //Cv2.Circle(frame, 30, 70, 20, Scalar.Yellow, 10, LineTypes.AntiAlias);
            //Cv2.Circle(frame, new OpenCvSharp.Point(90, 70), 25, Scalar.Green, -1, LineTypes.AntiAlias);
            //Cv2.Ellipse(frame, new RotatedRect(new Point2f(290, 70), new Size2f(75, 45), 0), Scalar.BlueViolet, 10, LineTypes.AntiAlias);
            //Cv2.Ellipse(frame, new OpenCvSharp.Point(10, 150), new OpenCvSharp.Size(50, 50), -90, 0, 100, Scalar.Tomato, -1, LineTypes.AntiAlias);
            Scalar s = new Scalar(255, 255, 255, 0.5);
            Cv2.Rectangle(frame, new Rect(10, 10, 455, 40), s, -1, LineTypes.AntiAlias);
            //Cv2.Rectangle(frame, new OpenCvSharp.Point(185, 45), new OpenCvSharp.Point(235, 95), Scalar.Navy, -1, LineTypes.AntiAlias);
            Cv2.PutText(frame, dt, new OpenCvSharp.Point(20, 40), HersheyFonts.HersheyComplex, 1, Scalar.Blue, 2, LineTypes.AntiAlias);

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("종료합니까?", "App 종료", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                LogMessage msg;
                Logger.Logger.WriteLog(out msg, LogType.Info, "종료합니다.", true);
                AddLogMessage(msg);
                doPlay = false;
                //this.timer1.Stop();
                Dispose(true);
                //if (client != null) client.Close();
                Environment.Exit(0);
            }
            else
            {
                e.Cancel = true;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            //timer1.Enabled = false;
            if (CamThread != null || CamThread.IsAlive)
            {
                CamThread.Join(1000);
            }
        }

        private void initApp()
        {
            LogMessage msg;
            ini = new IniFile("./config.ini");
            
            string tmp;
            APP_NAME = ini.Read("NAME", "MAIN");
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("NAME: {0}", APP_NAME), true);
            AddLogMessage(msg);
            this.Text = string.Format("{0} - {1}", APP_NAME, VER);

            CamUri = ini.Read("CAM_URI", "MAIN");
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("CAM_URI: {0}", CamUri), true);
            AddLogMessage(msg);

            targetFirePath = ini.Read("PATH_FIRE_IMAGE", "MAIN");
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("PATH_FIRE_IMAGE: {0}", targetFirePath), true);
            AddLogMessage(msg);
            Console.WriteLine("[INI] fire path: {0}", targetFirePath);
            makeFolders(targetFirePath);

            tmp = ini.Read("DURATION_FIRE_CHECK(SEC)", "MAIN");
            if (!int.TryParse(tmp, out fireCheckDuration))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "DURATION_FIRE_CHECK(SEC) Not a Number", true);
                AddLogMessage(msg);
            }
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("DURATION_FIRE_CHECK(SEC): {0}", fireCheckDuration), true);
            AddLogMessage(msg);

            flame[0] = Bitmap.FromFile("./flame02.png");
            flame[1] = Bitmap.FromFile("./flame03.png");
            flame[2] = Bitmap.FromFile("./flame11.png");
            flame[3] = Bitmap.FromFile("./flame12.png");

            doPlay = true;
            //bMin = DateTime.Now.Minute;
            CamThread = new Thread(playCam)
            {
                Name = "Camera Thread"
            };
            CamThread.Start();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            pbResult.Update();
        }

        private void makeFolders(string path)
        {
            DirectoryInfo info = new DirectoryInfo(path);
            if (!info.Exists) info.Create();
            //info = new DirectoryInfo(targetRecordPath);
            //if (!info.Exists) info.Create();
        }

        public void AddLogMessage(LogMessage message)
        {
            if (message == null) return;
            if (this.lbLog.Items.Count > 299)
            {
                lbLog.Items.RemoveAt(299);
            }
            lbLog.Items.Insert(0, message.logmsg);
        }

        private void pbScreen_MouseClick(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                lock (flameListLock)
                {
                    flameList.Add(new System.Drawing.Point((int)(e.X * scaleUpW), (int)(e.Y * scaleUpH)));
                }
            }
            else
            {
                lock (flameListLock)
                {
                    if(flameList.Count > 0) flameList.RemoveAt(0);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            lock (flameListLock)
            {
                flameList.Clear();
            }
            //Logger.Logger.WriteLog(out message, LogType.Info, string.Format("{0}", "Clear flame"), true);
            //AddLogMessage(message);
            //resetState();
        }

        private void pbResult_Paint(object sender, PaintEventArgs e)
        {
            string text = "";
            Font fnt = new Font("Arial", 20, FontStyle.Bold);
            SizeF stringSize;
            Rectangle rect;

            switch (detectorState)
            {
                case DetectorState.NO_FIRE:
                    text = "화염 없음";
                    stringSize = e.Graphics.MeasureString(text, fnt);
                    rect = new Rectangle(0, 0, pbResult.Width, pbResult.Height);
                    e.Graphics.FillRectangle(new SolidBrush(Color.MediumAquamarine), rect);
                    e.Graphics.DrawString(text, fnt, new SolidBrush(Color.ForestGreen), (pbScreen.Width - stringSize.Width) / 2, (pbScreen.Height - stringSize.Height) / 2);
                    break;
                case DetectorState.DETECT_FLAME:
                    text = "화염 발견";
                    stringSize = e.Graphics.MeasureString(text, fnt);
                    e.Graphics.DrawString(text, fnt, new SolidBrush(Color.IndianRed), (pbScreen.Width - stringSize.Width) / 2, (pbScreen.Height - stringSize.Height) / 2);
                    break;
                case DetectorState.MONITORING_FIRE:
                    text = "화재 발생";
                    stringSize = e.Graphics.MeasureString(text, fnt);
                    e.Graphics.DrawString(text, fnt, new SolidBrush(Color.Red), (pbScreen.Width - stringSize.Width) / 2, (pbScreen.Height - stringSize.Height) / 2);
                    break;
                case DetectorState.ALERT:
                    text = "화재 진행 중...";
                    stringSize = e.Graphics.MeasureString(text, fnt);
                    e.Graphics.DrawString(text, fnt, new SolidBrush(Color.DarkRed), (pbScreen.Width - stringSize.Width) / 2, (pbScreen.Height - stringSize.Height) / 2);
                    break;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Logger.Logger.WriteLog(out message, LogType.Info, string.Format("{0}", "Acknowledge fire"), true);
            AddLogMessage(message);
            resetState();
        }
    }
}
