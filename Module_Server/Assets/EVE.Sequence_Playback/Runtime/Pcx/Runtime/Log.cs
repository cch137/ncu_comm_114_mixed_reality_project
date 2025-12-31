using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pcx {
    public static class Log {
        private static DateTime time = default;
        private static string line = "";
        private static int timeIndex = -1;
        private static int timeLines = 0, fCounter = 0;
        private static List<TimeSpan> times = new List<TimeSpan>();
        private static float total;
        private static string TimeToString()
        {
            return "[" + System.DateTime.Now.ToString("HH:mm:ss") + "] ";
        }
        public static void Print(object o)
        {
            Debug.Log(o.ToString());
        }
        public static void Print(float f)
        {
            total += f;
            fCounter++;
            Print((object)f);
        }
        public static void PrintFloatAverage()
        {
            Print((total / fCounter).ToString("0.0000000000"));
        }
        public static void PrintLine()
        {
            Print(line);
            ClearLine();
        }
        public static void ClearLine()
        {
            line = "";
            if (timeIndex >= 0)
            {
                timeIndex = -1;
                timeLines++;
            }
        }
        public static void PrintAverage(string format = "0.00000")
        {
            ClearLine();
            string l = "";
            foreach (var t in times)
            {
                if (l != "") l += " ";
                l += "[" + (t.TotalSeconds / timeLines).ToString(format) + "]";
            }
            Print(l + " - AVERAGE");
        }
        public static void PrintTime()
        {
            Print(GetTime());
        }
        public static void AddToLine(object o)
        {
            if (line != "") line += " ";
            line += o.ToString();
        }
        public static string GetTime(string format = "0.00000")
        {
            var t = DateTime.Now - time;
            if (times.Count <= ++timeIndex) times.Add(t);
            else times[timeIndex] += t;
            return "[" + t.TotalSeconds.ToString(format) + "]";
        }
        public static void CaptureTime()
        {
            time = DateTime.Now;
        }
    }
}