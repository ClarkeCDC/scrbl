using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static scrbl.Utils;
// ReSharper disable All

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
        public readonly HashSet<char> placedLetters = new HashSet<char>();

        void LoadSquares() {
            if (squares.Keys.Count < 1) {
                //Fill a List with normal squares. We add special ones later.
                int squareCount = rows.Count * columns.Count;
                List<Square> squareList = Enumerable.Repeat(Square.Normal, squareCount).ToList();

                //Add the middle.
                squareList[squareCount / 2] = Square.Middle;

                /* TO DO: Add the other special squares here. */

                //These co-ordinates are (columnNumber, rowIndex + 1)
                List<(int, int)> tWords = new (int, int)[] { //Triple wd
                    (1, 1),
                    (8, 1),
                    (15, 1),
                    (1, 8),
                    (15, 8),
                    (1, 15),
                    (8, 15),
                    (15, 15)
                }.ToList();

                List<(int, int)> dLtrs = new (int, int)[] { //Double ltr
                    (4, 1),
                    (12, 1),
                    (7, 3),
                    (9, 3),
                    (8, 4),
                    (1, 4),
                    (15, 4),
                    (3, 7),
                    (7, 7),
                    (9, 7),
                    (13, 7),
                    (4, 8),
                    (12, 8),
                    (3, 9),
                    (7, 9),
                    (9, 9),
                    (13, 9),
                    (1, 12),
                    (8, 12),
                    (15, 12),
                    (7, 13),
                    (9, 13),
                    (4, 15),
                    (12, 15)
                }.ToList();

                List<(int, int)> tLtrs = new (int, int)[] { //Triple ltr
                    (6, 2),
                    (10, 2),
                    (2, 6),
                    (6, 6),
                    (10, 6),
                    (14, 6),
                    (2, 10),
                    (6, 10),
                    (10, 10),
                    (14, 10),
                    (6, 14),
                    (10, 14)
                }.ToList();

                List<(int, int)> dWords = new (int, int)[] { //Double words
                    (2, 2),
                    (3, 3),
                    (4, 4),
                    (5, 5),
                    (14, 2),
                    (13, 3),
                    (12, 4),
                    (11, 5),
                    (5, 11),
                    (4, 12),
                    (3, 13),
                    (2, 14),
                    (11, 11),
                    (12, 12),
                    (13, 13),
                    (14, 14)
                }.ToList();

                (int column, char row) CoordinateToSquare((int, int) co) {
                    return (co.Item1, rows[co.Item2 - 1]);
                }

                int listIndex = 0;
                foreach (char row in rows) {
                    foreach (int column in columns) {
                        squares[(column, row)] = squareList[listIndex];
                        listIndex++;
                    }
                }

                foreach (var co in tWords) {
                    squares[CoordinateToSquare(co)] = Square.TripleWord;
                }

                foreach (var co in tLtrs) {
                    squares[CoordinateToSquare(co)] = Square.TripleLetter;
                }

                foreach (var co in dWords) {
                    squares[CoordinateToSquare(co)] = Square.DoubleWord;
                }

                foreach (var co in dLtrs) {
                    squares[CoordinateToSquare(co)] = Square.DoubleLetter;
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
        }

        //Get the squares surrounding the passed one and return a dictionary.
        public Dictionary<RelativePosition, (int column, char row)> GetSurroundingDict((int column, char row) pos) {
            var oneUp = pos.row != 'A' ? (pos.column, rows[rows.IndexOf(pos.row) - 1]) : (-1, 'X');
            var oneDown = pos.row != 'O' ? (pos.column, rows[rows.IndexOf(pos.row) + 1]) : (-1, 'X');
            var oneLeft = pos.column != 1 ? (pos.column - 1, pos.row) : (-1, 'X');
            var oneRight = pos.column != 15 ? (pos.column + 1, pos.row) : (-1, 'X');

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

            return dict;
        }

        public List<(int column, char row)> GetSurrounding((int column, char row) pos) {
            var oneUp = pos.row != 'A' ? (pos.column, rows[rows.IndexOf(pos.row) - 1]) : (-1, 'X');
            var oneDown = pos.row != 'O' ? (pos.column, rows[rows.IndexOf(pos.row) + 1]) : (-1, 'X');
            var oneLeft = pos.column != 1 ? (pos.column - 1, pos.row) : (-1, 'X');
            var oneRight = pos.column != 15 ? (pos.column + 1, pos.row) : (-1, 'X');

            (int, char)[] boundingSquares = { oneUp, oneDown, oneLeft, oneRight };

            List<(int column, char row)> surrounding = new List<(int column, char row)>();
            foreach (var bSquare in boundingSquares) {
                if (!bSquare.Equals((-1, 'X'))) {
                    surrounding.Add((bSquare.Item1, bSquare.Item2));
                }
            }

            return surrounding;
        }

        public List<(int column, char row)> GetRow(char row) {
            List<(int column, char row)> found = new List<(int column, char row)>();

            int foundNumber = 0;
            foreach (var sq in squares.Keys) {
                if (foundNumber > 14) break;
                if (sq.row == row) {
                    found.Add(sq);
                    foundNumber++;
                }
            }

            return found;
        }

        public List<(int column, char row)> GetColumn(int column) {
            List<(int column, char row)> found = new List<(int column, char row)>();

            int foundNumber = 0;
            foreach (var sq in squares.Keys) {
                if (foundNumber > 14) break;
                if (sq.column == column) {
                    found.Add(sq);
                    foundNumber++;
                }
            }

            return found;
        }

        //Writing
        public void SetSquareContents((int column, char row) pos, char letter) {
            letters[pos] = letter;
        }

        public void ExecuteMove(DecisionMaker.Move move, MoveType type) { //Update the board for a move.
            if (move.Equals(DecisionMaker.Move.ERR)) return;
            var affected = Game.brain.AffectedSquares(move);
            for (int i = 0; i < affected.Count; i++) {
                placedLetters.Add(move.word[i]);
                SetSquareContents(affected[i], move.word[i]);
            }

            if (type == MoveType.Self) {
                Game.ownMoves.Add(move);
            } else {
                Game.opponentMoves.Add(move);
            }
        }

        public void ReloadPremiums() { //Remove any premium squares that have been used.
            foreach (var sq in squares.Keys) {
                if (GetSquareContents(sq) != ' ') {
                    squares[sq] = Square.Normal;
                }
            }
        }
    }

    //Work out what to do.
    public class DecisionMaker {
        //For speeding up evaluation by reducing needless checks.

        public class Zone {
            public List<(int column, char row)> squares = new List<(int column, char row)>();

            public static List<Zone> FindEmptyZones() {
                List<(int column, char row)> upper = new List<(int column, char row)>();

                foreach (var square in Game.board.squares.Keys) {
                    if (Game.board.GetSquareContents(square) != ' ') goto doublebreak1;
                    foreach (var surrounding in Game.board.GetSurrounding(square)) {
                        //Break if there is something in the square.
                        if (Game.board.GetSquareContents(surrounding) == ' ') goto doublebreak1;
                    }
                    upper.Add(square);
                }

            doublebreak1:
                List<(int column, char row)> lower = new List<(int column, char row)>();

                foreach (var square in Game.board.squares.Keys.Reverse()) {
                    if (Game.board.GetSquareContents(square) != ' ') goto doublebreak2;
                    foreach (var surrounding in Game.board.GetSurrounding(square)) {
                        //Break if there is something in the square.
                        if (Game.board.GetSquareContents(surrounding) == ' ') goto doublebreak2;
                    }
                    upper.Add(square);
                }

            doublebreak2:

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

        //Cache the affected squares for moves that go from one square to another.
        //Move objects are not stored (so less memory is used since strings are not stored), just positions.
        //The class is used as if it stores the moves, however.
        class AffectedCache {
            //private static ConcurrentDictionary<List<(int, char)>, List<(int column, char row)>> dict = new ConcurrentDictionary<List<(int, char)>, List<(int column, char row)>>();
            //static MemoryCache<List<(int, char)>, List<(int column, char row)>> cache = new MemoryCache<List<(int, char)>, List<(int column, char row)>>();
            private ConcurrentDictionary<(int, char)[], (int column, char row)[]> dict = new ConcurrentDictionary<(int, char)[], (int column, char row)[]>();
            
            public List<(int column, char row)> Get(Move move) {
                (int, char)[] key = { move.firstLetterPos, move.lastLetterPos };
                return dict[key].ToList();
            }

            public void Add(Move move, List<(int, char)> squares) {
                (int, char)[] key = { move.firstLetterPos, move.lastLetterPos };
                dict[key] = squares.ToArray();
            }

            public bool Contains(Move move) {
                (int, char)[] key = { move.firstLetterPos, move.lastLetterPos };
                return dict.ContainsKey(key);
            }
        }

        private AffectedCache cache = new AffectedCache();

        //Get the squares that a move will put letters on.
        public List<(int column, char row)> AffectedSquares(Move move) {
            //See if we have the move's position's squares in the cache.
            //if (cache.Contains(move)) {
            //    Console.WriteLine("Cached");
            //    return cache.Get(move);
            //}

            int wordLength = move.word.Length;
            Direction dir = GetDirection(move);

            List<(int column, char row)> squares = new List<(int column, char row)>();

            if (dir == Direction.Vertical) {
                for(int i = 0; i < wordLength; i++) {
                    (int column, char row) square = (move.firstLetterPos.column, 
                                                     Game.board.rows[Game.board.rows.IndexOf(move.firstLetterPos.row) + i]);
                    squares.Add(square);
                }
            } else {
                for (int i = 0; i < wordLength; i++) {
                    (int column, char row) square = (Game.board.columns[Game.board.columns.IndexOf(move.firstLetterPos.column) + i],
                                                     move.firstLetterPos.row);
                    squares.Add(square);
                }
            }
            
            //Check again because another thread could have cached the squares.
            //if(!cache.Contains(move)) {
            //    cache.Add(move, squares);
            //}
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
            

            //return move.word.ToCharArray().Except(Game.board.placedLetters).ToList();
            //return Game.board.placedLetters.Except(move.word.ToCharArray()).ToList();
            return needed;
        }

        //Get all the words in one line (the direction of which is determined by the readDirection param).
        List<string> ReadLine(Move move, Direction readDirection) {
            List<string> found = new List<string>();
            var affected = AffectedSquares(move);

            StringBuilder bobTheBuilder = new StringBuilder();
            if (readDirection == Direction.Horizontal) {
                foreach (var pos in affected) {
                    var squaresOnRow = Game.board.GetRow(pos.row);
                    foreach (var sq in squaresOnRow) {
                        char contents = Game.board.GetSquareContents(sq);
                        if (affected.Contains(sq)) {
                            contents = move.word[affected.IndexOf(sq)];
                        }
                        bobTheBuilder.Append(contents);
                    }
                }
            } else {
                foreach (var pos in affected) {
                    var squaresInColumn = Game.board.GetColumn(pos.column);
                    foreach (var sq in squaresInColumn) {
                        char contents = Game.board.GetSquareContents(sq);
                        if (affected.Contains(sq)) {
                            contents = move.word[affected.IndexOf(sq)];
                        }
                        bobTheBuilder.Append(contents);
                    }
                }
            }
            found = bobTheBuilder.ToString().Split(null).ToList();

            return found;
        }


        //Read the entire word created after a move has joined onto another word.
        string ReadWord(Move move, Direction direction) {
            var affected = AffectedSquares(move);

            if (direction == Direction.Horizontal) {
                StringBuilder left = new StringBuilder();
                StringBuilder right = new StringBuilder();

                var currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Left)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.word[affected.IndexOf(currentSquare)];
                    }

                    left.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Left];
                }

                string leftRead = left.ToString();
                left = new StringBuilder(new string(leftRead.Reverse().ToArray()));

                currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Right)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.word[affected.IndexOf(currentSquare)];
                    }

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
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.word[affected.IndexOf(currentSquare)];
                    }

                    up.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Up];
                }

                string upRead = up.ToString();
                up = new StringBuilder(new string(upRead.Reverse().ToArray()));

                currentSquare = move.firstLetterPos;
                while (Game.board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Down)) {
                    char contents = Game.board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.word[affected.IndexOf(currentSquare)];
                    }

                    down.Append(contents);
                    currentSquare = Game.board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Down];
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
            //Check if the number of letters we don't have is too many.
            if (LettersRequired(move).Count > 1) return false;

            //Check if the move has already been played.
            if (Game.ownMoves.Contains(move) || Game.opponentMoves.Contains(move)) return false;

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

        //Quick eval for base moves. (i.e. the checks are not position-related.)
        bool QuickEvalBase(Move baseMove) {
            if (LettersRequired(baseMove).Count > 1) return false;

            //Check if the letters we need are on the board.
            //This provides a speed boost at the start of the game.
            var needed = LettersRequired(baseMove);

            if (needed.Except(Game.board.placedLetters).Any()) return false;
            return true;
        }

        /* OPTIMISE THIS */
        //A more extensive check for moves that will only be used if a move passes QuickEval().
        bool MoveIsPossible(Move move) {
            /*
            All checks here are placed in order of effectiveness (on a single run with the letters QWERTYU how many words were caught by each).
            */

            Direction moveDir = GetDirection(move);
            var affected = AffectedSquares(move);

            //Does it connect to another word to create a valid one?
            int iters = 0;
            int emptyIters = 0;
            foreach (var thing in affected) {
                iters++;
                if (Game.board.GetSquareContents(thing) == ' ') emptyIters++;
            }

            //Check if the number of empty squares it affects == the total number of squares it affects.
            if (iters == emptyIters) {
                return false;
            }

            //Does it fit with the letters that are already on the board?
            for (int i = 0; i < affected.Count; i++) {
                var pos = affected[i];
                if (Game.board.GetSquareContents(pos) == ' ') continue;
                if (Game.board.GetSquareContents(pos) != move.word[i]) {
                    return false;
                }
            }

            //Does it make a word that works?
            string horizontalWord = ReadWord(move, Direction.Horizontal);
            if (string.IsNullOrWhiteSpace(horizontalWord) || horizontalWord.Length < 2) {
                goto skipH;
            }
            if (!ScrabbleDictionary.words.Contains(horizontalWord.ToUpper().Trim(null))) {
                Console.WriteLine($"DEBUG: In word {move.word}: Created word {horizontalWord} is invalid.");
                return false;
            }

        skipH:
            string verticalWord = ReadWord(move, Direction.Vertical);
            if (string.IsNullOrWhiteSpace(verticalWord) || verticalWord.Length < 2) {
                goto skipV;
            }
            if (!ScrabbleDictionary.words.Contains(verticalWord.ToUpper().Trim(null))) {
                Console.WriteLine($"DEBUG: In word {move.word}: Created word {verticalWord} is invalid.");
                return false;
            }

        skipV:
            //Check some more words.
            List<string> hWords = ReadLine(move, Direction.Horizontal);
            foreach (var word in hWords) {
                if (word.Length < 2) continue;
                if (!ScrabbleDictionary.words.Contains(word.ToUpper().Trim(null))) {
                    return false;
                }
            }

            List<string> vWords = ReadLine(move, Direction.Vertical);
            foreach (var word in vWords) {
                if (word.Length < 2) continue;
                if (!ScrabbleDictionary.words.Contains(word.ToUpper().Trim(null))) {
                    return false;
                }
            }

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

            //A word can be played twice but not a move. (A move holds positioning data, so an identical move would be on top of another.)
            if (Game.ownMoves.Contains(move) || Game.opponentMoves.Contains(move)) {
                return false;
            }

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
        private List<Move> PossibleMoves(out int considered)
        {
            //Refresh the zones so we make sure we don't discard any possible moves.
            RefreshZones();

            /*
             * Getting moves:
             *      1. Loop through words.
             *      2. Skip words where we need >1 more letters than we have. (This can be changed in the config file.)
             *      3. Create a base move for the word. Translate that move to every possible position.
             *      4. Check if the translated move would work.
             *      5. If it does, add it to a List.
            */

            int movesConsidered = 0;

            var possible = new List<Move>();

            Console.ForegroundColor = ConsoleColor.DarkRed;

            Console.CursorVisible = false;

            int bestScore = 0;

            Parallel.ForEach(ScrabbleDictionary.words, (word, stet) =>
            {
                movesConsidered++;
                //if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                //{
                //    stet.Break();
                //}

                bool flip = false;
                flipTime:
                Move baseMove = flip ? CreateMove(word, Direction.Vertical) : CreateMove(word);
                if (!QuickEvalBase(baseMove)) return;

                List<char> required = LettersRequired(baseMove);

                //This should be configurable.
                if (required.Count > 1)
                {
                    return;
                }

                /* TO DO: Clean up all of this. */

                //This will have to be calculated for every shifted move once the special squares are added.
                //int score = Score(baseMove);
                //if (score <= bestScore) return;

                Parallel.For(0, 15, (shift, state) =>
                {
                    //Horizontal shift loop.
                    Move shifted = TranslateMove(baseMove, Direction.Horizontal, shift);
                    movesConsidered++;

                    //Break if the translation failed. (There was an error fitting the move onto the board.)
                    if (shifted.Equals(Move.ERR)) state.Break();

                    //if (score <= bestScore) state.Break();

                    if (QuickEval(shifted))
                    {
                        if (MoveIsPossible(shifted))
                        {
                            int score = Score(shifted);
                            if (score > bestScore)
                                bestScore = score;
                            possible.Add(shifted);
                        }
                    }

                    //if (score <= bestScore) state.Break();

                    Parallel.For(0, 15, (downShift, nestedState) =>
                    {
                        //Vertical shift loop.
                        //if (score <= bestScore) state.Break();

                        Move downShifted = TranslateMove(shifted, Direction.Vertical, downShift);
                        movesConsidered++;

                        if (downShifted.Equals(Move.ERR)) nestedState.Break();

                        if (!QuickEval(downShifted)) return;
                        //if (score <= bestScore) state.Break();
                        if (MoveIsPossible(downShifted))
                        {
                            int iters = 0;
                            int emptyIters = 0;
                            foreach (var thing in AffectedSquares(downShifted))
                            {
                                iters++;
                                if (Game.board.GetSquareContents(thing) == ' ') emptyIters++;
                            }

                            //if (score <= bestScore) return;

                            int score = Score(downShifted);
                            //Only add the move if it links into another word.
                            if (iters != emptyIters)
                            {
                                if (score > bestScore)
                                    bestScore = score;
                                else
                                    return;
                                possible.Add(downShifted);
                            }
                        }
                    });
                });
                //Start again but vertically.
                if (!flip)
                {
                    flip = true;
                    goto flipTime;
                }
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

            /*
             * Premium Word Squares: 
             * The score for an entire word is doubled when one of its letters is placed on a pink square: it is tripled when one of its letters is placed on a red square. 
             * Include premiums for double or triple letter values, if any, before doubling or tripling the word score. 
             * If a word is formed that covers two premium word squares, the score is doubled and then re-doubled (4 times the letter count), or tripled and then re-tripled (9 times the letter count). 
             * NOTE: the center square is a pink square, which doubles the score for the first word.
            */

            var affected = AffectedSquares(move);

            int doubleWordHits = 0;
            int tripleWordHits = 0;

            int score = 0;
            for (int i = 0; i < affected.Count; i++) {
                var squareType = Game.board.GetSquare(affected[i]);

                switch (squareType) {
                    case Board.Square.Middle:
                        score += points[move.word[i]];
                        doubleWordHits++;
                        break;
                    case Board.Square.DoubleLetter:
                        score += points[move.word[i]] * 2;
                        break;
                    case Board.Square.TripleLetter:
                        score += points[move.word[i]] * 3;
                        break;
                    case Board.Square.DoubleWord:
                        score += points[move.word[i]];
                        doubleWordHits++;
                        break;
                    case Board.Square.TripleWord:
                        score += points[move.word[i]];
                        tripleWordHits++;
                        break;
                    default:
                        score += points[move.word[i]];
                        break;
                }
            }

            if (doubleWordHits > 0) {
                //By multiplying doubleWordHits by 2 we get how much we need to multiply the word by: 1 would be 2, 2 would be 4 (see the rules above).
                score *= doubleWordHits * 2;
            } else if (tripleWordHits > 0) {
                score *= tripleWordHits * 3;
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
            
            //return SelectMove(out considered);
        }
    }

    //Manage the Scrabble dictionary.
    static class ScrabbleDictionary {
        //Check words using the List, get definitions with the Dictionary.
        //public static List<string> words = new List<string>();
        public static ConcurrentDictionary<string, string> definitions = new ConcurrentDictionary<string, string>();
        public static HashSet<string> words = new HashSet<string>();
        private static bool loaded = false;

        public static void LoadDictionaries(string ndefs, string defs) {
            if (!loaded) {
                words = new HashSet<string>(File.ReadAllLines(ndefs));
                words.Remove(words.First());
                words.Remove(words.First());

                List<string> defsList = File.ReadAllLines(defs).ToList();
                Parallel.ForEach(defsList, (line) => {
                    string[] separated = line.Split(null, 2);
                    
                    if (separated.Length < 2) {
                        return; //Skip
                    }

                    string word = separated[0];
                    string def = separated[1];

                    Regex squareBracketRegex = new Regex(@"\[([^\]]+)\]");
                    def = squareBracketRegex.Replace(def, "");
                    def = def.Replace("\"", "");

                    definitions.TryAdd(word, def);
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

                Console.WriteLine($"Done! Loaded {/*276,643*/ScrabbleDictionary.words.Count} words in { watch.Elapsed.Milliseconds }ms.");
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
            List<List<(int column, char row)>> lists = Game.board.squares.Keys.ToList().SplitList(15);

            //Iterate over the list of lists of squares we made.
            foreach (var lst in lists) {
                List<char> chars = new List<char>();

                foreach (var pos in lst) {
                    chars.Add(Game.board.GetSquareContents(pos));
                }

                //Turn the list of chars into an array of strings that is officially an array of objects.
                var strChars = chars.Select(c => c.ToString()).ToArray<object>();
                parts.Add(lst[0].row.ToString() + " " + string.Format(row, strChars));
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
            Console.Write("Letters: ");
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

                    Console.Write("\n\nLetters: ");
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

        public static bool FastContains<T>(this List<T> me, object obj) {
            return me.Any((ob) => { return ob.Equals(obj); });
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

    //Get info from the configuration file.
    public class Configuration {
        /* IMPLEMENT THIS */
        private static Dictionary<string, object> configuration = new Dictionary<string, object>();
        public static void LoadConfig() {
            string dir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string configPath = Path.Combine(dir, "config.txt");
            if (File.Exists(configPath)) {

            }
        }
    }

}