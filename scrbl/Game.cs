using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System;
using static scrbl.Utils;

namespace scrbl {
    public static class Game {
        public static readonly List<char> Letters = new List<char>();

        public static readonly List<DecisionMaker.Move> OwnMoves = new List<DecisionMaker.Move>();
        public static readonly List<DecisionMaker.Move> OpponentMoves = new List<DecisionMaker.Move>();

        public static Board Board = new Board();

        public static readonly DecisionMaker Brain = new DecisionMaker();
        public static int BlankCount = 0;

        public static void Save() {
            using (Stream stream = new FileStream("save.sav", FileMode.Create)) {
                using (StreamWriter writer = new StreamWriter(stream)) {
                    //Save our letters and if we don't have 7 just pad.
                    for (int i = 0; i < 7; i++) {
                        if (i >= Letters.Count) {
                            writer.Write(' ');
                        } else {
                            writer.Write(Letters[i]);
                        }
                    }

                    //To save a move, we just write the word followed by the indexes of the first and last squares.
                    for (int i = 0; i < OwnMoves.Count; i++) {
                        var move = OwnMoves[i];
                        string idx1 = Board.Squares.Keys.ToList().IndexOf(move.FirstLetterPos).ToString("00");
                        string idx2 = Board.Squares.Keys.ToList().IndexOf(move.LastLetterPos).ToString("00");

                        writer.Write($"{move.Word}{idx1}{idx2}");
                    }
                    writer.Write("/");
                    for (int i = 0; i < OpponentMoves.Count; i++) {
                        var move = OpponentMoves[i];
                        string idx1 = Board.Squares.Keys.ToList().IndexOf(move.FirstLetterPos).ToString("00");
                        string idx2 = Board.Squares.Keys.ToList().IndexOf(move.LastLetterPos).ToString("00");

                        writer.Write($"{move.Word}{idx1}{idx2}");
                    }
                    writer.Write('/');
                    //Save the letters in order.
                    string rows = "ABCDEFGHIJKLMNO";
                    for (int i = 0; i < 15; i++) {
                        var squares = Board.GetRow(rows[i]);
                        for (int j = 0; j < squares.Count; j++) {
                            var square = squares[j];
                            writer.Write(Board.GetSquareContents(square));
                        }
                    }
                    writer.Flush();
                }
            }
        }

        public static void Load() {
            using (Stream stream = new FileStream("save.sav", FileMode.Open)) {
                using (StreamReader reader = new StreamReader(stream)) {
                    Write("Loading letters... ", ConsoleColor.Red);
                    //Read all 7 chars, even if they are spaces. We can remove those after.
                    for (int i = 0; i < 7; i++) {
                        Letters.Add((char)reader.Read());
                    }
                    Letters.RemoveAll(ch => ch == ' ');
                    Write("Done!", ConsoleColor.DarkCyan);
                    Console.WriteLine();

                    Write("Loading moves (1/2)... ", ConsoleColor.Red);
                    while (true) {
                        var read = new List<char>();
                        char ch;

                        StringBuilder wdB = new StringBuilder();
                        while (char.IsLetter(ch = (char)reader.Read())) {
                            wdB.Append(ch);
                            read.Add(ch);
                        }

                        if (ch == '/') break;

                        StringBuilder idxB = new StringBuilder();
                        while (char.IsDigit(ch = (char)reader.Read())) {
                            idxB.Append(ch);
                            read.Add(ch);
                        }
                        var chs = idxB.ToString().ToCharArray();
                        int.TryParse(string.Join("", chs.Take(2)), out int idx1);
                        int.TryParse(string.Join("", chs.Skip(2).Take(2)), out int idx2);

                        var move = new DecisionMaker.Move(wdB.ToString(),
                                                          Board.Squares.Keys.ToArray()[idx1],
                                                          Board.Squares.Keys.ToArray()[idx2]);
                        OwnMoves.Add(move);
                        if (ch == '/') break;
                        if (read.FastContains('/')) break;
                    }
                    Write("Done!", ConsoleColor.DarkCyan);

                    Console.WriteLine();
                    Write("Loading moves (2/2)... ", ConsoleColor.Red);
                    while (true) {
                        var read = new List<char>();
                        char ch;

                        StringBuilder wdB = new StringBuilder();
                        while (char.IsLetter(ch = (char)reader.Read())) {
                            wdB.Append(ch);
                            read.Add(ch);
                        }

                        if (ch == '/') break;

                        StringBuilder idxB = new StringBuilder();
                        while (char.IsDigit(ch = (char)reader.Read())) {
                            idxB.Append(ch);
                            read.Add(ch);
                        }
                        var chs = idxB.ToString().ToCharArray();
                        int.TryParse(string.Join("", chs.Take(2)), out int idx1);
                        int.TryParse(string.Join("", chs.Skip(2).Take(2)), out int idx2);

                        var move = new DecisionMaker.Move(wdB.ToString(),
                                                          Board.Squares.Keys.ToArray()[idx1],
                                                          Board.Squares.Keys.ToArray()[idx2]);
                        OpponentMoves.Add(move);
                        if (ch == '/') break;
                        if (read.FastContains('/')) break;
                    }
                    Write("Done!", ConsoleColor.DarkCyan);
                    Console.WriteLine();

                    Write("Loading board... ", ConsoleColor.Red);
                    string rows = "ABCDEFGHIJKLMNO";
                    for (int i = 0; i < 15; i++) {
                        var squares = Board.GetRow(rows[i]);
                        for (int j = 0; j < squares.Count; j++) {
                            var square = squares[j];
                            char ch;
                            if ((ch = (char)reader.Read()) != ' ') {
                                Board.SetSquareContents(square, ch);
                            }
                        }
                    }

                    Write("Done!", ConsoleColor.DarkCyan);
                    Console.WriteLine();
                }
            }
        }
    }
}