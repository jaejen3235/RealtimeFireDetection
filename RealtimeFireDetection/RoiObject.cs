using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeFireDetection
{
    class RoiObject
    {
        public string ID
        {
            get; set;
        }
        public bool IsInterested
        {
            get; set;
        }
        public List<Point> points
        {
            get;
        }

        public RoiObject(List<Point> points)
        {
            this.points = points;
        }
    }
}
