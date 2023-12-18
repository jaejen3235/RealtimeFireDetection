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
        Image[] flameIcons = new Image[8];
        List<System.Drawing.Point> virtualFlameList = new List<System.Drawing.Point>();
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
        private int NO_FIRE_WAIT_TIME = 10;
        public static double STANDARD_DEVIATION_LOW_LIMIT = 1.0;
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

        private Bitmap saveFlameInfo(Mat image, List<Prediction> result, bool save = false)
        {
            string strNow = DateTime.Now.ToString("yyyyMMddHHmmss");
            StreamWriter writer;
            string level = "";

            string[] ss;
            Rectangle rec;
            bool isNewFlameInfo;
            int n = 1;

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
                Cv2.Rectangle(image, new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin), new OpenCvSharp.Point(obj.Box.Xmax, obj.Box.Ymax), new Scalar(0, 0, 255), 1);
                //Cv2.PutText(image, obj.Label + " " + obj.Confidence.ToString("F2"), new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin - 5), HersheyFonts.HersheySimplex, 1, new Scalar(0, 0, 255), 2);
                Cv2.PutText(image, string.Format("L{0:D2}:{1}", n++, obj.Confidence.ToString("F2")), new OpenCvSharp.Point(obj.Box.Xmin, obj.Box.Ymin - 5), HersheyFonts.HersheySimplex, 0.4, new Scalar(0, 0, 255), 1);
                //writer.WriteLine(obj.Box.Xmin + ", " + obj.Box.Xmax + "," + obj.Box.Ymin + "," + obj.Box.Ymax + "," + obj.Confidence.ToString("F2"));
                string tmp = Math.Floor(obj.Box.Xmin + (obj.Box.Xmax - obj.Box.Xmin) * 0.5) + "," + // 중심점 x 좌표
                    Math.Floor(obj.Box.Ymin + (obj.Box.Ymax - obj.Box.Ymin) * 0.5) + "," + // 중심점 y 좌표
                    Math.Floor((obj.Box.Xmax - obj.Box.Xmin)) + "," +                      // 영역 가로 길이
                    Math.Floor((obj.Box.Ymax - obj.Box.Ymin)) + "," +                      // 영역 세로 길이
                    obj.Confidence.ToString("F2");
                sb.Append(tmp).Append("/");


                rec = new Rectangle();
                rec.X = (int)Math.Floor(obj.Box.Xmin);
                rec.Y = (int)Math.Floor(obj.Box.Ymin);
                rec.Width = (int)Math.Floor(obj.Box.Xmax - obj.Box.Xmin);
                rec.Height = (int)Math.Floor(obj.Box.Ymax - obj.Box.Ymin);
                isNewFlameInfo = true;

                FlameInfo info = new FlameInfo();
                info.Area = rec;
                info.Confidence = double.Parse(obj.Confidence.ToString("F2"));
                ///////////////////////////////////////////////////////////////////////////
                ///화염 판단
                if (FlameList.Count == 0)
                {
                    Flame flame = new Flame(strNow);
                    flame.AddFlameInfo(info);
                    FlameList.Add(flame);
                    continue;
                }

                foreach (Flame flame in FlameList)
                {
                    if (flame.AddFlameInfo(info))
                    {
                        Logger.Logger.WriteLog(out message, LogType.Info, "Flame ID " + flame.ID + ", Add info [" + flame.ToString() + "]", false);
                        isNewFlameInfo = false;
                        break;
                    }
                }

                if (isNewFlameInfo)
                {
                    Flame flame = new Flame(strNow);
                    flame.AddFlameInfo(info);
                    FlameList.Add(flame);
                    Logger.Logger.WriteLog(out message, LogType.Info, "Flame ID " + flame.ID + ", Add new & info [" + flame.ToString() + "]", false);
                }
            }//foreach (var obj in result)

            //Cv2.NamedWindow("RESULT", WindowFlags.AutoSize);
            //Cv2.ImShow("RESULT", dispImage);

            Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
            Graphics g = Graphics.FromImage(bmp);
            string[] tmps = sb.ToString().Trim().Split('/');

            foreach (Flame flame in FlameList)
            {
                flame.IsItFire(() => {
                    Console.WriteLine("Invoked flame.IsItFire");
                    save = true;
                });
                if(save) break;
            }
            
            if (save)
            {
                writer = File.CreateText(targetFirePath + "/" + strNow + "_" + APP_NAME + "_" + level + "_result" + ".txt");
                Font fnt = new Font("Arial", 8, FontStyle.Regular);
                int cnt = 1;
                int x = 0, y = 0, w = 0, h = 0;
                foreach (string s in tmps)
                {
                    if (s == null || s.Length == 0) break;
                    writer.WriteLine(s);
                    ss = s.Trim().Split(',');
                    string sw = string.Format("L{0:D2} X:{1} Y:{2} W:{3} H:{4} C:{5:0.00}", cnt, ss[0], ss[1], ss[2], ss[3], ss[4]);
                    g.DrawString(sw, fnt, new SolidBrush(Color.Black), 12, cnt * 10 + 15 + 2);
                    g.DrawString(sw, fnt, new SolidBrush(Color.Yellow), 10, cnt * 10 + 15);

                    int.TryParse(ss[0], out x);
                    int.TryParse(ss[1], out y);
                    int.TryParse(ss[2], out w);
                    int.TryParse(ss[3], out h);
                    x -= 10; y -= 10;
                    w = 20; h = 20;

                    if (x < 0) x = 0;
                    if (y < 0) y = 0;
                    if (x + w > bmp.Width) w = bmp.Width - (x + w);
                    if (y + h > bmp.Height) h = bmp.Height - (y + h);



                    if (w > 0 && h > 0)
                    {
                        //영역을 벗어나지 않도록 Crop
                        Bitmap croppedBitmap = bmp.Clone(new Rectangle(x, y, w, h), System.Drawing.Imaging.PixelFormat.DontCare);
                        makeRColor(ref croppedBitmap);
                        int red = 0, green = 0, blue = 0;
                        makeRGB_Average(croppedBitmap, ref red, ref green, ref blue);
                        croppedBitmap.Save(targetFirePath + "/" + strNow + "_" + APP_NAME + "_" + level + "_" + string.Format("L{0:D2}_{1},{2},{3}", cnt, red, green, blue) +"_cropped.bmp", ImageFormat.Bmp);
                    }
                    cnt++;
                }
                writer.Close();
                bmp.Save(targetFirePath + "/" + strNow + "_" + APP_NAME + "_" + level + "_snapshot.bmp", ImageFormat.Bmp);
            }
            return bmp;
        }

        private void resetState()
        {
            pbResult.Image = null;
            pbResult.Invalidate();
            detectorState = DetectorState.NO_FIRE;
            countWatchFlame = 0;
            Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "Change state to NO_FIRE"), true);
            AddLogMessage(message);
            FlameList.Clear();
        }

        private List<Prediction> DoYoLo(Mat image)
        {
            float ratio = 0.0f;
            OpenCvSharp.Point diff1 = new OpenCvSharp.Point();
            OpenCvSharp.Point diff2 = new OpenCvSharp.Point();
            //var letter_image = YoloDetector.CreateLetterbox(image, new OpenCvSharp.Size(640, 384), new Scalar(114, 114, 114), out ratio, out diff1, out diff2);
            return detector.objectDetection(image);
        }

        private void RunFlameMonitor()
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
            int flameNo = 0;
            Stopwatch noFireWatch = new Stopwatch();
            noFireWatch.Stop();
            int matImageFailCount = 0;
            System.Drawing.Size resize = new System.Drawing.Size(pbScreen.Width, pbScreen.Height);

            using (Mat image = new Mat())
            {
                while (doPlay)
                {
                    if (!video.Read(image))
                    {
                        Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " Cv2.WaitKey()");
                        Cv2.WaitKey(1000);
                        video.Release();
                        Thread.Sleep(1000);
                        if (video.Open(CamUri))
                        {
                            matImageFailCount = 0;
                        }
                        else
                        {
                            matImageFailCount++;
                            if (matImageFailCount > 10)
                            {
                                matImageFailCount = 0;
                                video.Dispose();
                                Thread.Sleep(1000);
                                video = new VideoCapture();
                                Thread.Sleep(1000);
                                video.Open(CamUri);
                                Thread.Sleep(1000);
                            }
                        }
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
                        drawDateTimeOnTheMat(image, dt);
                        lock (flameListLock)
                        {
                            if (virtualFlameList.Count() > 0)
                            {
                                matImage = drawFlames(image);
                            }
                            else matImage = image;
                        }

                        Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(matImage);
                        //bmp = new Bitmap(bmp, resize);
                        pbScreen.Image = bmp;
                        
                        if (stopwatch.ElapsedMilliseconds > 1000 * fireCheckDuration)
                        {
                            stopwatch.Stop();

                            Thread t = new Thread(() => {
                                using(Mat yoloImage = matImage.Clone())
                                {
                                    var result = DoYoLo(yoloImage);
                                    if (result.Count > 0)
                                    {
                                        Bitmap bmpResult = saveFlameInfo(yoloImage, result);
                                        //bmp = new Bitmap(bmpResult, resize);
                                        pbResult.Image = bmp;
                                        detectorState = DetectorState.DETECT_FLAME;
                                        pbResult.Update();
                                        noFireWatch.Reset();
                                        if (!noFireWatch.IsRunning) noFireWatch.Start();
                                    }
                                    else
                                    {
                                        if (noFireWatch.ElapsedMilliseconds > 1000 * NO_FIRE_WAIT_TIME)
                                        {
                                            noFireWatch.Stop();
                                            noFireWatch.Reset();
                                            resetState();
                                        }
                                    }
                                }
                            });
                            t.Start();

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

        class FlameInfo
        {
            public Rectangle Area { get; set; }
            public double Confidence { get; set; }
            public double DiagonalLength { get; set; }

            public double StandardDeviation { get; set; }

            override
            public string ToString()
            {
                return string.Format("Location X:{0}  Y:{1}  W:{2}  H:{3}  Confidence:{4:0.00}  DiagonalLength:{5:0.00}  StandardDeviation:{6:0.00}", Area.X, Area.Y, Area.Width, Area.Height, Confidence, DiagonalLength, StandardDeviation);
            }
        }

        List<Flame> FlameList = new List<Flame>();

        class Flame
        {
            public DetectorState state = DetectorState.NO_FIRE;
            public string ID { get; set; }
            public List<FlameInfo> FlameInfoList = new List<FlameInfo>();
            
            public Flame(string id)
            {
                this.ID = id;
                state = DetectorState.NO_FIRE;
            }

            public bool AddFlameInfo(FlameInfo info)
            {
                if(FlameInfoList.Count == 0)
                {
                    info.DiagonalLength = GetDiagonalLength(info.Area.Width, info.Area.Height);
                    FlameInfoList.Add(info);
                    //LogMessage msg;
                    //Logger.Logger.WriteLog(out msg, LogType.Info, No + " - " + "Add flame: " + info.ToString(), false);
                    
                    return true;
                }
                if (IsSameFlame(info.Area))
                {
                    info.DiagonalLength = GetDiagonalLength(info.Area.Width, info.Area.Height);
                    FlameInfoList.Add(info);

                    if (FlameInfoList.Count >= 11) FlameInfoList.RemoveAt(0);
                    if (FlameInfoList.Count > 0)
                    {
                        info.StandardDeviation = GetStandardDeviation();
                        //LogMessage msg;
                        //Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("NO {0} - Add flame count:{1} / SD {2} / info {3}", No , FlameInfoList.Count, info.StandardDeviation, info.ToString()), false);
                    }
                    return true;
                }
                return false;
            }

            private double GetStandardDeviation()
            {
                double average;
                double sum = 0;
                double sumOfDerivation = 0;
                foreach (FlameInfo info in FlameInfoList)
                {
                    sum += info.DiagonalLength;
                    sumOfDerivation += (info.DiagonalLength) * (info.DiagonalLength);
                }
                average = sum / FlameInfoList.Count;
                double sumOfDerivationAverage = sumOfDerivation / FlameInfoList.Count;
                return Math.Sqrt(sumOfDerivationAverage - (average * average));
            }

            private bool IsSameFlame(Rectangle rec)
            {
                Rectangle area = FlameInfoList.ElementAt(FlameInfoList.Count -1).Area;
                return area.IntersectsWith(rec);
            }

            public double GetDiagonalLength(int w, int h)
            {
                return Math.Sqrt(Math.Pow(w, 2) + Math.Pow(h, 2));
            }

            public void IsItFire(Action action)
            {
                double sum = 0;
                double avg = 0;
                bool isAll = true;
                for(int i=1;i<FlameInfoList.Count-1;i++)
                {
                    //검색된 영역의 모든 대각선이 지정된 값 이상인지 확인
                    //sum += FlameInfoList.ElementAt(i).StandardDeviation;
                    if (FlameInfoList.ElementAt(i).StandardDeviation <= STANDARD_DEVIATION_LOW_LIMIT)
                    {
                        isAll = false;
                        break;
                    }
                }
                //avg = sum / (FlameInfoList.Count - 1);

                //if (avg > STANDARD_DEVIATION_LOW_LIMIT)//화재
                if (isAll)//화재
                {
                    if (state == DetectorState.NO_FIRE) action();
                    state = DetectorState.DETECT_FLAME;
                }
                else
                {
                    if (state != DetectorState.NO_FIRE)
                    {
                        //action();
                    }
                    state = DetectorState.NO_FIRE;
                }
            }

            override
            public string ToString()
            {
                LogMessage msg;
                StringBuilder sb = new StringBuilder();
                sb.Append("\r\n");
                foreach (FlameInfo info in FlameInfoList)
                {
                    sb.Append(info.ToString()).Append("\r\n");
                }
                return sb.ToString();
            }
        }

        class Polygon
        {
            private List<PointF> mPointList = new List<PointF>();

            public Polygon()
            {
                mPointList.Add(new PointF(200, 100));
                mPointList.Add(new PointF(200, 200));
                mPointList.Add(new PointF(100, 200));
                mPointList.Add(new PointF(100, 100));
                mPointList.Add(new PointF(200, 100));
            }

            public void addPoint(float x, float y)
            {
                mPointList.Add(new PointF(x, y));
            }

            public bool isPointInPolygon(float x, float y)
            {
                int size = mPointList.Count;

                // 점이 3개 이하로 이루어진 polygon은 없다.
                if (size < 3)
                {
                    return false;
                }

                bool isInnerPoint = false;

                // Point in polygon algorithm
                for (int cur = 0; cur < size - 1; cur++)
                {
                    PointF curPoint = mPointList.ElementAt(cur);
                    PointF prevPoint = mPointList.ElementAt(cur + 1);
                    /*
                     * y - y1 = M * (x - x1)
                     * M = (y2 - y1) / (x2 - x1)
                     */
                    if (curPoint.Y < y && prevPoint.Y >= y || prevPoint.Y < y && curPoint.Y >= y)
                    {
                        if (curPoint.X + (y - curPoint.Y) / (prevPoint.Y - curPoint.Y) * (prevPoint.X - curPoint.X) < x)
                        {
                            isInnerPoint = !isInnerPoint;
                        }
                    }
                }
                return isInnerPoint;
            }

            private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
            {
                //   0 To  mouse.X ;
                PointF orgin = new PointF(0, 0);
                int cnt = 0;
                PointF mDown = new PointF(Convert.ToSingle(e.X), Convert.ToSingle(e.Y));
                for (int i = 0; i < mPointList.Count - 1; i++)
                    GetIntersectPoint(orgin, mDown, mPointList.ElementAt(i), mPointList.ElementAt(i+1), ref cnt);
                if (cnt % 2 == 1) Console.WriteLine("내부");
                else Console.WriteLine("외부");
            }

            bool GetIntersectPoint(PointF AP1, PointF AP2, PointF BP1, PointF BP2, ref int Cnt)
            {
                float t;
                float s;
                float under = (BP2.Y - BP1.Y) * (AP2.X - AP1.X) - (BP2.X - BP1.X) * (AP2.Y - AP1.Y);
                if (under == 0)
                {
                    //   Cnt = Cnt;
                    return false;
                }
                float _t = (BP2.X - BP1.X) * (AP1.Y - BP1.Y) - (BP2.Y - BP1.Y) * (AP1.X - BP1.X);
                float _s = (AP2.X - AP1.X) * (AP1.Y - BP1.Y) - (AP2.Y - AP1.Y) * (AP1.X - BP1.X);

                t = _t / under;
                s = _s / under;
                if (t < 0.0 || t > 1.0 || s < 0.0 || s > 1.0)
                {
                    //    Cnt = Cnt;
                    return false;
                }
                if (_t == 0 && _s == 0)
                {
                    //   Cnt = Cnt;
                    return false;
                }
                Cnt++;

                return true;
            }
        }

        private Mat drawFlames(Mat image)
        {
            Bitmap background = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
            Graphics bg = Graphics.FromImage(background);
            
            Random random = new Random();
            Image flameImage;
            //Image flameImage = flame[2];
            lock (flameListLock)
            {
                foreach(System.Drawing.Point p in virtualFlameList)
                {
                    flameImage = flameIcons[random.Next(0, 4)];
                    bg.DrawImage(flameImage, p.X - (flameImage.Width / 2), p.Y - (flameImage.Height / 2), flameImage.Width, flameImage.Height);
                }
            }
            return OpenCvSharp.Extensions.BitmapConverter.ToMat(background);
        }

        private void drawDateTimeOnTheMat(Mat frame, string dt)
        {
            //Cv2.Line(frame, 10, 10, 630, 10, Scalar.Red, 10, LineTypes.AntiAlias);
            //Cv2.Line(frame, new OpenCvSharp.Point(10, 30), new OpenCvSharp.Point(630, 30), Scalar.Orange, 10, LineTypes.AntiAlias);
            //Cv2.Circle(frame, 30, 70, 20, Scalar.Yellow, 10, LineTypes.AntiAlias);
            //Cv2.Circle(frame, new OpenCvSharp.Point(90, 70), 25, Scalar.Green, -1, LineTypes.AntiAlias);
            //Cv2.Ellipse(frame, new RotatedRect(new Point2f(290, 70), new Size2f(75, 45), 0), Scalar.BlueViolet, 10, LineTypes.AntiAlias);
            //Cv2.Ellipse(frame, new OpenCvSharp.Point(10, 150), new OpenCvSharp.Size(50, 50), -90, 0, 100, Scalar.Tomato, -1, LineTypes.AntiAlias);
            Scalar s = new Scalar(255, 255, 255, 0.5);
            Cv2.Rectangle(frame, new Rect(10, 10, (int)(175 * scaleUpW), (int)(14 * scaleUpH)), s, -1, LineTypes.AntiAlias);
            //Cv2.Rectangle(frame, new OpenCvSharp.Point(185, 45), new OpenCvSharp.Point(235, 95), Scalar.Navy, -1, LineTypes.AntiAlias);
            Cv2.PutText(frame, dt, new OpenCvSharp.Point(10, 20), HersheyFonts.HersheySimplex, 0.4, Scalar.Blue, 0, LineTypes.AntiAlias);
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

            tmp = ini.Read("STANDARD_DEVIATION_LOW_LIMIT", "MAIN");
            if (!double.TryParse(tmp, out STANDARD_DEVIATION_LOW_LIMIT))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "STANDARD_DEVIATION_LOW_LIMIT Not a Number", true);
                AddLogMessage(msg);
            }
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("STANDARD_DEVIATION_LOW_LIMIT: {0}", STANDARD_DEVIATION_LOW_LIMIT), true);
            AddLogMessage(msg);

            tmp = ini.Read("NO_FIRE_WAIT_TIME(SEC)", "MAIN");
            if (!int.TryParse(tmp, out NO_FIRE_WAIT_TIME))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "NO_FIRE_WAIT_TIME(SEC) Not a Number", true);
                AddLogMessage(msg);
            }
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("NO_FIRE_WAIT_TIME(SEC): {0}", NO_FIRE_WAIT_TIME), true);
            AddLogMessage(msg);


            flameIcons[0] = Bitmap.FromFile("./f01.png");
            flameIcons[1] = Bitmap.FromFile("./f02.png");
            flameIcons[2] = Bitmap.FromFile("./f03.png");
            flameIcons[3] = Bitmap.FromFile("./f04.png");
            flameIcons[4] = Bitmap.FromFile("./f05.png");
            flameIcons[5] = Bitmap.FromFile("./f06.png");
            flameIcons[6] = Bitmap.FromFile("./f07.png");
            flameIcons[7] = Bitmap.FromFile("./f08.png");

            doPlay = true;
            //bMin = DateTime.Now.Minute;
            //CamThread = new Thread(playCam)
            CamThread = new Thread(RunFlameMonitor)
            {
                Name = "Camera Thread"
            };
            CamThread.Start();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            pbResult.Update();
            pbCanvas.Parent = pbResult;
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
            if (!cbVitualFlame.Checked) return;

            if (e.Button == MouseButtons.Left)
            {
                lock (flameListLock)
                {
                    virtualFlameList.Add(new System.Drawing.Point((int)(e.X * scaleUpW), (int)(e.Y * scaleUpH)));
                }
            }
            else
            {
                lock (flameListLock)
                {
                    if(virtualFlameList.Count > 0) virtualFlameList.RemoveAt(0);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            lock (flameListLock)
            {
                virtualFlameList.Clear();
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

        private void cbVitualFlame_CheckedChanged(object sender, EventArgs e)
        {
            //cbVitualFlame.Checked;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////
        private void makeBinary(Bitmap tmp)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;

            //총 사이즈만큼 반복을 하면서 하나하나의 픽셀을 변경한다.
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    BinaryConvert(ref colorData);
                    tmp.SetPixel(i, j, colorData);
                }
            }
        }
        private void BinaryConvert(ref Color src)
        {
            //382란 수치는 (255*3)/2 이다. 평균보다 어두우면 검정으로 바꿈.
            if ((src.R + src.G + src.B) < 382)
            {
                src = Color.FromArgb(0, 0, 0);
            }
            else
            {
                src = Color.FromArgb(255, 255, 255);
            }
        }

        private void makeRGB_Average(Bitmap tmp, ref int Red, ref int Green, ref int Blue)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;
            double r = 0, g = 0, b = 0;
            double pCnt = 0;
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    if (colorData.R <= 0x22 && colorData.G <= 0x22 && colorData.B <= 0x22) continue; //검정색 계열 제외
                    if (colorData.R >= 0xDB && colorData.G >= 0xDB && colorData.B >= 0xDB) continue; //흰색 계열 제외
                    r += colorData.R;
                    g += colorData.G;
                    b += colorData.B;
                    pCnt++;
                }
            }
            Red = (int)(r / pCnt);
            Green = (int)(g / pCnt);
            Blue = (int)(b / pCnt);
        }

        private void makeRColor(ref Bitmap tmp)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    RColorConvert(ref colorData);
                    tmp.SetPixel(i, j, colorData);
                }
            }
        }
        private void RColorConvert(ref Color src)
        {
            if ((src.R < src.G) || (src.R < src.B)) //레드 성분이 상대적으로 적다면 회색화
            {
                int res = (src.R + src.G + src.B) / 3;
                src = Color.FromArgb(res, res, res);
            }
        }
        private void makeGColor(Bitmap tmp)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    GColorConvert(ref colorData);
                    tmp.SetPixel(i, j, colorData);
                }
            }
        }
        private void GColorConvert(ref Color src)
        {
            if ((src.G < src.R) || (src.G < src.B)) //그린 성분이 상대적으로 적다면 회색화
            {
                int res = (src.R + src.G + src.B) / 3;
                src = Color.FromArgb(res, res, res);
            }
        }
        private void makeBColor(Bitmap tmp)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    BColorConvert(ref colorData);
                    tmp.SetPixel(i, j, colorData);
                }
            }
        }
        private void BColorConvert(ref Color src)
        {
            if ((src.B < src.R) || (src.B < src.G)) //블루 성분이 상대적으로 적다면 회색화
            {
                int res = (src.R + src.G + src.B) / 3;
                src = Color.FromArgb(res, res, res);
            }
        }
        private void makeGray(Bitmap tmp)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    GaryConvert(ref colorData);
                    tmp.SetPixel(i, j, colorData);
                }
            }
        }
        private void GaryConvert(ref Color src)
        {
            int res = (src.R + src.G + src.B) / 3;
            src = Color.FromArgb(res, res, res); //3개의 값의 평균으로 색상을 조정한다.
            //RGB간 색상의 격차가 없으면 없을 수록 색감은 무채색을 띈다.
        }
        private void makeInvert(Bitmap tmp)
        {
            int width = tmp.Width;
            int height = tmp.Height;
            Color colorData;
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    colorData = tmp.GetPixel(i, j);
                    Invert(ref colorData);
                    tmp.SetPixel(i, j, colorData);
                }
            }
        }
        private void Invert(ref Color src)
        {
            //0xFF(255) 으로 XOR 연산하여 값을 반전 시킨다.
            int r = src.R ^ 255;
            int g = src.G ^ 255;
            int b = src.B ^ 255;
            src = Color.FromArgb(r, g, b);
        }

        private void pbCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            tbText.Text = string.Format("X:{0:D3}, Y:{1:D3}", e.X, e.Y);
        }
    }
}
