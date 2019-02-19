using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace scrbl {
    internal static class Utils {
        public static List<List<T>> SplitList<T>(this List<T> me, int size) {
            var list = new List<List<T>>();
            for (int i = 0; i < me.Count; i += size)
                list.Add(me.GetRange(i, Math.Min(size, me.Count - i)));
            return list;
        }

        //Very fast compared to Contains().
        public static bool FastContains<T>(this List<T> me, object obj) {
            return me.Any(ob => ob.Equals(obj));
        }

        public enum ColorType {
            Foreground,
            Background
        }

        //Do stuff with a console colour and then reset.
        public static void PerformColor(ConsoleColor color, Action action, ColorType type = ColorType.Foreground) {
            if (type == ColorType.Foreground) {
                Console.ForegroundColor = color;
            } else {
                Console.BackgroundColor = color;
            }

            action.Invoke();

            Console.ResetColor();
        }

        //Time an operation and return the seconds taken.
        public static int Time(Action action) {
            Stopwatch watch = Stopwatch.StartNew();
            action.Invoke();
            watch.Stop();
            return watch.Elapsed.Seconds;
        }
    }
}