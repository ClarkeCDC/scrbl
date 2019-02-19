using System.Collections.Generic;

namespace scrbl {
    public static class Game {
        public static readonly List<char> Letters = new List<char>();

        public static readonly List<DecisionMaker.Move> OwnMoves = new List<DecisionMaker.Move>();
        public static readonly List<DecisionMaker.Move> OpponentMoves = new List<DecisionMaker.Move>();

        public static Board Board = new Board();

        public static readonly DecisionMaker Brain = new DecisionMaker();
    }
}