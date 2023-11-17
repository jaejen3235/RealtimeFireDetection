﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealtimeFireDetection.Yolov5
{
    public class Prediction
    {
        public Box Box { get; set; }
        public string Label { get; set; }

        public int Id { get; set; }
        public float Confidence { get; set; }
    }

    public class Box
    {
        public float Xmin { get; set; }
        public float Ymin { get; set; }
        public float Xmax { get; set; }
        public float Ymax { get; set; }
    }
}
