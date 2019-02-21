using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        public static bool FastContains<T>(this IEnumerable<T> me, object obj) {
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

        //Separate a string into lines.
        public static IEnumerable<string> Lines(this string str) {
            using (var reader = new StringReader(str)) {
                string line;
                //Keep going until there are no more lines.
                while ((line = reader.ReadLine()) != null) {
                    //Add the line we just read to the return value.
                    yield return line;
                }
            }
        }

        //Remove a range from a HashSet. (I know usually it wouldn't make sense, but the dictionaries are in order.)
        public static void RemoveRange<T>(this HashSet<T> me, int inclusiveStart, int count) {
            int s = inclusiveStart;
            me.RemoveWhere(x => s++ < inclusiveStart + count);
        }

        public static bool TryNext<T>(this List<T> me, T current, out T result) {
            try {
                int curIndex = me.IndexOf(current);
                T next = me[curIndex + 1];
                result = next;
                return true;
            } catch {
                result = current;
                return false;
            }
        }

        public static bool TryNext<T>(this List<T> me, T current, out T result, int times) {
            try {
                int curIndex = me.IndexOf(current);
                T next = me[curIndex + times];
                result = next;
                return true;
            } catch {
                result = current;
                return false;
            }
        }

        //AddRange() for HashSet
        public static void AddRange<T>(this HashSet<T> me, IEnumerable<T> collection) {
            var colEnum = collection as T[] ?? collection.ToArray();
            for (int i = 0; i < colEnum.Length; i++) {
                me.Add(colEnum[i]);
            }
        }

        public static short ToInt(this bool me) {
            return Convert.ToInt16(me);
        }
    }
}