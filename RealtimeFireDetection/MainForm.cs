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
using Point = OpenCvSharp.Point;

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

        LogMessage message;

        public string targetRecordPath;
        public string targetCapturePath;
        public string targetFirePath;

        public string PathRoiList = "./list_of_ROI.cfg";
        public string PathNonRoiList = "./list_of_Non_ROI.cfg";

        private int fireCheckDuration = 10;
        private int NO_FIRE_WAIT_TIME = 10;
        public static double STANDARD_DEVIATION_LOW_LIMIT = 1.0;
        IniFile ini;
        string CamUri;
        Queue<Mat> matQueue = new Queue<Mat>();
        DateTime receivedEventTime = DateTime.Now;

        double scaleDnW;
        double scaleDnH;
        double scaleUpW;
        double scaleUpH;

        List<Point> Roi = new List<Point>();
        List<List<Point>> RoiList = new List<List<Point>>();
        List<Point> NonRoi = new List<Point>();
        List<List<Point>> NonRoiList = new List<List<Point>>();
        Polygon polygon = new Polygon();



        public Dictionary<string, List<Point>> DicRoiList
        {
            get;
        }
        public Dictionary<string, List<Point>> DicNonRoiList
        {
            get;
        }


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

            DicRoiList = new Dictionary<string, List<Point>>();
            DicNonRoiList = new Dictionary<string, List<Point>>();

            initApp();
            loadRegions(PathRoiList, DicRoiList);
            loadRegions(PathNonRoiList, DicNonRoiList);
        }

        private void loadRegions(string path, Dictionary<string, List<Point>> dic)
        {
            StreamReader sr = null;

            try
            {
                if (!File.Exists(path)) return;
                sr = new StreamReader(path);

                string line = "";
                string key;
                string values;
                string[] tmps;
                dic.Clear();

                while ((line = sr.ReadLine()) != null)
                {
                    key = line.Substring(0, line.IndexOf(":"));
                    values = line.Substring(line.IndexOf(":") + 1);
                    tmps = values.Split(',');
                    List<Point> list = new List<Point>();
                    string[] xy;
                    StringBuilder sb = new StringBuilder();

                    foreach (string s in tmps)
                    {
                        xy = s.Split('-');
                        list.Add(new Point(int.Parse(xy[0]), int.Parse(xy[1])));
                        sb.Append(xy[0] + "-" + xy[1] + ",");
                    }
                    Console.WriteLine("LOAD Point Key: {0}, Value: {1}", key, sb.ToString());
                    dic.Add(key, list);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                if (sr != null) sr.Close();
            }
        }

        private Bitmap saveFlameInfo(Mat image, List<Prediction> result, bool save = false)
        {
            string strNow = DateTime.Now.ToString("yyyyMMdd_HHmmss");
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
                    g.DrawString(sw, fnt, new SolidBrush(Color.Black), 12, cnt * 10 + 45 + 2);
                    g.DrawString(sw, fnt, new SolidBrush(Color.Yellow), 10, cnt * 10 + 45);

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
            Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "Change state to NO_FIRE"), true);
            AddLogMessage(message);
            FlameList.Clear();
        }

        private List<Prediction> DoYoLo(Mat image)
        {
            //OpenCvSharp.Point diff1 = new OpenCvSharp.Point();
            //OpenCvSharp.Point diff2 = new OpenCvSharp.Point();
            //var letter_image = YoloDetector.CreateLetterbox(image, new OpenCvSharp.Size(640, 384), new Scalar(114, 114, 114), out ratio, out diff1, out diff2);
            return detector.objectDetection(image);
        }

        private bool checkROIin(List<Prediction> predictionList)
        {
            foreach (List<Point> list in RoiList)
            {
                foreach(Prediction prediction in predictionList)
                {
                    if (polygon.isInside(list,
                        prediction.Box.Xmin + (prediction.Box.Xmax - prediction.Box.Xmin) / 2,
                        prediction.Box.Ymin + (prediction.Box.Ymax - prediction.Box.Ymin) / 2
                        )){
                        return true;
                    }
                }
            }
            return false;
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
                            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " Retry to Open");
                            matImageFailCount = 0;
                            continue;
                        }
                        else
                        {
                            matImageFailCount++;
                            if (matImageFailCount > 10)
                            {
                                matImageFailCount = 0;
                                video.Dispose(); video = null;
                                Thread.Sleep(1000);
                                video = new VideoCapture();
                                Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "fail count more than 10,  new VideoCapture()");
                                Thread.Sleep(1000);
                                video.Open(CamUri);
                                Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "fail count more than 10,  Open Uri");
                                Thread.Sleep(1000);
                            }
                            continue;
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

                        lock (flameListLock)
                        {
                            if (virtualFlameList.Count() > 0)
                            {
                                matImage = drawFlames(image);
                            }
                            else matImage = image;
                        }

                        ///////////////////////////////////////////////////////////////////////////
                        ///관심영역
                        if(DicRoiList.Count > 0)
                        {
                            RoiList.Clear();
                            foreach (KeyValuePair<string, List<Point>> kv in DicRoiList)
                            {
                                RoiList.Add(kv.Value);
                            }
                            Cv2.Polylines(matImage, RoiList, true, Scalar.Magenta, 1, LineTypes.AntiAlias);
                        }
                        ///////////////////////////////////////////////////////////////////////////
                        ///비관심영역
                        if (DicNonRoiList.Count > 0)
                        {
                            NonRoiList.Clear();
                            foreach (KeyValuePair<string, List<Point>> kv in DicNonRoiList)
                            {
                                NonRoiList.Add(kv.Value);
                            }
                            Cv2.FillPoly(matImage, NonRoiList, Scalar.Black);
                        }
                        ///Time-stamp
                        string dt = DateTime.Now.ToString(@"yyyy\/MM\/dd HH:mm:ss.fff");
                        //string fdt = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        drawDateTimeOnTheMat(matImage, dt);

                        Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(matImage);
                        bmp = new Bitmap(bmp, resize);
                        pbScreen.Image = bmp;
                        
                        if (stopwatch.ElapsedMilliseconds > 1000 * fireCheckDuration)
                        {
                            stopwatch.Stop();

                            Thread t = new Thread(() => {
                                using(Mat yoloImage = matImage.Clone())
                                {
                                    var result = DoYoLo(yoloImage);
                                    if (result.Count > 0 && checkROIin(result))
                                    {
                                        Bitmap bmpResult = saveFlameInfo(yoloImage, result);
                                        bmp = new Bitmap(bmpResult, resize);
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

            public double Deviation { get; set; }

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
                    if (FlameInfoList.Count > 5)
                    {
                        GetSDAll();
                        //info.StandardDeviation = GetStandardDeviation();
                        //LogMessage msg;
                        //Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("NO {0} - Add flame count:{1} / SD {2} / info {3}", No , FlameInfoList.Count, info.StandardDeviation, info.ToString()), false);
                    }
                    return true;
                }
                return false;
            }

            private void GetSDAll()
            {
                double average;
                double sumOfDeviation = 0;
                var ret = FlameInfoList.Select(s => s.DiagonalLength);
                average = ret.Average();
                sumOfDeviation = FlameInfoList.Sum(info => Math.Pow(info.DiagonalLength - average, 2));
                //분산 = Math.Pow(편차(개별값-평균값))의 합/자료갯수
                //표준편차 = 분산의 제곱근
                double result = Math.Sqrt(sumOfDeviation / FlameInfoList.Count());
                FlameInfoList.Last().StandardDeviation = result;
            }

            private double GetStandardDeviation()
            {
                double average;
                double sum = 0;
                double sumOfDerivation = 0;
                foreach (FlameInfo info in FlameInfoList)
                {
                    sum += info.DiagonalLength;
                    sumOfDerivation += info.DiagonalLength * info.DiagonalLength;
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
                bool isAll = true;
                if (FlameInfoList.Count >= 5)
                {
                    for (int i = 1; i < FlameInfoList.Count - 1; i++)
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
                }
                else isAll = false;

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

            public bool isInside(List<Point> pList, float x, float y){
                //crosses는 점q와 오른쪽 반직선과 다각형과의 교점의 개수
                int crosses = 0;
                int size = pList.Count;
                // 점이 3개 미만으로 이루어진 polygon은 없다.
                if (size < 3)
                {
                    return false;
                }
                Point[] p = pList.ToArray();
                for(int i = 0; i < p.Length; i++)
                {
                    int j = (i + 1) % p.Length;
                    if((p[i].Y > y) != (p[j].Y > y))
                    {
                        double atx = (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X;
                        if (x < atx) crosses++;
                    }
                }
                return crosses % 2 > 0;
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
            //Cv2.Rectangle(frame, new OpenCvSharp.Point(185, 45), new OpenCvSharp.Point(235, 95), Scalar.Navy, -1, LineTypes.AntiAlias);

            //Scalar s = new Scalar(255, 255, 255, 0.5);
            //Cv2.Rectangle(frame, new Rect(10, 10, (int)(175 * scaleUpW), (int)(14 * scaleUpH)), s, -1, LineTypes.AntiAlias);
            string[] tmps = dt.Split(' ');
            Cv2.PutText(frame, tmps[0], new OpenCvSharp.Point(12, 22), HersheyFonts.HersheySimplex, 0.4, Scalar.Black, 0, LineTypes.AntiAlias);
            Cv2.PutText(frame, tmps[0], new OpenCvSharp.Point(10, 20), HersheyFonts.HersheySimplex, 0.4, Scalar.White, 0, LineTypes.AntiAlias);

            Cv2.PutText(frame, tmps[1], new OpenCvSharp.Point(12, 42), HersheyFonts.HersheySimplex, 0.4, Scalar.Black, 0, LineTypes.AntiAlias);
            Cv2.PutText(frame, tmps[1], new OpenCvSharp.Point(10, 40), HersheyFonts.HersheySimplex, 0.4, Scalar.White, 0, LineTypes.AntiAlias);
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

            //Roi.Add(new OpenCvSharp.Point(63, 339));
            //Roi.Add(new OpenCvSharp.Point(61, 300));
            //Roi.Add(new OpenCvSharp.Point(106, 270));
            //Roi.Add(new OpenCvSharp.Point(106, 234));
            //Roi.Add(new OpenCvSharp.Point(72, 142));
            //Roi.Add(new OpenCvSharp.Point(106, 115));
            //Roi.Add(new OpenCvSharp.Point(100, 66));
            //Roi.Add(new OpenCvSharp.Point(122, 46));
            //Roi.Add(new OpenCvSharp.Point(122, 28));
            //Roi.Add(new OpenCvSharp.Point(102, 18));
            //Roi.Add(new OpenCvSharp.Point(102, 0));
            //Roi.Add(new OpenCvSharp.Point(639, 0));
            //Roi.Add(new OpenCvSharp.Point(639, 158));
            //Roi.Add(new OpenCvSharp.Point(496, 231));
            //Roi.Add(new OpenCvSharp.Point(330, 288));
            //RoiList.Add(Roi);

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
            //관심영역 In, out 체크
            int cnt = 0;
            foreach (List<Point> list in RoiList)
            {
                if (polygon.isInside(list, e.X, e.Y))
                {
                    Console.WriteLine("polygon.isInside:  ({0},{1}) is in the {2}th ROI", e.X, e.Y, cnt++);
                }
            }



            if (cbVitualFlame.Checked)  //가상 화염
            {
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
                        if (virtualFlameList.Count > 0) virtualFlameList.RemoveAt(0);
                    }
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

        private void pbScreen_MouseMove(object sender, MouseEventArgs e)
        {
        }

        private void btRoiConfig_Click(object sender, EventArgs e)
        {
            Bitmap bitmap = (Bitmap)pbScreen.Image;
            FormRoi fr = new FormRoi(bitmap, this);
            fr.Show();
        }
    }
}
