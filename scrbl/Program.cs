using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static scrbl.Utils;

namespace scrbl {
    class Program {
        static void Main(string[] args) {
            InteractionManager.Run();
        }
    }

    //Manage the entire game.
    public static class Game {
        public static readonly List<char> letters = new List<char>();
        public static int selfPoints = 0;
        public static int opponentPoints = 0;

        public static readonly List<DecisionMaker.Move> ownMoves = new List<DecisionMaker.Move>();
        public static readonly List<DecisionMaker.Move> opponentMoves = new List<DecisionMaker.Move>();

        public static Board board = new Board();

        public static readonly DecisionMaker brain = new DecisionMaker();
    }

    //Keep track of the board.
    public class Board {
        public enum Square {
            Normal,
            Middle,
            DoubleLetter,
            TripleLetter,
            DoubleWord,
            TripleWord
        }

        public enum MoveType {
            Self,
            Opponent
        }

        public Board() {
            LoadSquares();
        }

        //When modification to these two properties is required, make a new method to do the job rather than directly interacting with them.
        //It keeps the code neater.
        public readonly Dictionary<(int column, char row), Square> squares = new Dictionary<(int column, char row), Square>();
        public readonly Dictionary<(int column, char row), char> letters = new Dictionary<(int column, char row), char>();

        public readonly List<char> rows = "ABCDEFGHIJKLMNO".ToCharArray().ToList();
        public readonly List<int> columns = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }.ToList();

        void LoadSquares() {
            if (squares.Keys.Count < 1) {


                //Fill a List with normal squares. We add special ones later.
                int squareCount = rows.Count * columns.Count;
                List<Square> squareList = Enumerable.Repeat(Square.Normal, squareCount).ToList();

                //Add the middle.
                squareList[squareCount / 2] = Square.Middle;

                /* TO DO: Add the other special squares here. */

                int listIndex = 0;
                for (int columnInt = 0; columnInt < columns.Count; columnInt++) {
                    int column = columns[columnInt]; //Literally just columnInt + 1 but whatever.
                    for (int rowInt = 0; rowInt < rows.Count; rowInt++) {
                        char row = rows[rowInt];
                        squares[(column, row)] = squareList[listIndex];
                        listIndex++;
                    }
                }
            }
        }

        //Reading
        public Square GetSquare((int column, char row) pos) {
            LoadSquares(); //Load squares if required.
            return squares[pos];
        }

        public char GetSquareContents((int column, char row) pos) {
            if (letters.ContainsKey(pos)) {
                return letters[pos];
            }
            return ' ';
        }

        public enum RelativePosition {
            Left,
            Right,
            Up,
            Down,
            DiagUL,
            DiagUR,
            DiagLL,
            DiagLR
        }

        //Get the squares surrounding the passed one and return a dictionary.
        public Dictionary<RelativePosition, (int column, char row)> GetSurroundingDict((int column, char row) pos) {
            var oneUp = pos.row != 'A' ? (pos.column, rows[rows.IndexOf(pos.row) - 1]) : (-1, 'X');
            var oneDown = pos.row != 'O' ? (pos.column, rows[rows.IndexOf(pos.row) + 1]) : (-1, 'X');
            var oneLeft = pos.column != 1 ? (pos.column - 1, pos.row) : (-1, 'X');
            var oneRight = pos.column != 15 ? (pos.column + 1, pos.row) : (-1, 'X');

            //Diagonal
            var diagUpLeft = (-1, 'X');
            var diagDownLeft = (-1, 'X');
            var diagUpRight = (-1, 'X');
            var diagDownRight = (-1, 'X');
            if (!oneUp.Equals((-1, 'X')) && !oneLeft.Equals((-1, 'X'))) {
                diagUpLeft = (oneLeft.Item1, oneUp.Item2);
            }

            if (!oneDown.Equals((-1, 'X')) && !oneLeft.Equals((-1, 'X'))) {
                diagDownLeft = (oneLeft.Item1, oneDown.Item2);
            }

            if (!oneUp.Equals((-1, 'X')) && !oneRight.Equals((-1, 'X'))) {
                diagUpRight = (oneRight.Item1, oneUp.Item2);
            }

            if (!oneDown.Equals((-1, 'X')) && !oneRight.Equals((-1, 'X'))) {
                diagDownRight = (oneRight.Item1, oneDown.Item2);
            }

            bool TestValid((int column, char row) poz) {
                return !poz.Equals((-1, 'X'));
            }

            Dictionary<RelativePosition, (int column, char row)> dict = new Dictionary<RelativePosition, (int column, char row)>();
            void AddIfValid((int column, char row) poz, RelativePosition rel) {
                if (TestValid(poz)) {
                    dict.Add(rel, poz);
                }
            }

            AddIfValid(oneUp, RelativePosition.Up);
            AddIfValid(oneDown, RelativePosition.Down);
            AddIfValid(oneLeft, RelativePosition.Left);
            AddIfValid(oneRight, RelativePosition.Right);
            AddIfValid(diagUpLeft, RelativePosition.DiagUL);
            AddIfValid(diagUpRight, RelativePosition.DiagUR);
            AddIfValid(diagDownLeft, RelativePosition.DiagLL);
            AddIfValid(diagDownRight, RelativePosition.DiagLR);

            return dict;
        }

        public List<char> GetRowContents(char row) {
            var current = (1, row);

            List<char> contents = new List<char>();
            while (GetSurroundingDict(current).ContainsKey(RelativePosition.Right)) {
                contents.Add(GetSquareContents(current));
                current = GetSurroundingDict(current)[RelativePosition.Right];
            }
            //Add the final square (which needs doing manually as it does not have a square to the right).
            contents.Add(GetSquareContents(current));

            return contents;
        }

        public List<(int column, char row)> GetSurrounding((int column, char row) pos) {
            var oneUp = pos.row != 'A' ? (pos.column, rows[rows.IndexOf(pos.row) - 1]) : (-1, 'X');
            var oneDown = pos.row != 'O' ? (pos.column, rows[rows.IndexOf(pos.row) + 1]) : (-1, 'X');
            var oneLeft = pos.column != 1 ? (pos.column - 1, pos.row) : (-1, 'X');
            var oneRight = pos.column != 15 ? (pos.column + 1, pos.row) : (-1, 'X');

            //Diagonal
            var diagUpLeft = (-1, 'X');
            var diagDownLeft = (-1, 'X');
            var diagUpRight = (-1, 'X');
            var diagDownRight = (-1, 'X');
            if (!oneUp.Equals((-1, 'X')) && !oneLeft.Equals((-1, 'X'))) {
                diagUpLeft = (oneLeft.Item1, oneUp.Item2);
            }

            if (!oneDown.Equals((-1, 'X')) && !oneLeft.Equals((-1, 'X'))) {
                diagDownLeft = (oneLeft.Item1, oneDown.Item2);
            }

            if (!oneUp.Equals((-1, 'X')) && !oneRight.Equals((-1, 'X'))) {
                diagUpRight = (oneRight.Item1, oneUp.Item2);
            }

            if (!oneDown.Equals((-1, 'X')) && !oneRight.Equals((-1, 'X'))) {
                diagDownRight = (oneRight.Item1, oneDown.Item2);
            }

            (int, char)[] boundingSquares = { oneUp, oneDown, oneLeft, oneRight, diagUpLeft, diagUpRight, diagDownLeft, diagDownRight };

            List<(int column, char row)> surrounding = new List<(int column, char row)>();
            foreach (var bSquare in boundingSquares) {
                if (!bSquare.Equals((-1, 'X'))) {
                    surrounding.Add((bSquare.Item1, bSquare.Item2));
                }
            }

            return surrounding;
        }


        //Writing
        public void SetSquareContents((int column, char row) pos, char letter) {
            letters[pos] = letter;
        }

        public void ExecuteMove(DecisionMaker.Move move, MoveType type) { //Make the changes that would be made for a given move.
            if (move.Equals(DecisionMaker.Move.ERR)) return;
            var affected = Game.brain.AffectedSquares(move);
            for (int i = 0; i < affected.Count; i++) {
                //Console.Write(move.word[i]);
                SetSquareContents(affected[i], move.word[i]);
            }

            if (type == MoveType.Self) {
                Game.ownMoves.Add(move);
            } else {
                Game.opponentMoves.Add(move);
            }
        }
    }

    //Work out what to do.
    public class DecisionMaker {
        //For speeding up evaluation by reducing needless checks.
        
        public class Zone {
            private List<(int column, char row)> squares = new List<(int column, char row)>();

            public static List<Zone> FindEmptyZones() {
                List<(int column, char row)> upper = new List<(int column, char row)>();

                foreach(var square in Game.board.squares.Keys) {
                    if (Game.board.GetSquareContents(square) != ' ') goto doublebreak1;
                    foreach (var surrounding in Game.board.GetSurrounding(square)) {
                        //Break if there is something in the square.
                        if (Game.board.GetSquareContents(surrounding) == ' ') goto doublebreak1;
                    }
                    upper.Add(square);
                }

            doublebreak1:;
                List<(int column, char row)> lower = new List<(int column, char row)>();

                foreach (var square in Game.board.squares.Keys.Reverse()) {
                    if (Game.board.GetSquareContents(square) != ' ') goto doublebreak2;
                    foreach (var surrounding in Game.board.GetSurrounding(square)) {
                        //Break if there is something in the square.
                        if (Game.board.GetSquareContents(surrounding) == ' ') goto doublebreak2;
                    }
                    upper.Add(square);
                }

            doublebreak2:;

                return new List<Zone>(new Zone[] { new Zone() { squares = upper }, new Zone() { squares = lower } });
            }

            public bool Contains((int column, char row) pos) {
                return squares.Contains(pos);
            }
        }
        

        public class Move {
            public string word = "";
            public (int column, char row) firstLetterPos;
            public (int column, char row) lastLetterPos;
            public static Move ERR = new Move("", (-1, 'A'), (-1, 'A'));

            public Move(string wd, (int column, char row) first, (int column, char row) last) {
                word = wd;
                firstLetterPos = first;
                lastLetterPos = last;
            }
        }

        public enum Direction {
            Horizontal,
            Vertical
        }

        //Work out the direction of a move.
        public Direction GetDirection(Move move) {
            if (move.firstLetterPos.column == move.lastLetterPos.column) return Direction.Vertical;
            return Direction.Horizontal;
        }

        //Get the squares that a move will put letters on.
        public List<(int column, char row)> AffectedSquares(Move move) {
            //We only need to get the indexes for the first position because we can get the direction and length.
            int columnIndex = Game.board.columns.IndexOf(move.firstLetterPos.column);
            int rowIndex = Game.board.rows.IndexOf(move.firstLetterPos.row);
            int wordLength = move.word.Length;
            Direction dir = GetDirection(move);

            List<(int column, char row)> squares = new List<(int column, char row)>();
            if (dir == Direction.Vertical) {
                //Get all the squares on the board.
                List<(int column, char row)> boardPositions = Game.board.squares.Keys.ToList();

                int prevIndex = boardPositions.IndexOf(move.firstLetterPos);
                squares.Add(move.firstLetterPos);

                foreach (var position in boardPositions) {
                    if (boardPositions.IndexOf(position) == prevIndex + 15) {
                        if (squares.Count < wordLength) {
                            prevIndex = boardPositions.IndexOf(position);
                            squares.Add(position);
                        } else {
                            break; //No point continuing if there are no more letters in the word.
                        }
                    }
                }
            } else {
                //Get all the squares on the board.
                List<(int column, char row)> boardPositions = Game.board.squares.Keys.ToList();

                //Calculate the difference between the column of the first letter and the column of the last letter.
                int columnGap = Game.board.columns.IndexOf(move.lastLetterPos.column) - Game.board.columns.IndexOf(move.firstLetterPos.column);

                //Iterate over the board squares from the first letter to the last letter and add those.
                for (int i = boardPositions.IndexOf(move.firstLetterPos); i <= boardPositions.IndexOf(move.firstLetterPos) + columnGap; i++) {
                    squares.Add(boardPositions[i]);
                }
            }

            return squares;
        }

        //Returns the letters that we don't have that we need for a given move.
        List<char> LettersRequired(Move move) {
            List<char> needed = new List<char>();
            foreach (char letter in move.word) {
                if (!Game.letters.Contains(letter)) {
                    needed.Add(letter);
                }
            }

            return needed;
        }

        //Read the entire word created after a move has joined onto another word.
        string ReadWord(Move move, Direction direction) {
            if (direction == Direction.Horizontal) {
                StringBuilder left = new StringBuilder();
                StringBuilder right = new StringBuilder();

                var currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Left)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ') break;

                    left.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Left];
                }

                string leftRead = left.ToString();
                left = new StringBuilder(new string(leftRead.Reverse().ToArray()));

                currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Right)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ') break;

                    right.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Right];
                }

                string removd = right.Length > 0 ? right.Remove(0, 1).ToString() : right.ToString();
                left.Append(removd);
                return left.ToString();
            } else {
                StringBuilder up = new StringBuilder();
                StringBuilder down = new StringBuilder();

                var currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Up)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ') break;

                    up.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Up];
                }

                string upRead = up.ToString();
                up = new StringBuilder(new string(upRead.Reverse().ToArray()));

                currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Right)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ') break;

                    down.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Right];
                }
                string removd = down.Length > 0 ? down.Remove(0, 1).ToString() : down.ToString();
                up.Append(removd);
                return up.ToString();
            }

        }

        private Zone upperZone = new Zone();
        private Zone lowerZone = new Zone();

        private void RefreshZones() {
            List<Zone> empty = Zone.FindEmptyZones();
            upperZone = empty[0];
            lowerZone = empty[1];
        }

        //Quickly work out whether or not a move is worth evaluating fully.
        bool QuickEval(Move move) {
            //Check if the move will be into an empty zone.
            if (upperZone.Contains(move.firstLetterPos) && upperZone.Contains(move.lastLetterPos)) return false;
            if (lowerZone.Contains(move.firstLetterPos) && lowerZone.Contains(move.lastLetterPos)) return false;

            //Check if any of the squares surrounding the proposed move are occupied.
            var affected = AffectedSquares(move);
            int occupied = 0;
            foreach (var sq in affected) {
                var surrounding = Game.board.GetSurrounding(sq);
                foreach (var sqq in surrounding) {
                    if (Game.board.GetSquareContents(sqq) != ' ') {
                        occupied++;
                    }
                }
            }

            if (occupied == 0) return false;

            return true;
        }

        //A more extensive check for moves that will only be used if a move passes QuickEval().
        bool MoveIsPossible(Move move) {
            Direction moveDir = GetDirection(move);

            //Does it wrap around the board?
            if (moveDir == Direction.Vertical) {
                //Check if there are enough rows for the word.
                for (int i = Game.board.rows.IndexOf(move.firstLetterPos.row); i < move.word.Length; i++) {
                    if (Game.board.rows.Count <= i) {
                        //No more rows.
                        return false;
                    }
                }
            } else {
                //Check if there are enough columns for the word.
                for (int i = Game.board.columns.IndexOf(move.firstLetterPos.column); i < move.word.Length; i++) {
                    if (Game.board.columns.Count <= i) {
                        //No more columns.
                        return false;
                    }
                }
            }

            //Does it make a word that works?
            string horizontalWord = ReadWord(move, Direction.Horizontal);
            if (horizontalWord == null) goto skipH;
            if (!ScrabbleDictionary.words.Contains(horizontalWord.ToUpper().Trim(null))) {
                Console.WriteLine($"DEBUG: In word {move.word}: Created word {horizontalWord} is invalid.");
                return false;
            }

        skipH:
            string verticalWord = ReadWord(move, Direction.Vertical);
            if (verticalWord == null) goto skipV;
            if (!ScrabbleDictionary.words.Contains(verticalWord.ToUpper().Trim(null))) {
                Console.WriteLine($"DEBUG: In word {move.word}: Created word {verticalWord} is invalid.");
                return false;
            }
        skipV:
            //Does it fit with the letters that are already on the board?
            var affected = AffectedSquares(move);
            for (int i = 0; i < affected.Count; i++) {
                var pos = affected[i];
                if (Game.board.GetSquareContents(pos) == ' ') continue;
                if (Game.board.GetSquareContents(pos) != move.word[i]) return false;
            }

            //Does it connect to another word to create a valid one?
            int iters = 0;
            int emptyIters = 0;
            foreach (var thing in affected) {
                iters++;
                if (Game.board.GetSquareContents(thing) == ' ') emptyIters++;
            }
            //Check if the number of empty squares it affects == the total number of squares it affects.
            if (iters == emptyIters) return false;

            //A word can be played twice but not a move. (A move holds positioning data, so an identical move would be on top of another.)
            if (Game.ownMoves.Contains(move) || Game.opponentMoves.Contains(move)) return false;

            //Does it get the letters it needs?
            List<char> needed = LettersRequired(move);
            List<char> fullAvailable = new List<char>();
            foreach (char letter in Game.letters) {
                fullAvailable.Add(letter);
            }

            foreach (var sq in affected) {
                if (!Game.board.GetSquareContents(sq).Equals(' ')) {
                    fullAvailable.Add(Game.board.GetSquareContents(sq));
                }

            }

            bool gotAllNeeded = !needed.Except(fullAvailable).Any();
            if (!gotAllNeeded) {
                Console.WriteLine($"DEBUG: In word {move.word}: Needed {{{string.Join(", ", needed)}}}, only have {{{string.Join(", ", fullAvailable)}}}");
                return false;
            }

            //Have we used letters multiple times where we shouldn't have?
            Dictionary<char, int> legalUses = new Dictionary<char, int>();
            foreach (char letter in fullAvailable) {
                if (!legalUses.ContainsKey(letter)) {
                    legalUses[letter] = ((from temp in fullAvailable where temp.Equals(letter) select temp).Count());
                }
            }

            foreach (char letter in move.word) {
                int count = (from temp in move.word where temp.Equals(letter) select temp).Count();
                if (legalUses[letter] < count) {
                    Console.WriteLine($"DEBUG: In word {move.word}: Used letter {letter.ToString()} {count} times when the limit was {legalUses[letter]}.");
                    return false;
                }
            }

            //Did it escape the wrapping check?
            //if (move.firstLetterPos.column != move.lastLetterPos.column && move.firstLetterPos.row != move.lastLetterPos.row) return false;
            /*
            //Did it escape that one too?
            List<List<(int c, char r)>> lists = Game.board.squares.Keys.ToList().SplitList(15);
            if(moveDir == Direction.Horizontal) {
                bool found = false;
                foreach(var lst in lists) {
                    if(lst.Contains(move.firstLetterPos) && lst.Contains(move.lastLetterPos)) {
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    Console.WriteLine($"DEBUG: In word {move.word}: Horizontal placement spread over two rows.");
                    return false;
                }
            }
            */

            /* TO DO: Add other checks that must be completed. */

            //All checks passed.
            return true;
        }

        //Shift moves.
        private Move TranslateMove(Move move, Direction dir, int squares) {
            try {
                if (dir == Direction.Horizontal) {
                    int newColumnStart = Game.board.columns.IndexOf(move.firstLetterPos.column) + squares;
                    int newColumnEnd = Game.board.columns.IndexOf(move.lastLetterPos.column) + squares;
                    if (Game.board.columns.Count <= newColumnStart || Game.board.columns.Count <= newColumnEnd) {
                        return Move.ERR;
                    }
                    (int column, char row) newMoveStart = (Game.board.columns[newColumnStart],
                                                           move.firstLetterPos.row);
                    (int column, char row) newMoveEnd = (Game.board.columns[newColumnEnd],
                                                           move.lastLetterPos.row);

                    return new Move(move.word, newMoveStart, newMoveEnd);
                } else {
                    int newRowStart = Game.board.rows.IndexOf(move.firstLetterPos.row) + squares;
                    int newRowEnd = Game.board.rows.IndexOf(move.lastLetterPos.row) + squares;
                    if (Game.board.rows.Count <= newRowStart || Game.board.rows.Count <= newRowEnd) {
                        return Move.ERR;
                    }
                    (int column, char row) newMoveStart = (move.firstLetterPos.column,
                                                           Game.board.rows[newRowStart]);
                    (int column, char row) newMoveEnd = (move.lastLetterPos.column,
                                                         Game.board.rows[newRowEnd]);
                    return new Move(move.word, newMoveStart, newMoveEnd);
                }
            } catch {
                return Move.ERR;
            }
        }

        //Create a move at 1A that is the right length for the passed word.
        private Move CreateMove(string word) {
            (int column, char row) start = (1, 'A');
            (int column, char row) end = (word.Length, 'A');
            return new Move(word, start, end);
        }

        private Move CreateMove(string word, Direction dir) {
            if (dir == Direction.Horizontal) return CreateMove(word);

            var start = (1, 'A');
            var end = (1, Game.board.rows[word.Length - 1]);
            return new Move(word, start, end);
        }

        //Get all possible moves.
        private List<Move> PossibleMoves(out int considered) {
            //Refresh the zones so we make sure we don't discard any possible moves.
            RefreshZones();

            /*
             * Getting moves:
             *      1. Loop through words.
             *      2. Skip words where we need >2 more letters than we have.
             *      3. Create a base move for the word. Translate that move to every possible position.
             *      4. Check if the translated move would work.
             *      5. If it does, add it to a List.
            */

            Console.WriteLine("Initial search phase started...");

            int movesConsidered = 0;

            List<Move> possible = new List<Move>();

            Console.ForegroundColor = ConsoleColor.DarkRed;
            //Console.Write("Thinking... 0");

            Console.CursorVisible = false;
            void UpdateConsole(string wd) {
                //UpdateText($"Thinking... {movesConsidered} {wd}");
                //Console.Write("\r                                            ");
                //Console.Write("\r" + wd);
            }

            int bestScore = 0;



            Parallel.ForEach(ScrabbleDictionary.words, (word, stet) => {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape) {
                    stet.Break();
                }

                bool flip = false;
            flipTime:
                Move baseMove = flip ? CreateMove(word, Direction.Vertical) : CreateMove(word);
                List<char> required = LettersRequired(baseMove);
                if (required.Count > 1) {
                    return;
                }

                int score = Score(baseMove);
                if (score <= bestScore) return;
                bool brokeInf = false;
                Parallel.For(0, 15, (shift, state) => { //Horizontal shift loop.

                    Move shifted = TranslateMove(baseMove, Direction.Horizontal, shift);
                    movesConsidered++;
                    //UpdateConsole(baseMove.word);

                    //Break if the translation failed. (There was an error fitting the move onto the board.)
                    if (shifted.Equals(Move.ERR)) state.Break();

                    if (score <= bestScore) state.Break();

                    if (QuickEval(shifted)) {
                        if (MoveIsPossible(shifted)) {
                            if (score > bestScore)
                                bestScore = score;
                            possible.Add(shifted);
                        }
                    }
                    bool brokeInfinite = false;
                    if (score <= bestScore) return;
                    Parallel.For(0, 15, (downShift, nestedState) => { //Vertical shift loop.
                        if (score <= bestScore) state.Break();
                        Move downShifted = TranslateMove(shifted, Direction.Vertical, downShift);
                        movesConsidered++;
                        UpdateConsole(baseMove.word);

                        if (downShifted.Equals(Move.ERR)) nestedState.Break();

                        if (!QuickEval(downShifted)) return;
                        if (score <= bestScore) return;
                        if (score <= bestScore) state.Break();
                        if (score <= bestScore) nestedState.Break();
                        if (MoveIsPossible(downShifted)) {

                            int iters = 0;
                            int emptyIters = 0;
                            foreach (var thing in AffectedSquares(downShifted)) {
                                iters++;
                                if (Game.board.GetSquareContents(thing) == ' ') emptyIters++;
                            }

                            //if (score <= bestScore) return;


                            //Only add the move if it links into another word.
                            if (iters != emptyIters) {
                                if (score > bestScore)
                                    bestScore = score;
                                else
                                    return;
                                possible.Add(downShifted);
                            }
                        }
                    });
                    if (brokeInfinite) {
                        brokeInf = true;
                        state.Break();
                    }
                });
                if (brokeInf) {
                    return;
                }

                //Start again but vertically.
                //if(!flip) {
                //    flip = true;
                //    goto flipTime;
                //}

            });

            considered = movesConsidered;

            return possible;
        }

        Dictionary<char, int> points = new Dictionary<char, int>();

        private int Score(Move move) {
            if (points.Keys.Count < 1) {
                points = new Dictionary<char, int> {
                    { 'A', 1 },
                    { 'B', 3 },
                    { 'C', 3 },
                    { 'D', 2 },
                    { 'E', 1 },
                    { 'F', 4 },
                    { 'G', 2 },
                    { 'H', 4 },
                    { 'I', 1 },
                    { 'J', 8 },
                    { 'K', 5 },
                    { 'L', 1 },
                    { 'M', 3 },
                    { 'N', 1 },
                    { 'O', 1 },
                    { 'P', 3 },
                    { 'Q', 10 },
                    { 'R', 1 },
                    { 'S', 1 },
                    { 'T', 1 },
                    { 'U', 1 },
                    { 'V', 4 },
                    { 'W', 4 },
                    { 'X', 8 },
                    { 'Y', 4 },
                    { 'Z', 10 }
                };
            }

            int score = 0;
            foreach (char ch in move.word.ToUpper()) {
                score += points[ch];
            }

            return score;
        }

        public Move BestMove(out int considered) {
            List<Move> possible = PossibleMoves(out int cons);
            if (possible.Count < 1) {
                Console.WriteLine("No moves generated!");
                considered = cons;
                return Move.ERR;
            }

            Move best = possible[0];
            int bestScore = Score(best);

            Parallel.ForEach(possible, (move) => {
                cons++;
                int moveScore = Score(move);
                if (moveScore > bestScore) {
                    best = move;
                    bestScore = moveScore;
                }
            });

            considered = cons;
            return best;
        }
    }

    //Manage the Scrabble dictionary.
    static class ScrabbleDictionary {
        //Check words using the List, get definitions with the Dictionary.
        public static List<string> words = new List<string>();
        public static ConcurrentDictionary<string, string> definitions = new ConcurrentDictionary<string, string>();

        private static bool loaded = false;

        public static void LoadDictionaries(string ndefsPath, string defsPath) {
            if (!loaded) {
                

                //Console.WriteLine("Please enter the path to the ndefs file: ");
                words = File.ReadAllLines(/*"C:\\Users\\alexj\\source\\repos\\scrbl\\scrbl\\Properties\\nodefs.txt"*/ndefsPath).ToList();
                words.RemoveRange(0, 2); //Remove the title and the line after.

                //Console.WriteLine("Please enter the path to the defs file: ");
                List<string> defs = File.ReadAllLines(/*"C:\\Users\\alexj\\source\\repos\\scrbl\\scrbl\\Properties\\defs.txt"*/defsPath).ToList();
                defs.RemoveRange(0, 2);

                //Loop more efficiently in parallel.
                Parallel.ForEach(defs, (line) => {

                    /*
                     Examples:
                     AA	a volcanic rock consisting of angular blocks of lava with a very rough surface [n -S]
                     AAH "an interjection expressing surprise [interj] / to exclaim in surprise [v -ED, -ING, -S]"
                    */

                    //We need to split from the whitespace after the first word and remove quotes and strings in [].

                    //Separate the word from the definition.
                    string[] separated = line.Split(null, 2);
                    string word = "", def = "";
                    try {
                        word = separated[0];
                        def = separated[1];
                    } catch {
                        goto cont;
                    }

                    /* TO DO: Add the same root word but with the suffix given between square brackets (where present). */

                    //Remove the square brackets.
                    Regex squareBracketRegex = new Regex(@"\[([^\]]+)\]");
                    def = squareBracketRegex.Replace(def, "");

                    //Remove the quotes.
                    def = def.Replace("\"", "");

                    try {
                        definitions[word] = def;
                    } catch { }

                cont:;
                });

                loaded = true;
            }
        }
    }

    //Manage user interaction.
    static class InteractionManager {
        private static void LoadEverything() {
            PerformColor(ConsoleColor.DarkYellow, () => {
                Console.WriteLine("Loading dictionaries...");

                string currentPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                string nodefs = Path.Combine(currentPath, "nodefs.txt");

                if(!File.Exists(nodefs)) {
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

                Console.WriteLine($"Done! Loaded {/*276,643*/ScrabbleDictionary.words.Count} words in { watch.Elapsed.Milliseconds }ms.");
            });



        }

        private static List<string> BoardRepresentation() {
            //Define the different parts we need.
            string top = "┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐";
            string rowSeparator = "├───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┼───┤";
            string row = "│ {0} │ {1} │ {2} │ {3} │ {4} │ {5} │ {6} │ {7} │ {8} │ {9} │ {10} │ {11} │ {12} │ {13} │ {14} │";
            string bottom = "└───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘";

            //Add the top line.
            List<string> parts = new List<string> {
                top
            };

            //Split into rows (each row is 15 squares wide).
            List<List<(int column, char row)>> lists = Game.board.squares.Keys.ToList().SplitList(15);
            
            //Iterate over the lists of squares we made.
            foreach (var lst in lists) {
                List<char> chars = new List<char>();

                Console.WriteLine($"DEBUG: Row {lst[0].row} begins with square {lst[0].column.ToString() + lst[0].row} and ends with {lst.Last().column.ToString() + lst.Last().row}.");

                foreach (var pos in lst) {
                    chars.Add(Game.board.GetSquareContents(pos));
                }
                var strChars = chars.Select(c => c.ToString()).ToArray<object>();
                parts.Add(string.Format(row, strChars));
                parts.Add(rowSeparator);
            }
            
            /*
            foreach(char rowLetter in Game.board.rows) {
                List<char> contents = Game.board.GetRowContents(rowLetter);
                parts.Add(string.Format(row, contents.Select(c => c.ToString()).ToArray<object>()));
                parts.Add(rowSeparator);
            }
            */
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
            List<string> parts = input.Split(new char[] { ' ' }).ToList();

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
            }

            var move = ParseMove(input);
            Console.WriteLine($"DEBUG: {input} first column is {move.firstLetterPos.column}\nsecond column is {move.lastLetterPos.column}\n" +
                $"first row is {move.firstLetterPos.row}\n" +
                $"second row is {move.lastLetterPos.row}");
            return move;
        }

        private static string MoveToString(DecisionMaker.Move move) {
            string wd = move.word;
            string squareOne = move.firstLetterPos.column.ToString() + move.firstLetterPos.row.ToString();
            string squareTwo = move.lastLetterPos.column.ToString() + move.lastLetterPos.row.ToString();

            return $"{wd} {squareOne} {squareTwo}".ToLower();
        }

        public static void Run() {
            LoadEverything();
            //Game.letters.AddRange(new char[] { 'Y', 'G', 'H', 'A', 'E', 'I', 'L'});
            Console.Write("Please give me some letters: ");
            string letterInput = Console.ReadLine();
            Game.letters.AddRange(letterInput.ToUpper().ToCharArray());

            while (true) {
                try {
                    Game.board.ExecuteMove(GetMoveInput(), Board.MoveType.Opponent);
                    PrintBoard();

                    DecisionMaker.Move selfMove = null;
                    int considered = 0;
                    int time = Time(() => {
                        selfMove = Game.brain.BestMove(out considered);
                    });

                    PerformColor(ConsoleColor.Magenta, () => {
                        Console.WriteLine($"Considered {considered} moves in {time} seconds.");
                    });

                    Console.CursorVisible = true;

                    if (selfMove.Equals(DecisionMaker.Move.ERR)) continue;
                    Game.board.ExecuteMove(selfMove, Board.MoveType.Self);

                    PrintBoard();
                    Console.WriteLine(MoveToString(selfMove));

                    Console.Write($"{selfMove.word.ToLower()}: ");
                    PerformColor(ConsoleColor.DarkBlue, () => {
                        Console.Write($"'{ScrabbleDictionary.definitions[selfMove.word].Trim(null)}'");
                    });

                    Game.letters.Clear();

                    Console.Write("\n\nPlease give me some letters: ");
                    string letInp = Console.ReadLine();
                    Game.letters.AddRange(letInp.ToUpper().ToCharArray());

                    Console.WriteLine();
                } catch (Exception e) {
                    Console.WriteLine($"DEBUG: Encountered error: {e.ToString()}");
                }
            }
        }

        private static void Wait() {
            Console.ReadKey();
        }
    }

    static class Utils {
        public static List<List<T>> SplitList<T>(this List<T> me, int size) {
            var list = new List<List<T>>();
            for (int i = 0; i < me.Count; i += size)
                list.Add(me.GetRange(i, Math.Min(size, me.Count - i)));
            return list;
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
            var watch = Stopwatch.StartNew();
            action.Invoke();
            watch.Stop();
            return watch.Elapsed.Seconds;
        }

        static string currentText = "";
        public static void UpdateText(string text) {
            // Get length of common portion
            int commonPrefixLength = 0;
            int commonLength = Math.Min(currentText.Length, text.Length);
            while (commonPrefixLength < commonLength && text[commonPrefixLength] == currentText[commonPrefixLength]) {
                commonPrefixLength++;
            }

            // Backtrack to the first differing character
            StringBuilder outputBuilder = new StringBuilder();
            outputBuilder.Append('\b', currentText.Length - commonPrefixLength);

            // Output new suffix
            outputBuilder.Append(text.Substring(commonPrefixLength));

            // If the new text is shorter than the old one: delete overlapping characters
            int overlapCount = currentText.Length - text.Length;
            if (overlapCount > 0) {
                outputBuilder.Append(' ', overlapCount);
                outputBuilder.Append('\b', overlapCount);
            }

            Console.Write(outputBuilder);
            currentText = text;
        }
    }

}


