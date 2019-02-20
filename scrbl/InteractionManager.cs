using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using static scrbl.Utils;

namespace scrbl {
    static class InteractionManager {

        private static void LoadEverything() {
            PerformColor(ConsoleColor.DarkYellow, () => {
                Console.WriteLine("Loading dictionaries...");

                Stopwatch watch = Stopwatch.StartNew();
                ScrabbleDictionary.LoadDictionaries();
                watch.Stop();

                Console.WriteLine($"Done! Loaded {/*276,643*/ScrabbleDictionary.Words.Count} words in { watch.Elapsed.Milliseconds }ms.");
            });

            Game.Brain.LoadPoints();
        }

        private static List<string> BoardRepresentation() {
            //Define the different parts we need.
            const string
                topLabels = "    1   2   3   4   5   6   7   8   9  10  11  12  13  14  15",
                top = "  ┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐",
                rowSeparator = "  ├───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┤",
                row = "│ {0} │ {1} │ {2} │ {3} │ {4} │ {5} │ {6} │ {7} │ {8} │ {9} │ {10} │ {11} │ {12} │ {13} │ {14} │",
                bottom = "  └───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘";

            //Add the top line.
            var parts = new List<string> {
                topLabels,
                top
            };

            //Split into rows (each row is 15 squares wide).
            var lists = Game.Board.Squares.Keys.ToList().SplitList(15);

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

            var move = new DecisionMaker.Move(parts[0], firstPos, lastPos);
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
                    Console.WriteLine(
                        "You can also enter commands. Currently you can use '!isword word' to check if 'word' " +
                        "is valid.");
                }

                if (!input.Contains("!ISWORD ")) continue;

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

            var move = ParseMove(input);
            return move;
        }

        private static string MoveToString(DecisionMaker.Move move) {
            string wd = move.Word;
            string squareOne = move.FirstLetterPos.column + move.FirstLetterPos.row.ToString();
            string squareTwo = move.LastLetterPos.column + move.LastLetterPos.row.ToString();

            return $"{wd} {squareOne} {squareTwo}".ToLower();
        }

        public static void GetLetters() {
            Game.Letters.Clear();
            Console.Write("Letters: ");
            string letterInput = Console.ReadLine();
            while (letterInput != null && !letterInput.All(c => char.IsLetter(c) || c == '_') || letterInput.Length > 7) {
                Console.Write("Letters: ");
                letterInput = Console.ReadLine();

                //Count the blanks now so that we don't have to do it whilst evaluating (that would be a huge performance hit).
                //Game.blankCount = letterInput.Count(x => letterInput[x] == '_');
            }
            Game.Letters.AddRange(letterInput.ToUpper().ToCharArray());
            foreach (char letter in Game.Letters) {
                if (letter == '_') Game.blankCount++;
            }
            Console.WriteLine();
        }

        public static void Run() {
            LoadEverything();
            Console.Write("How many players are there? (Not including me) ");

            int.TryParse(Console.ReadLine(), out int peoplePlaying);
            if (peoplePlaying == 0) return;

            GetLetters();

            while (true) {
                try {
                    for (int i = 0; i < peoplePlaying; i++) {
                        Game.Board.ExecuteMove(GetMoveInput(), Board.MoveType.Opponent);
                        PrintBoard();
                    }

                    DecisionMaker.Move selfMove = null;
                    int considered = 0;
                    int time = Time(() => {
                        selfMove = Game.Brain.BestMove(out considered);
                    });

                    PerformColor(ConsoleColor.Magenta, () => {
                        Console.WriteLine($"Considered {considered} moves in {time} seconds.");
                    });

                    Console.CursorVisible = true;

                    if (selfMove.Equals(DecisionMaker.Move.Err)) continue;
                    Game.Board.ExecuteMove(selfMove, Board.MoveType.Self);

                    PrintBoard();
                    Console.WriteLine(MoveToString(selfMove));

                    Console.Write($"{selfMove.Word.ToLower()}: ");
                    PerformColor(ConsoleColor.DarkBlue, () => {
                        Console.Write($"'{ScrabbleDictionary.Definitions[selfMove.Word].Trim(null)}'");
                    });
                    Console.WriteLine();
                    GetLetters();
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