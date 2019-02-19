using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace scrbl {
    public class DecisionMaker {
        //For speeding up evaluation by reducing needless checks.

        public class Zone {
            public List<(int column, char row)> Squares = new List<(int column, char row)>();

            public static List<Zone> FindEmptyZones() {
                List<(int column, char row)> upper = new List<(int column, char row)>();

                foreach (var square in Game.Board.Squares.Keys) {
                    if (Game.Board.GetSquareContents(square) != ' ') goto doublebreak1;
                    foreach (var surrounding in Game.Board.GetSurrounding(square)) {
                        //Break if there is something in the square.
                        if (Game.Board.GetSquareContents(surrounding) == ' ') goto doublebreak1;
                    }
                    upper.Add(square);
                }

                doublebreak1:
                var lower = new List<(int column, char row)>();

                foreach ((int column, char row) square in Game.Board.Squares.Keys.Reverse()) {
                    if (Game.Board.GetSquareContents(square) != ' ') goto doublebreak2;
                    foreach ((int column, char row) surrounding in Game.Board.GetSurrounding(square)) {
                        //Break if there is something in the square.
                        if (Game.Board.GetSquareContents(surrounding) == ' ') goto doublebreak2;
                    }
                    upper.Add(square);
                }

                doublebreak2:

                return new List<Zone>(new[] { new Zone { Squares = upper }, new Zone { Squares = lower } });
            }

            public bool Contains((int column, char row) pos) {
                return Squares.Contains(pos);
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
            //See if we have the move's position's squares in the cache.
            //if (cache.Contains(move)) {
            //    Console.WriteLine("Cached");
            //    return cache.Get(move);
            //}

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
        private static List<char> LettersRequired(Move move) {
            var needed = new List<char>();
            foreach (char letter in move.Word) {
                if (!Game.Letters.Contains(letter)) {
                    needed.Add(letter);
                }
            }

            return needed;
        }

        //Get all the words in one line (the direction of which is determined by the readDirection param).
        List<string> ReadLine(Move move, Direction readDirection) {
            List<string> found = new List<string>();
            var affected = AffectedSquares(move);

            StringBuilder bobTheBuilder = new StringBuilder();
            if (readDirection == Direction.Horizontal) {
                foreach (var pos in affected) {
                    var squaresOnRow = Game.Board.GetRow(pos.row);
                    foreach (var sq in squaresOnRow) {
                        char contents = Game.Board.GetSquareContents(sq);
                        if (affected.Contains(sq)) {
                            contents = move.Word[affected.IndexOf(sq)];
                        }
                        bobTheBuilder.Append(contents);
                    }
                }
            } else {
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
            }
            found = bobTheBuilder.ToString().Split(null).ToList();

            return found;
        }


        //Read the entire word created after a move has joined onto another word.
        private string ReadWord(Move move, Direction direction) {
            List<(int column, char row)> affected = AffectedSquares(move);

            if (direction == Direction.Horizontal) {
                var left = new StringBuilder();
                var right = new StringBuilder();

                (int column, char row) currentSquare = move.FirstLetterPos;
                while (Game.Board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Left)) {
                    char contents = Game.Board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.Word[affected.IndexOf(currentSquare)];
                    }

                    left.Append(contents);
                    currentSquare = Game.Board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Left];
                }

                string leftRead = left.ToString();
                left = new StringBuilder(new string(leftRead.Reverse().ToArray()));

                currentSquare = move.FirstLetterPos;
                while (Game.Board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Right)) {
                    char contents = Game.Board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.Word[affected.IndexOf(currentSquare)];
                    }

                    right.Append(contents);
                    currentSquare = Game.Board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Right];
                }

                string removed = right.Length > 0 ? right.Remove(0, 1).ToString() : right.ToString();
                left.Append(removed);
                return left.ToString();
            } else {
                var up = new StringBuilder();
                var down = new StringBuilder();

                (int column, char row) currentSquare = move.FirstLetterPos;
                while (Game.Board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Up)) {
                    char contents = Game.Board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.Word[affected.IndexOf(currentSquare)];
                    }

                    up.Append(contents);
                    currentSquare = Game.Board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Up];
                }

                string upRead = up.ToString();
                up = new StringBuilder(new string(upRead.Reverse().ToArray()));

                currentSquare = move.FirstLetterPos;
                while (Game.Board.GetSurroundingDict(currentSquare).ContainsKey(Board.RelativePosition.Down)) {
                    char contents = Game.Board.GetSquareContents(currentSquare);
                    if (contents == ' ' && !affected.Contains(currentSquare)) {
                        break;
                    }

                    if (affected.Contains(currentSquare)) {
                        contents = move.Word[affected.IndexOf(currentSquare)];
                    }

                    down.Append(contents);
                    currentSquare = Game.Board.GetSurroundingDict(currentSquare)[Board.RelativePosition.Down];
                }
                string removed = down.Length > 0 ? down.Remove(0, 1).ToString() : down.ToString();
                up.Append(removed);
                return up.ToString();
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
            //Check if the number of Letters we don't have is too many.
            if (LettersRequired(move).Count > 1) return false;



            //Check if the move has already been played.
            if (Game.OwnMoves.Contains(move) || Game.OpponentMoves.Contains(move)) return false;

            //Check if the move will be into an empty zone.
            if (_upperZone.Contains(move.FirstLetterPos) && _upperZone.Contains(move.LastLetterPos)) return false;
            if (_lowerZone.Contains(move.FirstLetterPos) && _lowerZone.Contains(move.LastLetterPos)) return false;

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

        /* OPTIMISE THIS */
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

            bool gotAllNeeded = !needed.Except(fullAvailable).Any();
            if (!gotAllNeeded) {
                //Console.WriteLine($"DEBUG: In word {move.word}: Needed {{{string.Join(", ", needed)}}}, only have {{{string.Join(", ", fullAvailable)}}}");
                return false;
            }

            //Have we used Letters multiple times where we shouldn't have?
            Dictionary<char, int> legalUses = new Dictionary<char, int>();
            foreach (char letter in fullAvailable) {
                if (!legalUses.ContainsKey(letter)) {
                    legalUses[letter] = ((from temp in fullAvailable where temp.Equals(letter) select temp).Count());
                }
            }

            foreach (char letter in move.Word) {
                int count = (from temp in move.Word where temp.Equals(letter) select temp).Count();
                if (legalUses[letter] < count) {
                    //Console.WriteLine($"DEBUG: In word {move.word}: Used letter {letter.ToString()} {count} times when the limit was {legalUses[letter]}.");
                    return false;
                }
            }

            //Does it wrap around the board?
            if (moveDir == Direction.Vertical) {
                //Check if there are enough rows for the word.
                for (int i = Game.Board.Rows.IndexOf(move.FirstLetterPos.row); i < move.Word.Length; i++) {
                    if (Game.Board.Rows.Count <= i) {
                        //No more rows.
                        return false;
                    }
                }
            } else {
                //Check if there are enough columns for the word.
                for (int i = Game.Board.Columns.IndexOf(move.FirstLetterPos.column); i < move.Word.Length; i++) {
                    if (Game.Board.Columns.Count <= i) {
                        //No more columns.
                        return false;
                    }
                }
            }

            //A word can be played twice but not a move. (A move holds positioning data, so an identical move would be on top of another.)
            if (Game.OwnMoves.Contains(move) || Game.OpponentMoves.Contains(move)) {
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
                } else {
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

        //Get all possible moves.
        private List<Move> PossibleMoves(out int considered) {
            //Refresh the zones so we make sure we don't discard any possible moves.
            RefreshZones();

            /*
             * Getting moves:
             *      1. Loop through words.
             *      2. Skip words where we need >1 more Letters than we have. (This can be changed in the config file.)
             *      3. Create a base move for the word. Translate that move to every possible position.
             *      4. Check if the translated move would work.
             *      5. If it does, add it to a List.
            */

            int movesConsidered = 0;

            var possible = new List<Move>();

            Console.ForegroundColor = ConsoleColor.DarkRed;

            Console.CursorVisible = false;

            int bestScore = 0;

            Parallel.ForEach(ScrabbleDictionary.Words, (word, stet) => {
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
                if (required.Count > 1) {
                    return;
                }

                /* TO DO: Clean up all of this. */

                //This will have to be calculated for every shifted move once the special squares are added.
                //int score = Score(baseMove);
                //if (score <= bestScore) return;

                Parallel.For(0, 15, (shift, state) => {
                    //Horizontal shift loop.
                    Move shifted = TranslateMove(baseMove, Direction.Horizontal, shift);
                    movesConsidered++;

                    //Break if the translation failed. (There was an error fitting the move onto the board.)
                    if (shifted.Equals(Move.Err)) state.Break();

                    //if (score <= bestScore) state.Break();

                    if (QuickEval(shifted)) {
                        if (MoveIsPossible(shifted)) {
                            int score = Score(shifted);
                            if (score > bestScore)
                                bestScore = score;
                            possible.Add(shifted);
                        }
                    }

                    //if (score <= bestScore) state.Break();

                    Parallel.For(0, 15, (downShift, nestedState) => {
                        //Vertical shift loop.
                        //if (score <= bestScore) state.Break();

                        Move downShifted = TranslateMove(shifted, Direction.Vertical, downShift);
                        movesConsidered++;

                        if (downShifted.Equals(Move.Err)) nestedState.Break();

                        if (!QuickEval(downShifted)) return;
                        //if (score <= bestScore) state.Break();
                        if (MoveIsPossible(downShifted)) {
                            int iters = 0;
                            int emptyIters = 0;
                            foreach (var thing in AffectedSquares(downShifted)) {
                                iters++;
                                if (Game.Board.GetSquareContents(thing) == ' ') emptyIters++;
                            }

                            //if (score <= bestScore) return;

                            int score = Score(downShifted);
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
                });
                //Start again but vertically.
                if (!flip) {
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

            return score;
        }

        public Move BestMove(out int considered) {

            List<Move> possible = PossibleMoves(out int cons);
            if (possible.Count < 1) {
                Console.WriteLine("No moves generated!");
                considered = cons;
                return Move.Err;
            }

            Move best = possible[0];
            int bestScore = Score(best);

            Parallel.ForEach(possible, move => {
                cons++;
                int moveScore = Score(move);
                if (moveScore > bestScore) {
                    best = move;
                    bestScore = moveScore;
                }
            });

            considered = cons;
            Utils.PerformColor(ConsoleColor.DarkCyan, () => {
                //lol
                Console.WriteLine($"DEBUG: By my calculations, the word {best.Word} should give " +
                                  $"a score of {bestScore} where I put it. But I'm not designed to calculate scores, so it's rough.");
            });

            //Why the fuck do I have to do this?
            var affected = AffectedSquares(best);
            while (affected.All(arg => { return Game.Board.GetSquareContents(arg).ToString() == " "; })) {
                possible.Remove(best);
                best = possible.Last();
                affected = AffectedSquares(best);
            }

            return best;

            //return SelectMove(out considered);
        }
    }
}