using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace scrbl {
    internal static class ScrabbleDictionary {
        public static ConcurrentDictionary<string, string> Definitions = new ConcurrentDictionary<string, string>();
        public static HashSet<string> Words = new HashSet<string>();
        private static bool _loaded;

        public static void LoadDictionaries(string ndefs, string defs) {
            if (_loaded) return;

            Words = new HashSet<string>(File.ReadAllLines(ndefs));
            Words.Remove(Words.First());
            Words.Remove(Words.First());

            List<string> defsList = File.ReadAllLines(defs).ToList();
            Parallel.ForEach(defsList, line => {
                string[] separated = line.Split(null, 2);

                if (separated.Length < 2) {
                    return; //Skip
                }

                string word = separated[0];
                string def = separated[1];

                var squareBracketRegex = new Regex(@"\[([^\]]+)\]");
                def = squareBracketRegex.Replace(def, "");
                def = def.Replace("\"", "");

                Definitions.TryAdd(word, def);
            });
            _loaded = true;
        }
    }
}