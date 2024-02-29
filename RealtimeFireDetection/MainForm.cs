using IPC4Fire;
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
using System.IO.Ports;
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
        string VER = "1.0.6";
        //1.0.1 Retry to open the VideoCapture, Add Delay() instaed of Thread.sleep()
        //1.0.2 Cross thread issue (pbScreen.Image = bmp)
        //1.0.3 Improve a tring to connect to cctv(VideoCapture.Open)
        //1.0.4 Modify the LoRa message log can be output only when the serial port is normally opened
        //1.0.5 CCTV 연결상태 유지 (Retry 임시 제거)
        //1.0.6 CCTV 영상을 10개, 10초 단위로 실시간 저장, YoLo 분석은 직전 저장된 영상을 이용하고 여기서 판단된 영상 정보를 전송

        public readonly static byte ALARM_TYPE_NORMAL = 0;
        public readonly static byte ALARM_TYPE_WARN = 1;
        public readonly static byte ALARM_TYPE_OCCUR = 2;

        //public static string LocalName = "";
        public static SerialPort LoRaSerialPort; // = new SerialPort();
        private StringBuilder sbLoRaReceiveData; // = new StringBuilder();
        private DateTime lastLoRaTxRxTime;

        private bool LoRaSendStart = false;
        private List<byte[]> listOfResponse = new List<byte[]>();
        private string comport;
        private string baudRate = "115200";
        private bool rbLoRaReceiveASCII = true;

        private Thread threadDoWork;
        private bool bThreadDoWorkRun = false;
        private int bSec = -1;

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
        //FRAME_COUNT_WARN=10
        private int frameCountWarn = 10;
        //WARN_THRESHOLD(%)=50
        private double thresholdWarnRate = 0.5;
        //FRAME_COUNT_OCCUR=10
        private int frameCountOccur = 10;
        //OCCUR_THRESHOLD(%)=50
        private double thresholdOccurRate = 0.5;

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

        RemoteObject remoteObject; //Communicate with SendEdgeFire("Edge Monitor for Fire 1.0.0)

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
                    Console.WriteLine(line);
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
                sr?.Close();
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
                if (FlameList.Count == 0)   //최초 화염
                {
                    Flame flame = new Flame(this, strNow);
                    flame.AddFirstFlameInfo(info);
                    FlameList.Add(flame);
                    Logger.Logger.WriteLog(out message, LogType.Info, "초기 Flame Count: " + FlameList.Count, false);
                    Logger.Logger.WriteLog(out message, LogType.Info, "Flame ID " + flame.ID + ", Add info [" + flame.ToString() + "]", false);
                    continue;
                }

                foreach (Flame flame in FlameList) //기존 화염 위치인가?
                {
                    if (flame.AddFlameInfo(info))
                    {
                        Logger.Logger.WriteLog(out message, LogType.Info, "기존 Flame Count: " + FlameList.Count, false);
                        Logger.Logger.WriteLog(out message, LogType.Info, "Flame ID " + flame.ID + ", Add info [" + flame.ToString() + "]", false);
                        isNewFlameInfo = false;
                        break;
                    }
                }

                if (isNewFlameInfo) //새로운 위치의 화염
                {
                    Flame flame = new Flame(this, strNow);
                    flame.AddFirstFlameInfo(info);
                    FlameList.Add(flame);
                    Logger.Logger.WriteLog(out message, LogType.Info, "새로운 Flame Count: " + FlameList.Count, false);
                    Logger.Logger.WriteLog(out message, LogType.Info, "Flame ID " + flame.ID + ", Add new & info [" + flame.ToString() + "]", false);
                }
            }//foreach (var obj in result)

            //Cv2.NamedWindow("RESULT", WindowFlags.AutoSize);
            //Cv2.ImShow("RESULT", dispImage);

            Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
            Graphics g = Graphics.FromImage(bmp);
            string[] tmps = sb.ToString().Trim().Split('/');


            StringBuilder sbFlameInfos = new StringBuilder();
            int saveCnt = 0;
            foreach (Flame flame in FlameList)
            {
                if(flame.state == DetectorState.NO_FIRE)
                {
                    flame.IsItFlame(() => {
                        //Console.WriteLine("Invoked flame.IsItFire");
                        Logger.Logger.WriteLog(out message, LogType.Info, "화염 발견", false);
                        AddLogMessage(message);
                        saveCnt++;
                        save = true;
                        detectorState = DetectorState.DETECT_FLAME;
                    });

                    if (save)
                    {
                        sbFlameInfos.Append(flame.getMaxConfidenceInfo()).Append("|");
                        save = false;
                        //break; 20240111 모든 화염정보에서 Confidence가 WARN_THRESHOLD 보다 큰 정보들을 모아 LoRa 전송하기 위해...
                    }
                }
                else if (flame.state == DetectorState.DETECT_FLAME)
                {
                    flame.IsItFire(() => {
                        Logger.Logger.WriteLog(out message, LogType.Info, "화재 발생", false);
                        AddLogMessage(message);
                        saveCnt++;
                        save = true;
                        detectorState = DetectorState.MONITORING_FIRE;
                    });

                    if (save)
                    {
                        sbFlameInfos.Append(flame.getMaxConfidenceInfo()).Append("|");
                        save = false;
                        //break; 20240111 모든 화염정보에서 Confidence가 OCCUR_THRESHOLD 보다 큰 정보들을 모아 LoRa 전송하기 위해...
                    }
                }
            }
            
            if (saveCnt > 0)
            {
                //화재정보 전달 -> LoRa 모듈
                //UpdateRemoteMessage("FIRE:" + sbFlameInfos.Remove(sbFlameInfos.Length - 1, 1).ToString());

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
                        //20240110 중심부 조각 이미지 저장은 Putting it off until being certain
                        //croppedBitmap.Save(targetFirePath + "/" + strNow + "_" + APP_NAME + "_" + level + "_" + string.Format("L{0:D2}_{1},{2},{3}", cnt, red, green, blue) +"_cropped.bmp", ImageFormat.Bmp);
                    }
                    cnt++;
                }
                writer.Close();
                bmp.Save(targetFirePath + "/" + strNow + "_" + APP_NAME + "_" + level + "_snapshot.bmp", ImageFormat.Bmp);
                LoRaSendStart = true; //Start LoRa
            }
            return bmp;
        }

        private void resetState()
        {
            FlameList.Clear();
            if (detectorState != DetectorState.NO_FIRE)//정상 상태 1회 전송
            {
                LoRaSendStart = true;
                Logger.Logger.WriteLog(out message, LogType.Info, string.Format("[YOLO] {0} ", "Change state to NO_FIRE"), true);
                AddLogMessage(message);
            }
            detectorState = DetectorState.NO_FIRE;
            pbResult.Invoke((MethodInvoker)delegate () {
                pbResult.Image = null;
                pbResult.Invalidate();
            });
            //20240115 LoRa 모듈 통합, IPC 통신 사용 안함
            //화재정보 전달 -> LoRa 모듈
            //UpdateRemoteMessage("NO_FIRE");
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

        private VideoCapture InitVideoCapture()
        {
            VideoCapture video = new VideoCapture();
            //video.setExceptionMode(True);
            //video.set(Cv2.CAP_PROP_OPEN_TIMEOUT_MSEC, 5000);
            video.Open(CamUri);
            if (video.Fps == 0) return null;
            double CAM_FPS = video.Fps;
            int sleepTime = (int)Math.Round(1000 / video.Fps);
            LogMessage msg;
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("VIDEO CAPTURE  FPS:{0}, SLEEP_TIME:{1}", video.Fps, sleepTime), true);
            AddLogMessage(msg);
            return video;
        }

        private void Delay(int ms)
        {
            DateTime dateTimeNow = DateTime.Now;
            TimeSpan duration = new TimeSpan(0, 0, 0, 0, ms);
            DateTime dateTimeAdd = dateTimeNow.Add(duration);
            while (dateTimeAdd >= dateTimeNow)
            {
                System.Windows.Forms.Application.DoEvents();
                dateTimeNow = DateTime.Now;
            }
            return;
        }

        private void PlayAndRecord()
        {
            VideoCapture vc = null;
            VideoWriter vw = new VideoWriter();
            int failCount = 0;
            int recordNum = 0;
            string path = "./records/";
            string recName = "";

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            Mat image = new Mat();

            while (true)
            {
                vc = InitVideoCapture();
                if (!vc.IsOpened()) 
                { 
                    Thread.Sleep(10 * 1000);  continue; 
                }

                Console.WriteLine("{0} {1}", DateTime.Now, "InitVideoCapture()");
                doPlay = true;
                int fHeight = vc.FrameHeight;
                int fWidth = vc.FrameWidth;

                DateTime startTime = DateTime.Now;
                recName = string.Format("record_{0:D2}.avi", recordNum);
                vw.Open(string.Format("{0}{1}", path, recName), FourCC.DIVX, vc.Fps, new OpenCvSharp.Size(fWidth, fHeight));

                while (doPlay)
                {
                    bool b = vc.Read(image);
                    Cv2.WaitKey(1);

                    if (b && !image.Empty())
                    {
                        //drawDateTimeOnTheMat(image, DateTime.Now.ToString(@"yyyy\/MM\/dd HH:mm:ss.fff"));
                        /////////////////////////////////////////////////////////////////////////// 
                        ///관심영역
                        if (DicRoiList.Count > 0)
                        {
                            RoiList.Clear();
                            foreach (KeyValuePair<string, List<Point>> kv in DicRoiList)
                            {
                                RoiList.Add(kv.Value);
                            }
                            Cv2.Polylines(image, RoiList, true, Scalar.Magenta, 1, LineTypes.AntiAlias);
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
                            Cv2.FillPoly(image, NonRoiList, Scalar.Black);
                        }
                        if (pbScreen.InvokeRequired)
                        {
                            pbScreen.Invoke((MethodInvoker)delegate ()
                            {
                                pbScreen.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
                            });
                        }
                        else
                        {
                            pbScreen.Image = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(image);
                        }

                        if (!vw.IsOpened())
                        {
                            recName = string.Format("record_{0:D2}.avi", recordNum);
                            Console.WriteLine("{0} Record start ({1}).", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), recName);
                            startTime = DateTime.Now;
                            vw.Open(string.Format("{0}{1}", path, recName), FourCC.XVID, vc.Fps, new OpenCvSharp.Size(fWidth, fHeight));
                        }

                        vw.Write(image);

                        if ((DateTime.Now - startTime).TotalMilliseconds >= (10 * 1000))
                        {
                            Console.WriteLine("{0} Record done ({1}).", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), recName);
                            Thread t = new Thread(() => FlameDetector(path + recName));
                            t.Start();
                            if (recordNum >= 9) recordNum = 0;
                            else recordNum++;
                            vw.Release();
                        }
                        failCount = 0;
                    }
                    else
                    {
                        failCount++;
                        Console.WriteLine("{0} Fail count to grab image ({1}).", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), failCount);
                    }

                    //Thread.Sleep((int)vc.Fps);
                    if (failCount >= 60)
                    {
                        failCount = 0;
                        doPlay = false;
                        if (vc != null) vc.Release();
                        if (vw != null) vw.Release();
                    }
                }//while(doPaly)
                try
                {
                    if (vc != null) vc.Release();
                    if (vw != null) vw.Release();
                    vc = null; vw = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }//while
        }

        private void FlameDetector(string recName)
        {
            VideoCapture capture = new VideoCapture(recName);

            int sleepTime = (int)Math.Round(1000 / capture.Fps);

            using (Mat image = new Mat()) // Frame image buffer
            {
                Thread.Sleep(2000);
                // When the movie playback reaches end, Mat.data becomes NULL.
                Console.WriteLine("{0} Play start ({1}).", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), recName);
                Console.WriteLine("{0} FPS {1}, Frame count {2}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), capture.Fps, capture.FrameCount);
                while (capture.Read(image))
                {
                    if(image == null || image.Empty()) break;
                    capture.Read(image);
                    Cv2.WaitKey((int)capture.Fps);
                }
                image.Dispose();
                capture.Release();
                Cv2.DestroyAllWindows();
                Console.WriteLine("{0} Play done.", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"));
            }
        }

        private void RunFlameMonitor()
        {
            detector = new YoloDetector("best_yolov5.onnx");
            VideoCapture video = InitVideoCapture();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Mat matImage = null;
            bool isFirst = true;
            Stopwatch noFireWatch = new Stopwatch();
            noFireWatch.Stop();
            System.Drawing.Size resize = new System.Drawing.Size(pbScreen.Width, pbScreen.Height);
            int waitCount = 0;

            using (Mat image = new Mat())
            {
                while (doPlay)
                {
                    if (video == null || !video.Read(image))
                    {
                        Cv2.WaitKey();
                        waitCount++;
                        if (waitCount >= 500)
                        {
                            waitCount = 0;
                            try
                            {
                                if (video != null)
                                {
                                    video.Release();
                                    video.Dispose();
                                }
                                Delay(2 * 1000);
                                Console.WriteLine("{0} {1}", DateTime.Now, "InitVideoCapture()");
                                video = InitVideoCapture();
                                Delay(2 * 1000);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("E1: " + e.StackTrace);
                                Console.WriteLine("E2: " + e.Message);
                                Console.WriteLine("E3: " + e.Source);
                                Console.WriteLine("E4: " + e.ToString());
                                continue;
                            }
                        }

                    }
                    if (!image.Empty())
                    {
                        waitCount = 0;
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

                        //20240227 invokeRequired
                        if (pbScreen.InvokeRequired)
                        {
                            pbScreen.Invoke((MethodInvoker)delegate ()
                            {
                                pbScreen.Image = bmp;
                            });
                        }
                        else
                        {
                            pbScreen.Image = bmp;
                        }

                        if (stopwatch.ElapsedMilliseconds > 1000 * fireCheckDuration)
                        {
                            stopwatch.Stop();

                            Thread t = new Thread(() => {
                                using(Mat yoloImage = matImage.Clone())
                                {
                                    var result = DoYoLo(yoloImage);
                                    
                                    //if (result.Count > 0 && checkROIin(result))
                                    if (result != null && result.Count > 0)
                                    {
                                        Bitmap bmpResult = saveFlameInfo(yoloImage, result);
                                        bmp = new Bitmap(bmpResult, resize);

                                        pbResult.Invoke((MethodInvoker)delegate ()
                                        {
                                            pbResult.Image = bmp;
                                            pbResult.Update();
                                        });
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

            public double Deviation2 { get; set; }

            public double StandardDeviation { get; set; }
            
            public string GetRegionInfo()
            {
                return string.Format("{0},{1},{2},{3},{4:0.00}", Area.X, Area.Y, Area.Width, Area.Height, Confidence);
            }

            override
            public string ToString()
            {
                return string.Format("Location X:{0}  Y:{1}  W:{2}  H:{3}  Confidence:{4:0.00}  DiagonalLength:{5:0.00}  StandardDeviation:{6:0.00}  Deviation2:{7:0.00}", Area.X, Area.Y, Area.Width, Area.Height, Confidence, DiagonalLength, StandardDeviation, Deviation2);
            }
        }

        List<Flame> FlameList = new List<Flame>();

        class Flame
        {
            MainForm mf;
            public DetectorState state = DetectorState.NO_FIRE;
            public string ID { get; set; }
            public List<FlameInfo> FlameInfoList = new List<FlameInfo>();

            public DateTime DtFlame { get; set; }
            public DateTime DtFire { get; set; }
            
            public Flame(MainForm mf, string id)
            {
                this.mf = mf;
                this.ID = id;
                state = DetectorState.NO_FIRE;
                DtFlame = DateTime.Now;
                DtFire = DateTime.Now;
            }

            public void AddFirstFlameInfo(FlameInfo info)
            {
                info.DiagonalLength = GetDiagonalLength(info.Area.Width, info.Area.Height);
                FlameInfoList.Add(info);
                GetSDAll();
            }

            public bool AddFlameInfo(FlameInfo info)
            {
                //LogMessage msg;
                //Logger.Logger.WriteLog(out msg, LogType.Info, No + " - " + "Add flame: " + info.ToString(), false);


                //if (FlameInfoList.Count < 2) return true;
                if (IsSameFlame(info.Area))
                {
                    if (FlameInfoList.Count > mf.frameCountWarn) FlameInfoList.RemoveAt(0);
                    info.DiagonalLength = GetDiagonalLength(info.Area.Width, info.Area.Height);
                    FlameInfoList.Add(info);
                    GetSDAll();
                    return true;
                }
                return false;
            }

            private void GetSDAll()
            {
                double average;
                double sumOfDeviation = 0;

                //var ret = FlameInfoList.Select(s => s.DiagonalLength);
                //average = ret.Average();
                average = FlameInfoList.Average(info => info.DiagonalLength);

                //편차의 제곱 (편차가 큰 경우 실제 화재로 인식하기 위해..)
                foreach (FlameInfo info in FlameInfoList)
                {
                    info.Deviation2 = Math.Pow(info.DiagonalLength - average, 2);
                }

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

            public string getMaxConfidenceInfo()
            {
                int index = 0;
                double con = -0.1;
                FlameInfo info;

                for (int i = 0; i < FlameInfoList.Count; i++)
                {
                    info = FlameInfoList[i];
                    if (info.Confidence > con)
                    {
                        con = info.Confidence;
                        index = i;
                    }
                }
                return FlameInfoList[index].GetRegionInfo();
            }
            public FlameInfo getMaxConfidenceInfoData()
            {
                int index = 0;
                double con = -0.1;
                FlameInfo info;

                for (int i = 0; i < FlameInfoList.Count; i++)
                {
                    info = FlameInfoList[i];
                    if (info.Confidence > con)
                    {
                        con = info.Confidence;
                        index = i;
                    }
                }
                return FlameInfoList[index];
            }

            public string IsItFlame(Action action)
            {
                bool isAll = true;
                if (FlameInfoList.Count >= mf.frameCountWarn)
                {
                    double avg_confidence = FlameInfoList.Average(info => info.Confidence);

                    if(mf.thresholdWarnRate <= avg_confidence)
                    {
                        DtFlame = DateTime.Now;
                        LogMessage msg;
                        Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("Flame check threshold warn {0:0.00}, avg {1:0.00}", mf.thresholdWarnRate, avg_confidence), false);
                        mf.AddLogMessage(msg);
                    }
                }
                else isAll = false;

                if (isAll)//화염 감지
                {
                    if (state == DetectorState.NO_FIRE) action();
                    state = DetectorState.DETECT_FLAME;
                }
                else
                {
                    state = DetectorState.NO_FIRE;
                }
                return "";
            }

            public string IsItFire(Action action)
            {
                bool isAll = true;
                if (FlameInfoList.Count >= mf.frameCountOccur)
                {
                    //20240111 confidence 평균값으로 INI의 설정값과 비교 (occur_threshold)하여 화재 판단으로 변경
                    //for (int i = 0; i < FlameInfoList.Count; i++)
                    //{
                    //    //검색된 영역의 모든 대각선이 지정된 값 이상인지 확인
                    //    //sum += FlameInfoList.ElementAt(i).StandardDeviation;
                    //    //if (FlameInfoList.ElementAt(i).StandardDeviation <= STANDARD_DEVIATION_LOW_LIMIT)
                    //    //{
                    //    //    isAll = false;
                    //    //    break;
                    //    //}
                    //    //20240109 표준편차가 아닌 편차의 제곱값을 이용하여 화재 판단으로 변경
                    //    if (FlameInfoList.ElementAt(i).Deviation2 <= STANDARD_DEVIATION_LOW_LIMIT)
                    //    {
                    //        isAll = false;
                    //        break;
                    //    }
                    //}

                    double avg_confidence = FlameInfoList.Average(info => info.Confidence);
                    if (mf.thresholdOccurRate <= avg_confidence)
                    {
                        DtFire = DateTime.Now;
                        LogMessage msg;
                        Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("Fire check threshold occur {0:0.00}, avg {1:0.00}", mf.thresholdOccurRate, avg_confidence), false);
                        mf.AddLogMessage(msg);
                        isAll = true;
                    }
                    else isAll = false;
                }
                else isAll = false;

                if (isAll)//화재
                {
                    if (state == DetectorState.DETECT_FLAME) action();
                    state = DetectorState.MONITORING_FIRE;
                }
                else
                {
                    state = DetectorState.DETECT_FLAME;
                }
                return "";
            }

            public string getFlameInfoList()
            {
                StringBuilder sb = new StringBuilder();
                foreach(FlameInfo info in FlameInfoList)
                {
                    sb.Append(info.GetRegionInfo()).Append("|");
                }
                sb.Remove(sb.Length - 1, 1);
                return sb.ToString();
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
            makeFolders(targetFirePath);

            comport = ini.Read("LORA_PORT", "MAIN");
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("LORA_PORT: {0}", comport), true);
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

            tmp = ini.Read("DURATION_FIRE_CHECK(SEC)", "MAIN");
            if (!int.TryParse(tmp, out fireCheckDuration))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "DURATION_FIRE_CHECK(SEC) Not a Number", true);
                AddLogMessage(msg);
            }
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("DURATION_FIRE_CHECK(SEC): {0}", fireCheckDuration), true);
            AddLogMessage(msg);

            tmp = ini.Read("FRAME_COUNT_WARN", "MAIN");
            if (!int.TryParse(tmp, out frameCountWarn))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "FRAME_COUNT_WARN Not a Number", true);
                AddLogMessage(msg);
            }
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("FRAME_COUNT_WARN: {0}", frameCountWarn), true);
            AddLogMessage(msg);

            tmp = ini.Read("WARN_THRESHOLD(%)", "MAIN");
            if (!double.TryParse(tmp, out thresholdWarnRate))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "WARN_THRESHOLD(%) Not a Number", true);
                AddLogMessage(msg);
            }
            thresholdWarnRate = thresholdWarnRate / 100.0;
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("WARN_THRESHOLD(%): {0}", thresholdWarnRate), true);
            AddLogMessage(msg);

            tmp = ini.Read("FRAME_COUNT_OCCUR", "MAIN");
            if (!int.TryParse(tmp, out frameCountOccur))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "FRAME_COUNT_OCCUR Not a Number", true);
                AddLogMessage(msg);
            }
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("FRAME_COUNT_OCCUR: {0}", frameCountOccur), true);
            AddLogMessage(msg);

            tmp = ini.Read("OCCUR_THRESHOLD(%)", "MAIN");
            if (!double.TryParse(tmp, out thresholdOccurRate))
            {
                Logger.Logger.WriteLog(out msg, LogType.Error, "OCCUR_THRESHOLD(%) Not a Number", true);
                AddLogMessage(msg);
            }
            thresholdOccurRate = thresholdOccurRate / 100.0;
            Logger.Logger.WriteLog(out msg, LogType.Info, string.Format("OCCUR_THRESHOLD(%): {0}", thresholdOccurRate), true);
            AddLogMessage(msg);

            flameIcons[0] = Bitmap.FromFile("./f01.png");
            flameIcons[1] = Bitmap.FromFile("./f02.png");
            flameIcons[2] = Bitmap.FromFile("./f03.png");
            flameIcons[3] = Bitmap.FromFile("./f04.png");
            flameIcons[4] = Bitmap.FromFile("./f05.png");
            flameIcons[5] = Bitmap.FromFile("./f06.png");
            flameIcons[6] = Bitmap.FromFile("./f07.png");
            flameIcons[7] = Bitmap.FromFile("./f08.png");

            InitSerialPort();


            doPlay = true;
            //bMin = DateTime.Now.Minute;
            //CamThread = new Thread(playCam)
            /*            CamThread = new Thread(RunFlameMonitor)
                        {
                            Name = "Camera Thread"
                        };
                        CamThread.Start();
            */
            CamThread = new Thread(PlayAndRecord)
            {
                Name = "Camera Thread"
            };
            CamThread.Start();
        }

        private void InitSerialPort()
        {
            string[] str = SerialPort.GetPortNames();
            if (str == null)
            {
                Logger.Logger.WriteLog(out message, Logger.LogType.Info, "Can't find any serial port.", true);
                return;
            }
            LoRaSerialPort = new SerialPort();
            sbLoRaReceiveData = new StringBuilder();
            LoRaSerialPort.DataReceived += new SerialDataReceivedEventHandler(loraDataReceived);
            LoRaSerialPort.DtrEnable = true;
            LoRaSerialPort.RtsEnable = true;
            LoRaSerialPort.ReadTimeout = 1000;
            LoRaSerialPort.Close();

            try
            {
                Int32 iBaudRate = Convert.ToInt32(baudRate);
                Int32 iDataBit = 8;
                LoRaSerialPort.PortName = comport;
                LoRaSerialPort.BaudRate = iBaudRate;
                LoRaSerialPort.DataBits = iDataBit;
                LoRaSerialPort.StopBits = StopBits.One;
                LoRaSerialPort.Parity = Parity.None;
                LoRaSerialPort.Open();
                Logger.Logger.WriteLog(out message, Logger.LogType.Info, "Open a LoRa serial port", true);
                lastLoRaTxRxTime = DateTime.Now;
            }
            catch (System.Exception ex)
            {
                Logger.Logger.WriteLog(out message, Logger.LogType.Error, ex.Message, true);
                return;
            }
        }

        class DetectionObject
        {
            public byte AlarmType { get; set; }
            public byte LocX
            {
                set; get;
            }
            public byte LocY
            {
                set; get;
            }
            public byte Width
            {
                set; get;
            }
            public byte Height
            {
                set; get;
            }
            public byte Confidence
            {
                set; get;
            }

            public DetectionObject()
            {
                AlarmType = 0x00;
                LocX = 0x00;
                LocY = 0x00;
                Width = 0x00;
                Height = 0x00;
                Confidence = 0x00;
            }

            public byte[] getBytes()
            {
                byte[] bs = new byte[6];
                bs[0] = AlarmType;
                bs[1] = LocX;
                bs[2] = LocY;
                bs[3] = Width;
                bs[4] = Height;
                bs[5] = Confidence;
                return bs;
            }
        }

        private byte[] MakeDataFrame4LoRa()
        {
            int countFlames = 0;
            byte[] sendTime;
            byte[] acqTime;

            sendTime = dateTimeToByteArray(DateTime.Now);
            Flame flame;
            List<byte> lbs = new List<byte>();
            lbs.AddRange(sendTime);
            for(int i = 0; i < 6; i++)
            {
                if (i < FlameList.Count())
                {
                    flame = FlameList[i];
                    if (flame.state == DetectorState.DETECT_FLAME)
                    {
                        if (countFlames == 0)
                        {
                            acqTime = dateTimeToByteArray(flame.DtFlame);
                            lbs.AddRange(acqTime);
                        }
                        FlameInfo info = flame.getMaxConfidenceInfoData();
                        lbs.Add(ALARM_TYPE_WARN);
                        lbs.Add((byte)(info.Area.X / 4));
                        lbs.Add((byte)(info.Area.Y / 4));
                        lbs.Add((byte)(info.Area.Width / 4));
                        lbs.Add((byte)(info.Area.Height / 4));
                        lbs.Add((byte)(info.Confidence * 100));
                    }
                    else if (flame.state == DetectorState.MONITORING_FIRE)
                    {
                        if (countFlames == 0)
                        {
                            acqTime = dateTimeToByteArray(flame.DtFire);
                            lbs.AddRange(acqTime);
                        }
                        FlameInfo info = flame.getMaxConfidenceInfoData();
                        lbs.Add(ALARM_TYPE_OCCUR);
                        lbs.Add((byte)(info.Area.X / 4));
                        lbs.Add((byte)(info.Area.Y / 4));
                        lbs.Add((byte)(info.Area.Width / 4));
                        lbs.Add((byte)(info.Area.Height / 4));
                        lbs.Add((byte)(info.Confidence * 100));
                    }
                    else
                    {
                        if(countFlames == 0)
                        {
                            acqTime = sendTime;
                            lbs.AddRange(acqTime);
                        }
                        lbs.Add(ALARM_TYPE_NORMAL);
                        lbs.Add(0);
                        lbs.Add(0);
                        lbs.Add(0);
                        lbs.Add(0);
                        lbs.Add(0);
                    }
                }
                else
                {
                    if (countFlames == 0)
                    {
                        acqTime = sendTime;
                        lbs.AddRange(acqTime);
                    }
                    lbs.Add(ALARM_TYPE_NORMAL);
                    lbs.Add(0);
                    lbs.Add(0);
                    lbs.Add(0);
                    lbs.Add(0);
                    lbs.Add(0);
                }
                countFlames++;
                if (countFlames >= 6) break;
            }
            return lbs.ToArray();
        }

        private void ThreadDoWork()
        {
            int threadStep = 0;
            int bStep = 0;
            byte[] bs = null;
            int threadStepStayCnt = 0;
            bool work = true;
            while (work)
            {
                //Console.WriteLine("ThreadDoWork step: {0}", threadStep);
                switch (threadStep)
                {
                    default:
                        break;
                    case 0:
                        if (LoRaSendStart)
                        {
                            LoRaSendStart = false;
                            threadStep++;
                        }
                        break;
                    case 1:
                        if(listOfResponse.Count > 0)
                        {
                            threadStep = 10;
                        }
                        else
                        {
                            bs = MakeDataFrame4LoRa();
                            if (bs == null || bs.Length == 0)
                            {
                                threadStep = 0;
                            }
                            else threadStep++;
                        }
                        break;

                    case 2:
                        sendLoraData(bs, 0, bs.Length, 0x60);
                        threadStep = 100;
                        bStep = 0;
                        break;
                    //////////////////////////////////////////////////////////////////////////////////////////
                    case 10:
                        if(listOfResponse.Count > 0)
                        {
                            bs = listOfResponse[0];
                            listOfResponse.RemoveAt(0);
                            listOfResponse.TrimExcess();
                            threadStep++;
                        }
                        else
                        {
                            threadStep = 0;
                        }
                        break;
                    case 11:
                        sendLoraData(bs, 0, bs.Length, 0x72);
                        threadStep = 100;
                        bStep = 10;
                        break;
                    //////////////////////////////////////////////////////////////////////////////////////////
                    case 100: //데이터 전송 후 대기 약 8초
                        threadStepStayCnt++;
                        if (threadStepStayCnt > 8)
                        {
                            threadStep = bStep;
                            threadStepStayCnt = 0;
                        }
                        break;
                }
                Thread.Sleep(1000);
            }
        }

        private void sendLoraData(byte[] convertHex, int offset, int len, byte type)
        {
            byte[] stx = new byte[1] { 0x02 };
            byte[] frameLen = new byte[1];
            frameLen[0] = (byte)(len + 2);
            byte[] typeCode = new byte[1] { type };
            byte[] bodyLen = new byte[1];
            bodyLen[0] = (byte)len;

            byte[] temp4Crc = frameLen.Concat(typeCode).Concat(bodyLen).Concat(convertHex).ToArray();

            ushort uscrc = GetCRC(temp4Crc, 0, temp4Crc.Length);
            byte[] crc = BitConverter.GetBytes(uscrc);
            Array.Reverse(crc);
            byte[] dataFrame = stx.Concat(temp4Crc).Concat(crc).ToArray();

            byte[] atcmd = stringToByte("at+bdat=");
            byte[] totalLen = new byte[1];
            totalLen[0] = (byte)dataFrame.Length;
            byte[] finalFrame = atcmd.Concat(totalLen).Concat(dataFrame).ToArray();
            StringBuilder sb = new StringBuilder();
            foreach (byte b in finalFrame)
            {
                sb.Append(string.Format("{0:X2} ", b));
            }
            if (LoRaSerialPort.IsOpen)
            {
                LoRaSerialPort.Write(finalFrame, 0, finalFrame.Length);
                lastLoRaTxRxTime = DateTime.Now;
                Logger.Logger.WriteLog(out message, Logger.LogType.Info, "-> LoRa: [" + hexByteToAscii(dataFrame) + "]", true);
                AddLogMessage(message);
            }
        }

        private string hexByteToAscii(byte[] bs)
        {
            List<byte> list = new List<byte>();
            byte[] bb;
            foreach (byte b in bs)
            {
                bb = Encoding.ASCII.GetBytes(b.ToString("X2"));
                foreach (byte bb1 in bb)
                {
                    list.Add(bb1);
                }
            }
            return Encoding.Default.GetString(list.ToArray());
        }

        private byte[] stringToByte(string str)
        {
            byte[] StrByte = Encoding.UTF8.GetBytes(str);
            return StrByte;
        }

        private byte[] dateTimeToByteArray(DateTime dateTime)
        {
            //byte[] b = new byte[1];
            //b[0] = (byte)(dateTime.Millisecond / 10);
            long bNow = ((DateTimeOffset)dateTime).ToUnixTimeSeconds(); //get Timestamp
            byte[] arrayNow = BitConverter.GetBytes((int)bNow);
            Array.Reverse(arrayNow);
            //return arrayNow.Concat(b).ToArray();
            return arrayNow;
        }

        StringBuilder LoRa_sb = new StringBuilder();
        private void loraDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //extDevMgmt app msg received.(len = 27)
            //02 17 53 15 01 7f c3 03 1c 80 3c f6 40 80 3c f6 a4 80 3c f7 08 80 3c f7 6c 6b 4b        
            //Ack

            if (LoRaSerialPort.IsOpen)
            {
                DateTime dateTimeNow = DateTime.Now;
                string receivedData = LoRaSerialPort.ReadExisting();
                lastLoRaTxRxTime = DateTime.Now;
                string tmp = "";
                StringBuilder sb = new StringBuilder();
                if (rbLoRaReceiveASCII)
                {
                    try
                    {
                        string[] splitReceiveData = receivedData.Replace("\r\n", "\n").Split(new char[] { '\n' });
                        for (int i = 0; i < splitReceiveData.Length; ++i)
                        {
                            tmp = splitReceiveData[i].Trim();
                            Logger.Logger.WriteLog(out message, Logger.LogType.Info, string.Format("<- LoRa: RxNO-{0} [" + tmp + "]", i), true);
                            if (tmp.Length > 0) LoRa_sb.Append(tmp);
                        }
                        tmp = LoRa_sb.ToString();
                        if (tmp.ToUpper().Contains("LEN") && tmp.ToUpper().Contains("02") && tmp.ToUpper().Contains("ACK"))
                        {

                            tmp = takeRealRecvFrame(tmp);
                            if (tmp.Length == 0)
                            {
                                LoRa_sb.Clear(); return;
                            }
                            byte[] bs = StringToByteArray(tmp);

                            ParsingLoRaMessage(bs);
                            foreach (byte b in bs)
                            {
                                sb.Append(string.Format("{0:X2} ", b));
                            }
                            Logger.Logger.WriteLog(out message, Logger.LogType.Info, "   LoRa: DATA Bytes [" + sb.ToString().Trim() + "]", true);
                        }
                        LoRa_sb.Clear();
                    }
                    catch (System.Exception ex)
                    {
                        LoRa_sb.Clear();
                        Console.WriteLine(ex.ToString());
                        Logger.Logger.WriteLog(out message, Logger.LogType.Error, "loraDataReceived: " + ex.ToString(), true);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        char[] values = receivedData.ToCharArray();
                        foreach (char letter in values)
                        {
                            int value = Convert.ToInt32(letter);
                            string hexOutput = String.Format("{0:X2}", value);
                            sbLoRaReceiveData.Append(hexOutput + " ");
                        }
                        Logger.Logger.WriteLog(out message, Logger.LogType.Info, "<- LoRa: " + string.Concat(values), true);
                        sbLoRaReceiveData.Clear();
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private string takeRealRecvFrame(string recv)
        {
            string s = recv.ToUpper();
            //Console.WriteLine(s.Length);
            int index02 = s.LastIndexOf("02");
            int indexAck = s.LastIndexOf("ACK");
            //Console.WriteLine("02 index: {0}", index02);

            string s1 = s.Substring(s.LastIndexOf("LEN"), s.LastIndexOf(")") - s.LastIndexOf("LEN"));
            //Console.WriteLine("s1: {0}", s1);
            string s2 = s1.Substring(s1.IndexOf("=") + 1);
            //Console.WriteLine("s2: {0}", s2);
            int len = int.Parse(s2.Trim());
            //Console.WriteLine("Length: {0}", len);

            int l = indexAck - (index02 + (len * 2 + len - 1));
            //Console.WriteLine("L: {0}", l);

            if (l == 0)
            {
                return s.Substring(index02, (len * 2 + len - 1)).Replace(" ", "");
            }
            return "";
        }

        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                                .Where(x => x % 2 == 0)
                                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                                .ToArray();
        }

        private void TestManualResponse()
        {
            ini = new IniFile("./config.ini");
            string tmp;
            string msg = "Control reponse confing.ini ";
            byte[] sendTime;
            sendTime = dateTimeToByteArray(DateTime.Now);
            List<byte> lbs = new List<byte>();
            lbs.AddRange(sendTime);
            lbs.Add(0x00);  //보고

            tmp = ini.Read("DURATION_FIRE_CHECK(SEC)", "MAIN");
            lbs.Add(byte.Parse(tmp));
            msg = msg + tmp + ", ";

            tmp = ini.Read("WARN_THRESHOLD(%)", "MAIN");
            lbs.Add(byte.Parse(tmp));
            msg = msg + tmp + ", ";

            tmp = ini.Read("FRAME_COUNT_WARN", "MAIN");
            lbs.Add(byte.Parse(tmp));
            msg = msg + tmp + ", ";

            tmp = ini.Read("OCCUR_THRESHOLD(%)", "MAIN");
            lbs.Add(byte.Parse(tmp));
            msg = msg + tmp + ", ";

            tmp = ini.Read("FRAME_COUNT_OCCUR", "MAIN");
            lbs.Add(byte.Parse(tmp));
            msg = msg + tmp + ", ";

            tmp = ini.Read("STANDARD_DEVIATION_LOW_LIMIT", "MAIN");
            lbs.Add(byte.Parse(tmp));
            msg = msg + tmp;

            Logger.Logger.WriteLog(out message, LogType.Info, msg, true);
            AddLogMessage(message);

            //화재감시 설정 응답 (0x72)
            listOfResponse.Add(lbs.ToArray());
            LoRaSendStart = true;
        }

        private void ParsingLoRaMessage(byte[] bs)
        {
            ushort uscrc = GetCRC(bs, 1, bs.Length - 3);
            byte[] crc = BitConverter.GetBytes(uscrc);
            Array.Reverse(crc);
            Console.WriteLine();
            if (bs[bs.Length - 2] != crc[0] || bs[bs.Length - 1] != crc[1])
            {
                Logger.Logger.WriteLog(out message, LogType.Info,
                    string.Format("CRC Error, Msg: {0:X2}{1:X2} Calc: {2:X2}{3:X2}", bs[bs.Length - 2], bs[bs.Length - 1], crc[0], crc[1]),
                    true);
                return;
            }
            //00. 02 : STX
            //01. 09 : Len
            //02. 71 : CMD Code
            //03. 07 : Len
            //04. 01 : controlType
            //05. 02 : checkInterval
            //06. 46 : thresholdWarn
            //07. 05 : warnFrameCount
            //08. 55 : thresholdOccur
            //09. 05 : occurFrameCount
            //10. 0A : diagonalMinChange
            //11. 1A : CRC 0
            //12. E6 : CRC 1


        //private int fireCheckDuration = 10;
        ////FRAME_COUNT_WARN=10
        //private int frameCountWarn = 10;
        ////WARN_THRESHOLD(%)=50
        //private double thresholdWarnRate = 0.5;
        ////FRAME_COUNT_OCCUR=10
        //private int frameCountOccur = 10;
        ////OCCUR_THRESHOLD(%)=50
        //private double thresholdOccurRate = 0.5;

        //private int NO_FIRE_WAIT_TIME = 10;
        //public static double STANDARD_DEVIATION_LOW_LIMIT = 1.0;


            try
            {
                if (bs[4] == 0x01)   //0x00 보고, 0x01 등록
                {
                    ini = new IniFile("./config.ini");
                    string tmp;
                    string msg = "Control confing.ini ";

                    tmp = Convert.ToString(bs[5]);
                    ini.Write("DURATION_FIRE_CHECK", tmp, "MAIN");
                    msg = msg + tmp + ", ";
                    fireCheckDuration = bs[5];

                    tmp = Convert.ToString(bs[6]);
                    ini.Write("WARN_THRESHOLD(%)", tmp, "MAIN");
                    msg = msg + tmp + ", ";
                    thresholdWarnRate = bs[6] / 100.0;

                    tmp = Convert.ToString(bs[7]);
                    ini.Write("FRAME_COUNT_WARN", tmp, "MAIN");
                    msg = msg + tmp + ", ";
                    frameCountWarn = bs[7];

                    tmp = Convert.ToString(bs[8]);
                    ini.Write("OCCUR_THRESHOLD(%)", tmp, "MAIN");
                    msg = msg + tmp + ", ";
                    thresholdOccurRate = bs[8] / 100.0;


                    tmp = Convert.ToString(bs[9]);
                    ini.Write("FRAME_COUNT_OCCUR", tmp, "MAIN");
                    msg = msg + tmp + ", ";
                    frameCountOccur = bs[9];

                    tmp = Convert.ToString(bs[10]);
                    ini.Write("STANDARD_DEVIATION_LOW_LIMIT", tmp, "MAIN");
                    msg = msg + tmp;
                    STANDARD_DEVIATION_LOW_LIMIT = bs[10];

                    Logger.Logger.WriteLog(out message, LogType.Info, msg, true);
                    AddLogMessage(message);

                    //화재감시 설정 응답 (0x72)
                    byte[] sendTime;
                    sendTime = dateTimeToByteArray(DateTime.Now);
                    List<byte> lbs = new List<byte>();
                    lbs.AddRange(sendTime);
                    lbs.Add(0x00);  //보고
                    lbs.Add(bs[5]); lbs.Add(bs[6]); lbs.Add(bs[7]); lbs.Add(bs[8]); lbs.Add(bs[9]); lbs.Add(bs[10]);
                    listOfResponse.Add(lbs.ToArray());
                    LoRaSendStart = true;
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        #region POLYNOMIAL
        private ushort[] TABLE_POLYNOMIAL = new ushort[256] {
            0x0000 ,0x1021 ,0x2042 ,0x3063 ,0x4084 ,0x50A5 ,0x60C6 ,0x70E7   //000 ~ 007
	        ,0x8108 ,0x9129 ,0xA14A ,0xB16B ,0xC18C ,0xD1AD ,0xE1CE ,0xF1EF   //008 ~ 015
	        ,0x1231 ,0x0210 ,0x3273 ,0x2252 ,0x52B5 ,0x4294 ,0x72F7 ,0x62D6   //016 ~ 023
	        ,0x9339 ,0x8318 ,0xB37B ,0xA35A ,0xD3BD ,0xC39C ,0xF3FF ,0xE3DE   //024 ~ 031
	        ,0x2462 ,0x3443 ,0x0420 ,0x1401 ,0x64E6 ,0x74C7 ,0x44A4 ,0x5485   //032 ~ 039
	        ,0xA56A ,0xB54B ,0x8528 ,0x9509 ,0xE5EE ,0xF5CF ,0xC5AC ,0xD58D   //040 ~ 047
	        ,0x3653 ,0x2672 ,0x1611 ,0x0630 ,0x76D7 ,0x66F6 ,0x5695 ,0x46B4   //048 ~ 055
	        ,0xB75B ,0xA77A ,0x9719 ,0x8738 ,0xF7DF ,0xE7FE ,0xD79D ,0xC7BC   //056 ~ 063
	        ,0x48C4 ,0x58E5 ,0x6886 ,0x78A7 ,0x0840 ,0x1861 ,0x2802 ,0x3823   //064 ~ 071
	        ,0xC9CC ,0xD9ED ,0xE98E ,0xF9AF ,0x8948 ,0x9969 ,0xA90A ,0xB92B   //072 ~ 079
	        ,0x5AF5 ,0x4AD4 ,0x7AB7 ,0x6A96 ,0x1A71 ,0x0A50 ,0x3A33 ,0x2A12   //080 ~ 087
	        ,0xDBFD ,0xCBDC ,0xFBBF ,0xEB9E ,0x9B79 ,0x8B58 ,0xBB3B ,0xAB1A   //088 ~ 095
	        ,0x6CA6 ,0x7C87 ,0x4CE4 ,0x5CC5 ,0x2C22 ,0x3C03 ,0x0C60 ,0x1C41   //096 ~ 103
	        ,0xEDAE ,0xFD8F ,0xCDEC ,0xDDCD ,0xAD2A ,0xBD0B ,0x8D68 ,0x9D49   //104 ~ 111
	        ,0x7E97 ,0x6EB6 ,0x5ED5 ,0x4EF4 ,0x3E13 ,0x2E32 ,0x1E51 ,0x0E70   //112 ~ 119
	        ,0xFF9F ,0xEFBE ,0xDFDD ,0xCFFC ,0xBF1B ,0xAF3A ,0x9F59 ,0x8F78   //120 ~ 127
	        ,0x9188 ,0x81A9 ,0xB1CA ,0xA1EB ,0xD10C ,0xC12D ,0xF14E ,0xE16F   //128 ~ 135
	        ,0x1080 ,0x00A1 ,0x30C2 ,0x20E3 ,0x5004 ,0x4025 ,0x7046 ,0x6067   //136 ~ 143
	        ,0x83B9 ,0x9398 ,0xA3FB ,0xB3DA ,0xC33D ,0xD31C ,0xE37F ,0xF35E   //144 ~ 151
	        ,0x02B1 ,0x1290 ,0x22F3 ,0x32D2 ,0x4235 ,0x5214 ,0x6277 ,0x7256   //152 ~ 159
	        ,0xB5EA ,0xA5CB ,0x95A8 ,0x8589 ,0xF56E ,0xE54F ,0xD52C ,0xC50D   //160 ~ 167
	        ,0x34E2 ,0x24C3 ,0x14A0 ,0x0481 ,0x7466 ,0x6447 ,0x5424 ,0x4405   //168 ~ 175
	        ,0xA7DB ,0xB7FA ,0x8799 ,0x97B8 ,0xE75F ,0xF77E ,0xC71D ,0xD73C   //176 ~ 183
	        ,0x26D3 ,0x36F2 ,0x0691 ,0x16B0 ,0x6657 ,0x7676 ,0x4615 ,0x5634   //184 ~ 191
	        ,0xD94C ,0xC96D ,0xF90E ,0xE92F ,0x99C8 ,0x89E9 ,0xB98A ,0xA9AB   //192 ~ 199
	        ,0x5844 ,0x4865 ,0x7806 ,0x6827 ,0x18C0 ,0x08E1 ,0x3882 ,0x28A3   //200 ~ 207
	        ,0xCB7D ,0xDB5C ,0xEB3F ,0xFB1E ,0x8BF9 ,0x9BD8 ,0xABBB ,0xBB9A   //208 ~ 215
	        ,0x4A75 ,0x5A54 ,0x6A37 ,0x7A16 ,0x0AF1 ,0x1AD0 ,0x2AB3 ,0x3A92   //216 ~ 223
	        ,0xFD2E ,0xED0F ,0xDD6C ,0xCD4D ,0xBDAA ,0xAD8B ,0x9DE8 ,0x8DC9   //224 ~ 231
	        ,0x7C26 ,0x6C07 ,0x5C64 ,0x4C45 ,0x3CA2 ,0x2C83 ,0x1CE0 ,0x0CC1   //232 ~ 239
	        ,0xEF1F ,0xFF3E ,0xCF5D ,0xDF7C ,0xAF9B ,0xBFBA ,0x8FD9 ,0x9FF8   //240 ~ 247
	        ,0x6E17 ,0x7E36 ,0x4E55 ,0x5E74 ,0x2E93 ,0x3EB2 ,0x0ED1 ,0x1EF0
        };

        public ushort GetCRC(byte[] _buffer, int _start_idx, int _length)
        {
            ushort crc = 0x0000;
            int ii;
            for (ii = _start_idx; ii < _start_idx + _length; ++ii)
                crc = (ushort)(TABLE_POLYNOMIAL[((crc >> 8) ^ _buffer[ii]) & 0xFF] ^ (crc << 8));
            return crc;
        }
        #endregion


        private void MainForm_Load(object sender, EventArgs e)
        {
            RemoteObject.CreateServer();
            remoteObject = new RemoteObject();
            pbResult.Update();

            threadDoWork = new Thread(new ThreadStart(ThreadDoWork));
            bThreadDoWorkRun = true;
            Logger.Logger.WriteLog(out message, Logger.LogType.Info, "============== Start LoRa thread ================", true);
            threadDoWork.Start();

        }

        private void makeFolders(string path)
        {
            DirectoryInfo info = new DirectoryInfo(path);
            if (!info.Exists) info.Create();
            //info = new DirectoryInfo(targetRecordPath);
            //if (!info.Exists) info.Create();
        }

        private void UpdateRemoteMessage(string msg)
        {
            if (!remoteObject.Str.Equals(msg))
            {
                remoteObject.Str = msg;
                remoteObject.Count++;
            }
            Logger.Logger.WriteLog(out message, LogType.Info, msg, true);
            AddLogMessage(message);
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
                    text = "화염 의심";
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

        private void btResTest_Click(object sender, EventArgs e)
        {
            TestManualResponse();
        }
    }
}
