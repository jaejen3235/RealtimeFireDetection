using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeFireDetection.Logger
{
    public enum LogType
    {
        Debug, Info, Error
    }

    public class LogMessage
    {
        public LogType logtype;
        public string logmsg;

        public LogMessage(LogType log, string msg)
        {
            logtype = log;
            logmsg = msg;
        }
    }
}
