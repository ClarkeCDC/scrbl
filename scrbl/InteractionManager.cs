using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace scrbl {
    static class InteractionManager {

        private static void LoadEverything() {
            Utils.PerformColor(ConsoleColor.DarkYellow, () => {
                Console.WriteLine("Loading dictionaries...");

                string currentPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                string nodefs = Path.Combine(currentPath, "nodefs.txt");

                if (!File.Exists(nodefs)) {
                    Console.WriteLine("Unable to find nodefs file!");
                    Console.Write("Enter the path to the nodefs file: ");
                    nodefs = Console.ReadLine();
                }

                string defs = Path.Combine(currentPath, "defs.txt");
                if (!File.Exists(defs)) {
                    Console.WriteLine("Unable to find defs file!");
                    Console.Write("Enter the path to the defs file: ");
                    defs = Console.ReadLine();
                }

                var watch = Stopwatch.StartNew();
                ScrabbleDictionary.LoadDictionaries(nodefs, defs);
                watch.Stop();

                Console.WriteLine($"Done! Loaded {/*276,643*/ScrabbleDictionary.Words.Count} words in { watch.Elapsed.Milliseconds }ms.");
            });
        }

        private static List<string> BoardRepresentation() {
            //Define the different parts we need.
            string topLabels = "    1   2   3   4   5   6   7   8   9  10  11  12  13  14  15";
            string top = "  ┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐";
            string rowSeparator = "  ├───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┤";
            string row = "│ {0} │ {1} │ {2} │ {3} │ {4} │ {5} │ {6} │ {7} │ {8} │ {9} │ {10} │ {11} │ {12} │ {13} │ {14} │";
            string bottom = "  └───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘";

            //Add the top line.
            List<string> parts = new List<string> {
                topLabels,
                top
            };

            //Split into rows (each row is 15 squares wide).
            List<List<(int column, char row)>> lists = Game.Board.Squares.Keys.ToList().SplitList(15);

            //Iterate over the list of lists of squares we made.
            foreach (var lst in lists) {
                List<char> chars = new List<char>();

                foreach (var pos in lst) {
                    chars.Add(Game.Board.GetSquareContents(pos));
                }

                //Turn the list of chars into an array of strings that is officially an array of objects.
                var strChars = chars.Select(c => c.ToString()).ToArray<object>();
                parts.Add(lst[0].row + " " + string.Format(row, strChars));
                parts.Add(rowSeparator);
            }

            //Remove the final separator (which is not required).
            parts.RemoveAt(parts.Count - 1);
            parts.Add(bottom);

            return parts;
        }

        private static void PrintBoard() {
            //Print the board.
            foreach (string segment in BoardRepresentation()) {
                Console.WriteLine(segment);
            }
        }

        //Check if we will be able to parse a move string.
        private static bool ValidateMoveInput(string input) {
            string pattern = @"(([A-Z])\w+) (\d*[A-Z]) (\d*[A-Z])";

            Regex regex = new Regex(pattern);
            return regex.IsMatch(input);
        }

        private static DecisionMaker.Move ParseMove(string input) {
            List<string> parts = input.Split(' ').ToList();

            string firstPosNumber = Regex.Match(parts[1], @"\d+").Value;
            string secondPosNumber = Regex.Match(parts[2], @"\d+").Value;

            var firstPos = (int.Parse(firstPosNumber), parts[1].Replace(firstPosNumber, "").ToCharArray()[0]);
            var lastPos = (int.Parse(secondPosNumber), parts[2].Replace(secondPosNumber, "").ToCharArray()[0]);

            //The squares should probably be validated... meh.

            DecisionMaker.Move move = new DecisionMaker.Move(parts[0], firstPos, lastPos);
            return move;
        }

        //Get a move from the user.
        private static DecisionMaker.Move GetMoveInput() {
            string input = "";
            while (!ValidateMoveInput(input)) {
                Console.Write("Enter a move (type '?' for help): ");
                input = Console.ReadLine().ToUpper();
                if (input.Contains("?")) {
                    Console.WriteLine("Moves should be given in the format: word <start square> <end square>");
                    Console.WriteLine("For example, \"hello 1a 5a\" (without the quotes).");
                }

                if (input.Contains("!ISWORD ")) {
                    string rest = input.Replace("!ISWORD ", "");
                    if (ScrabbleDictionary.Words.Contains(rest.ToUpper().Trim(null))) {
                        Utils.PerformColor(ConsoleColor.DarkCyan, () => {
                            Console.WriteLine($"Valid: {ScrabbleDictionary.Definitions[rest.ToUpper().Trim(null)].Trim(null)}");
                        });

                    } else {
                        Utils.PerformColor(ConsoleColor.DarkCyan, () => {
                            Console.WriteLine("Invalid");
                        });
                    }
                }
            }

            var move = ParseMove(input);
            Console.WriteLine($"DEBUG: {input} first column is {move.FirstLetterPos.column}\nsecond column is {move.LastLetterPos.column}\n" +
                              $"first row is {move.FirstLetterPos.row}\n" +
                              $"second row is {move.LastLetterPos.row}");
            return move;
        }

        private static string MoveToString(DecisionMaker.Move move) {
            string wd = move.Word;
            string squareOne = move.FirstLetterPos.column + move.FirstLetterPos.row.ToString();
            string squareTwo = move.LastLetterPos.column + move.LastLetterPos.row.ToString();

            return $"{wd} {squareOne} {squareTwo}".ToLower();
        }

        public static void Run() {
            LoadEverything();
            int peoplePlaying = 1;
            Console.Write("How many people are playing? (I'm not a person) ");
            int.TryParse(Console.ReadLine(), out peoplePlaying);
            Console.WriteLine();

            Console.Write("Letters: ");
            string letterInput = Console.ReadLine();
            while (!letterInput.All(c => Char.IsLetter(c) || c == '_') || letterInput.Length > 7) {
                Console.Write("Letters: ");
                letterInput = Console.ReadLine();
            }
            ;
            Game.Letters.AddRange(letterInput.ToUpper().ToCharArray());

            while (true) {
                try {
                    for (int i = 0; i < peoplePlaying; i++) {
                        Game.Board.ExecuteMove(GetMoveInput(), Board.MoveType.Opponent);
                        PrintBoard();
                    }

                    DecisionMaker.Move selfMove = null;
                    int considered = 0;
                    int time = Utils.Time(() => {
                        selfMove = Game.Brain.BestMove(out considered);
                    });

                    Utils.PerformColor(ConsoleColor.Magenta, () => {
                        Console.WriteLine($"Considered {considered} moves in {time} seconds.");
                    });

                    Console.CursorVisible = true;

                    if (selfMove.Equals(DecisionMaker.Move.Err)) continue;
                    Game.Board.ExecuteMove(selfMove, Board.MoveType.Self);

                    PrintBoard();
                    Console.WriteLine(MoveToString(selfMove));

                    Console.Write($"{selfMove.Word.ToLower()}: ");
                    Utils.PerformColor(ConsoleColor.DarkBlue, () => {
                        Console.Write($"'{ScrabbleDictionary.Definitions[selfMove.Word].Trim(null)}'");
                    });

                    Game.Letters.Clear();

                    Console.Write("\n\nLetters: ");
                    string letInp = Console.ReadLine();
                    Game.Letters.AddRange(letInp.ToUpper().ToCharArray());

                    Console.WriteLine();
                } catch (Exception e) {
                    Console.WriteLine($"DEBUG: Encountered error: {e}");
                }
            }
        }

        private static void Wait() {
            Console.ReadKey();
        }
    }
}