using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using C5;
using static scrbl.Utils;

namespace scrbl {
    public class DecisionMaker {
        //For speeding up evaluation by reducing needless checks.

        //This never actually worked in the first place. Needs rewriting.
        public class Zone {
            //public HashSet<(int column, char row)> Squares = new HashSet<(int column, char row)>();

            public static List<Zone> FindEmptyZones() {
                return new List<Zone>(new[] { new Zone() {/* Squares = upper */}, new Zone {/* Squares = lower */} });
            }

            public bool Contains((int column, char row) pos) {
                return false;//Squares.FastContains(pos);
            }
        }


        public class Move {
            public string Word;
            public (int column, char row) FirstLetterPos;
            public (int column, char row) LastLetterPos;
            public static Move Err = new Move("", (-1, 'A'), (-1, 'A'));

            public Move(string wd, (int column, char row) first, (int column, char row) last) {
                Word = wd;
                FirstLetterPos = first;
                LastLetterPos = last;
            }
        }

        public enum Direction {
            Horizontal,
            Vertical
        }

        //Work out the direction of a move.
        public Direction GetDirection(Move move) {
            return move.FirstLetterPos.column == move.LastLetterPos.column ? Direction.Vertical : Direction.Horizontal;
        }

        //Get the squares that a move will put Letters on.
        public List<(int column, char row)> AffectedSquares(Move move) {
            int wordLength = move.Word.Length;
            Direction dir = GetDirection(move);

            var squares = new List<(int column, char row)>();

            if (dir == Direction.Vertical) {
                for (int i = 0; i < wordLength; i++) {
                    (int column, char row) square = (move.FirstLetterPos.column,
                        Game.Board.Rows[Game.Board.Rows.IndexOf(move.FirstLetterPos.row) + i]);
                    squares.Add(square);
                }
            } else {
                for (int i = 0; i < wordLength; i++) {
                    (int column, char row) square = (Game.Board.Columns[Game.Board.Columns.IndexOf(move.FirstLetterPos.column) + i],
                        move.FirstLetterPos.row);
                    squares.Add(square);
                }
            }

            return squares;
        }

        //Returns the Letters that we don't have that we need for a given move.
        private List<char> LettersRequired(Move move) {
            var needed = new List<char>();

            //Speed is very important here, hence the use of a for loop and FastContains().
            for (int i = 0; i < move.Word.Length; i++) {
                if (!Game.Letters.FastContains(move.Word[i])) {
                    needed.Add(move.Word[i]);
                }
            }

            if (Game.BlankCount < 1) {
                return needed;
            } else {
                //WHYYYYY
                var affected = AffectedSquares(move);
                if (affected.Count < 1) return needed;
                var lowest = affected[0];
                int lowestWorth = 42563;
                //Find the lowest value letter - we will replace this with the blank.
                for (int i = 0; i < affected.Count; i++) {
                    int score = Score(new Move(move.Word[i].ToString(), affected[i], affected[i]));
                    if (score < lowestWorth) {
                        lowest = affected[i];
                        lowestWorth = score;
                    }
                }

                needed.Remove(move.Word[affected.IndexOf(lowest)]);
            }

            return needed;
        }

        //Get all the words in one line (the direction of which is determined by the readDirection param).
        private List<string> ReadLine(Move move, Direction readDirection) {
            var affected = AffectedSquares(move);

            var bobTheBuilder = new StringBuilder();
            switch (readDirection) {
                case Direction.Horizontal: {
                    foreach ((int _, char row) in affected) {
                        var squaresOnRow = Game.Board.GetRow(row);
                        foreach (var sq in squaresOnRow) {
                            char contents = Game.Board.GetSquareContents(sq);
                            if (affected.Contains(sq)) {
                                contents = move.Word[affected.IndexOf(sq)];
                            }
                            bobTheBuilder.Append(contents);
                        }
                    }

                    break;
                }
                case Direction.Vertical:
                    foreach (var pos in affected) {
                        var squaresInColumn = Game.Board.GetColumn(pos.column);
                        foreach (var sq in squaresInColumn) {
                            char contents = Game.Board.GetSquareContents(sq);
                            if (affected.Contains(sq)) {
                                contents = move.Word[affected.IndexOf(sq)];
                            }
                            bobTheBuilder.Append(contents);
                        }
                    }

                    break;
            }
            List<string> found = bobTheBuilder.ToString().Split(null).ToList();

            return found;
        }

        //Read the entire word created after a move has joined onto another word.
        private string ReadWord(Move move, Direction direction) {
            List<(int column, char row)> affected = AffectedSquares(move);

            switch (direction) {
                case Direction.Horizontal:
                    //Get all the squares in the row.
                    var rowSquares = Game.Board.GetRow(move.FirstLetterPos.row);

                    var rowBuilder = new StringBuilder();
                    for (int i = 0; i < rowSquares.Count; i++) {
                        //If the square will be changed by the move, use the move's letter. Otherwise, use the existing contents.
                        rowBuilder.Append(affected.FastContains(rowSquares[i])
                            ? move.Word[affected.IndexOf(rowSquares[i])]
                            : Game.Board.GetSquareContents(rowSquares[i]));
                    }

                    return rowBuilder.ToString().Trim(null);

                case Direction.Vertical:
                    //Get all the squares in the column.
                    var columnSquares = Game.Board.GetColumn(move.FirstLetterPos.column);

                    var columnBuilder = new StringBuilder();
                    for (int i = 0; i < columnSquares.Count; i++) {
                        columnBuilder.Append(affected.FastContains(columnSquares[i])
                            ? move.Word[affected.IndexOf(columnSquares[i])]
                            : Game.Board.GetSquareContents(columnSquares[i]));
                    }

                    return columnBuilder.ToString().Trim(null);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private Zone _upperZone = new Zone();
        private Zone _lowerZone = new Zone();

        private void RefreshZones() {
            List<Zone> empty = Zone.FindEmptyZones();
            _upperZone = empty[0];
            _lowerZone = empty[1];
        }

        //Quickly work out whether or not a move is worth evaluating fully.
        private bool QuickEval(Move move) {
            //Check if the number of letters we don't have is too many.
            if (LettersRequired(move).Count > 1) return false;

            //Check if the move has already been played.
            if (Game.OwnMoves.Contains(move) || Game.OpponentMoves.Contains(move)) return false;

            //Check if the move will be into an empty zone.
            if (_upperZone.Contains(move.FirstLetterPos) && _upperZone.Contains(move.LastLetterPos)) {
                Console.WriteLine("DEBUG: Bad zone.");
                return false;
            }

            if (_lowerZone.Contains(move.FirstLetterPos) && _lowerZone.Contains(move.LastLetterPos)) {
                Console.WriteLine("DEBUG: Bad zone.");
                return false;
            }

            //Check if any of the squares surrounding the proposed move are occupied.
            var affected = AffectedSquares(move);
            int occupied = 0;
            foreach (var sq in affected) {
                var surrounding = Game.Board.GetSurrounding(sq);
                foreach (var sqq in surrounding) {
                    if (Game.Board.GetSquareContents(sqq) != ' ') {
                        occupied++;
                    }
                }
            }

            if (occupied == 0) return false;
            if (occupied == affected.Count) return false;

            //Keep this last.
            for (int i = 0; i < affected.Count; i++) {
                if (Game.Board.GetSquareContents(affected[i]) != ' ') goto ded;
            }
            return false;

            ded:

            return true;
        }

        //Quick eval for base moves. (i.e. the checks are not position-related.)
        bool QuickEvalBase(Move baseMove) {
            if (LettersRequired(baseMove).Count > 1) return false;

            //Check if the Letters we need are on the board.
            //This provides a speed boost at the start of the game.
            var needed = LettersRequired(baseMove);

            if (needed.Except(Game.Board.PlacedLetters).Any()) return false;
            return true;
        }

        //A more extensive check for moves that will only be used if a move passes QuickEval().
        bool MoveIsPossible(Move move) {
            /*
            All checks here are placed in order of effectiveness (on a single run with the Letters QWERTYU how many words were caught by each).
            */

            Direction moveDir = GetDirection(move);
            var affected = AffectedSquares(move);

            //Does it connect to another word to create a valid one?
            int iters = 0;
            int emptyIters = 0;
            foreach (var thing in affected) {
                iters++;
                if (Game.Board.GetSquareContents(thing) == ' ') emptyIters++;
            }

            //Check if the number of empty squares it affects == the total number of squares it affects.
            if (iters == emptyIters) {
                return false;
            }

            //Does it fit with the Letters that are already on the board?
            for (int i = 0; i < affected.Count; i++) {
                var pos = affected[i];
                if (Game.Board.GetSquareContents(pos) == ' ') continue;
                if (Game.Board.GetSquareContents(pos) != move.Word[i]) {
                    return false;
                }
            }

            //Does it make a word that works?
            string horizontalWord = ReadWord(move, Direction.Horizontal);
            if (string.IsNullOrWhiteSpace(horizontalWord) || horizontalWord.Length < 2) {
                goto skipH;
            }
            if (!ScrabbleDictionary.Words.Contains(horizontalWord.ToUpper().Trim(null))) {
                //Console.WriteLine($"DEBUG: In word {move.word}: Created word {horizontalWord} is invalid.");
                Console.WriteLine($"DEBUG: {move.Word} -> {horizontalWord} X");
                return false;
            }

            skipH:
            string verticalWord = ReadWord(move, Direction.Vertical);
            if (string.IsNullOrWhiteSpace(verticalWord) || verticalWord.Length < 2) {
                goto skipV;
            }
            if (!ScrabbleDictionary.Words.Contains(verticalWord.ToUpper().Trim(null))) {
                //Console.WriteLine($"DEBUG: In word {move.word}: Created word {verticalWord} is invalid.");
                Console.WriteLine($"DEBUG: {move.Word} -> {verticalWord} X");
                return false;
            }

            skipV:
            //Check some more words.
            List<string> hWords = ReadLine(move, Direction.Horizontal);
            foreach (var word in hWords) {
                if (word.Length < 2) continue;
                if (!ScrabbleDictionary.Words.Contains(word.ToUpper().Trim(null))) {
                    return false;
                }
            }

            List<string> vWords = ReadLine(move, Direction.Vertical);
            foreach (var word in vWords) {
                if (word.Length < 2) continue;
                if (!ScrabbleDictionary.Words.Contains(word.ToUpper().Trim(null))) {
                    return false;
                }
            }

            //Does it get the Letters it needs?
            List<char> needed = LettersRequired(move);
            List<char> fullAvailable = new List<char>();
            foreach (char letter in Game.Letters) {
                fullAvailable.Add(letter);
            }

            foreach (var sq in affected) {
                if (!Game.Board.GetSquareContents(sq).Equals(' ')) {
                    fullAvailable.Add(Game.Board.GetSquareContents(sq));
                }
            }

            var diff = needed.Except(fullAvailable);
            var enumerable = diff as char[] ?? diff.ToArray();
            bool gotAllNeeded = !enumerable.Any();
            if (!gotAllNeeded) {
                if (!(Game.BlankCount >= enumerable.ToList().Count)) return false;
            }

            //Have we used Letters multiple times where we shouldn't have?
            Dictionary<char, int> legalUses = new Dictionary<char, int>();
            foreach (char letter in fullAvailable) {
                if (!legalUses.Keys.FastContains(letter)) {
                    legalUses[letter] = ((from temp in fullAvailable where temp.Equals(letter) select temp).Count());
                }
            }

            foreach (char letter in move.Word) {
                int count = (from temp in move.Word where temp.Equals(letter) select temp).Count();
                if (!legalUses.Keys.FastContains(letter)) continue;
                if (legalUses[letter] < count) {
                    return false;
                }
            }

            //Does it wrap around the board?
            if (moveDir == Direction.Vertical) {
                //Check if there are enough rows for the word.
                for (int i = Game.Board.Rows.IndexOf(move.FirstLetterPos.row); i < move.Word.Length; i++) {
                    if (Game.Board.Rows.Count <= i) {
                        //No more rows.
                        Console.WriteLine("DEBUG: Wrapping");
                        return false;
                    }
                }
            } else {
                //Check if there are enough columns for the word.
                for (int i = Game.Board.Columns.IndexOf(move.FirstLetterPos.column); i < move.Word.Length; i++) {
                    if (Game.Board.Columns.Count <= i) {
                        //No more columns.
                        Console.WriteLine("DEBUG: Wrapping");
                        return false;
                    }
                }
            }

            //A word can be played twice but not a move. (A move holds positioning data, so an identical move would be on top of another.)
            if (Game.OwnMoves.Contains(move) || Game.OpponentMoves.Contains(move)) {
                return false;
            }

            /* TO DO: Add any other checks that must be completed. */

            if (affected.All(arg => Game.Board.GetSquareContents(arg).ToString() == " ")) return false;

            //All checks passed.
            return true;
        }

        //Shift moves.
        private Move TranslateMove(Move move, Direction dir, int squares) {
            try {
                switch (dir) {
                    case Direction.Horizontal: {
                        int newColumnStart = Game.Board.Columns.IndexOf(move.FirstLetterPos.column) + squares;
                        int newColumnEnd = Game.Board.Columns.IndexOf(move.LastLetterPos.column) + squares;
                        if (Game.Board.Columns.Count <= newColumnStart || Game.Board.Columns.Count <= newColumnEnd) {
                            return Move.Err;
                        }
                        (int column, char row) newMoveStart = (Game.Board.Columns[newColumnStart],
                            move.FirstLetterPos.row);
                        (int column, char row) newMoveEnd = (Game.Board.Columns[newColumnEnd],
                            move.LastLetterPos.row);

                        return new Move(move.Word, newMoveStart, newMoveEnd);
                    }
                    default: {
                        int newRowStart = Game.Board.Rows.IndexOf(move.FirstLetterPos.row) + squares;
                        int newRowEnd = Game.Board.Rows.IndexOf(move.LastLetterPos.row) + squares;
                        if (Game.Board.Rows.Count <= newRowStart || Game.Board.Rows.Count <= newRowEnd) {
                            return Move.Err;
                        }
                        (int column, char row) newMoveStart = (move.FirstLetterPos.column,
                            Game.Board.Rows[newRowStart]);
                        (int column, char row) newMoveEnd = (move.LastLetterPos.column,
                            Game.Board.Rows[newRowEnd]);
                        return new Move(move.Word, newMoveStart, newMoveEnd);
                    }
                }
            } catch {
                return Move.Err;
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
            var end = (1, Game.Board.Rows[word.Length - 1]);
            return new Move(word, start, end);
        }

        /*
         * Get the top 11 moves. Top 11 because I set the hash dictionary's
         * capacity to 5 and the fill to 0.1 and that limited it to 11.
         *
         * ¯\_(ツ)_/¯ I guess 11 is a good enough number.
         */

        private HashDictionary<Move, int> PossibleBest(out int considered) {
            //Refresh the zones so we make sure we don't discard any possible moves.
            RefreshZones();

            int movesConsidered = 0, bestScore = 0;
            var moves = new HashDictionary<Move, int>(5, 0.1, C5.EqualityComparer<Move>.Default);

            PerformColor(ConsoleColor.DarkRed, () => {
                Parallel.ForEach(ScrabbleDictionary.Words, (word, stet) => {
                    movesConsidered++;

                    bool flip = false;
                    flipTime:

                    //1. Create a base move from which all moves for this word are derived.
                    Move baseMove = flip ? CreateMove(word, Direction.Vertical) : CreateMove(word);
                    if (!QuickEvalBase(baseMove)) return;

                    List<char> required = LettersRequired(baseMove);
                    if (required.Count > 1) return;

                    //The inverted if statements are just to make code easier to read.
                    //The indentation gets a bit crazy.
                    void FullEval(Move m) {
                        if (!QuickEval(m)) return;
                        if (!MoveIsPossible(m)) return;

                        int score;
                        if ((score = Score(m)) <= bestScore) return;

                        bestScore = score;
                        moves.Add(m, score);
                    }

                    //2. Translate the move 1 square to the right until we hit the side.
                    Parallel.For(0L, 16, (shift, state) => {
                        Move shifted = TranslateMove(baseMove, Direction.Horizontal, (int)shift);
                        movesConsidered++;

                        if (shifted.Equals(Move.Err)) state.Break();

                        //3. Translate the move 1 square down until we hit the bottom.
                        Parallel.For(0L, 16, (downShift, nestedState) => {
                            Move downShifted = TranslateMove(shifted, Direction.Vertical, (int)downShift);
                            movesConsidered++;

                            if (downShifted.Equals(Move.Err)) nestedState.Break();

                            //Evaluate the move.
                            FullEval(downShifted);
                        });
                    });

                    //Start again but vertically.
                    if (!flip) {
                        flip = true;
                        goto flipTime;
                    }
                });
            });

            considered = movesConsidered;
            return moves;
        }

        Dictionary<char, int> points = new Dictionary<char, int>();

        public void LoadPoints() {
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
        }

        private int CountLettersUsed(Move move) {
            var affected = AffectedSquares(move);

            int used = 0;
            for (int i = 0; i < affected.Count; i++) {
                //Only +1 if the square is empty; if it is not empty, it contains the letter that the move would place.
                if (Game.Board.IsEmpty(affected[i])) used++;
            }

            return used;
        }

        private int Score(Move move) {

            /*
             * Premium Word Squares: 
             * The score for an entire word is doubled when one of its Letters is placed on a pink square: it is tripled when one of its Letters is placed on a red square. 
             * Include premiums for double or triple letter values, if any, before doubling or tripling the word score. 
             * If a word is formed that covers two premium word squares, the score is doubled and then re-doubled (4 times the letter count), or tripled and then re-tripled (9 times the letter count). 
             * NOTE: the center square is a pink square, which doubles the score for the first word.
            */

            var affected = AffectedSquares(move);

            int doubleWordHits = 0;
            int tripleWordHits = 0;

            int score = 0;
            for (int i = 0; i < affected.Count; i++) {
                var squareType = Game.Board.GetSquare(affected[i]);

                switch (squareType) {
                    case Board.Square.Middle:
                        score += points[move.Word[i]];
                        doubleWordHits++;
                        break;
                    case Board.Square.DoubleLetter:
                        score += points[move.Word[i]] * 2;
                        break;
                    case Board.Square.TripleLetter:
                        score += points[move.Word[i]] * 3;
                        break;
                    case Board.Square.DoubleWord:
                        score += points[move.Word[i]];
                        doubleWordHits++;
                        break;
                    case Board.Square.TripleWord:
                        score += points[move.Word[i]];
                        tripleWordHits++;
                        break;
                    default:
                        score += points[move.Word[i]];
                        break;
                }
            }

            if (doubleWordHits > 0) {
                //By multiplying doubleWordHits by 2 we get how much we need to multiply the word by: 1 would be 2, 2 would be 4 (see the rules above).
                score *= doubleWordHits * 2;
            } else if (tripleWordHits > 0) {
                score *= tripleWordHits * 3;
            }

            //BINGO! If you play seven tiles on a turn, it's a Bingo. You score a premium of 50 points after totaling your score for the turn.
            if (CountLettersUsed(move) > 6) {
                Console.WriteLine($"DEBUG: Found bonus position for word {move.Word}");
                score += 50;
            }

            return score;
        }

        public Move BestMove(out int considered) {
            HashDictionary<Move, int> moves = PossibleBest(out int cons);

            var pair = moves.OrderByDescending(key => key.Value).First();
            Move best = pair.Key;
            int bestScore = pair.Value;
            
            PerformColor(ConsoleColor.DarkCyan, () => {
                Console.WriteLine($"DEBUG: Estimated score for word '{best.Word}' where placed: {bestScore}.");
                Console.WriteLine($"DEBUG: Letters used for word '{best.Word}' where placed: {CountLettersUsed(best)}.");
            });

            Console.WriteLine($"DEBUG: Picked {moves.Keys.Count} top moves.");

            considered = cons;
            return best;
        }
    }
}