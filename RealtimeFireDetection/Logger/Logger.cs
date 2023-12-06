using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.IO.Directory;

namespace RealtimeFireDetection.Logger
{
    class Logger
    {
        static Queue<LogMessage> logQueue = new Queue<LogMessage>();
        private static readonly object writeLock = new object();

        public static void WriteLog(out LogMessage msg, LogType logtype, string str, bool bwritenow = false)
        {
            msg = null;
            if (str.Trim().Length == 0 || str.Trim().Equals("")) return;
            string path = @".\logs\" + DateTime.Now.ToString("yyyyMM") + @"\";
            if (!Exists(path))
            {
                CreateDirectory(path);
            }
            string filename = path + "Edge_" + DateTime.Now.ToString("yyyyMMdd") + ".log";
            string logmsg = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + str;
            Console.WriteLine(logmsg);
            LogMessage logMessage = new LogMessage(logtype, logmsg);
            msg = logMessage;
            logQueue.Enqueue(logMessage);
            if (bwritenow == true)
            {
                lock (writeLock)
                {
                    using (StreamWriter sw = new StreamWriter(filename, append: true))
                    {
                        while (logQueue.Count > 0)
                        {
                            LogMessage log = logQueue.Dequeue();
                            sw.Write(log.logmsg + "\r\n");
                        }
                    }
                }
            }
        }
    }
}
