using ChessChallenge.API;
using System;
using System.Linq;

namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
    {
        private int searchMaxTime;
        private Timer searchTimer;

        // Return true if out of time AND a valid move has been found
        private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime &&
                                  TTRetrieve().Hash == board.ZobristKey &&
                                  TTRetrieve().BestMove != Move.NullMove;

        Board board;

        //
        // Search
        //

        public Move Think(Board newBoard, Timer timer)
        {
            // Cache the board to save precious tokens
            board = newBoard;

            // 1/30th of our remaining time, split among all of the moves
            searchMaxTime = timer.MillisecondsRemaining / 30;
            searchTimer = timer;

            // Progressively increase search depth, starting from 2
            for (int depth = 2; ; depth++)
            {
                Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms");

                PVS(depth, -9999999, 9999999);

                if (OutOfTime)
                {
                    // Console.WriteLine("Hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms with an eval of " +
                    //    TTRetrieve().Score + " centipawns.");
                    return TTRetrieve().BestMove;
                }
            }
        }

        private int PVS(int depth, int alpha, int beta)
        {
            // Evaluate the gamestate
            if (board.IsDraw())
                // Discourage draws slightly
                return -15;
            if (board.IsInCheckmate())
                // Checkmate = 99999
                return -(99999 - board.PlyCount);

            // Terminal node, start QSearch
            if (depth <= 0)
                return QuiescenceSearch(alpha, beta);

            // Transposition table lookup
            TTEntry entry = TTRetrieve();

            // Found a valid entry for this position
            if (entry.Hash == board.ZobristKey && entry.Depth >= depth)
            {
                // Exact
                if (entry.Flag == 1)
                    return entry.Score;
                // Lowerbound
                if (entry.Flag == -1)
                    alpha = Math.Max(alpha, entry.Score);
                // Upperbound
                else
                    beta = Math.Min(beta, entry.Score);

                if (alpha >= beta)
                    return entry.Score;
            }

            // Using var to save a single token
            var moves = GetOrdererdMoves(entry.BestMove, false);

            int bestEval = -9999999;
            Move bestMove = moves[0];

            bool firstMove = true;
            foreach (Move move in moves)
            {
                board.MakeMove(move);

                // Always fully search the first child, search the rest with a null window
                int eval = firstMove ? -PVS(depth - 1, -beta, -alpha) : -PVS(depth - 1, -alpha - 1, -alpha);

                // If failed high, do a research
                if (!firstMove && alpha < eval && eval < beta)
                    eval = -PVS(depth - 1, -beta, -eval);

                board.UndoMove(move);

                if (OutOfTime)
                    return 0;

                if (eval > bestEval)
                {
                    // Beta cutoff
                    if (eval >= beta)
                    {
                        TTInsert(move, eval, depth, -1);
                        return eval;
                    }

                    bestMove = move;
                    bestEval = eval;
                    alpha = Math.Max(alpha, bestEval);
                }
                firstMove = false;
            }

            // Transposition table insertion
            TTInsert(bestMove, bestEval, depth, bestEval <= alpha ? 2 : 1);

            return alpha;
        }

        // Quiescence search with help from
        // https://stackoverflow.com/questions/48846642/is-there-something-wrong-with-my-quiescence-search
        private int QuiescenceSearch(int alpha, int beta)
        {
            if (OutOfTime)
                return 0;

            // Evaluate the gamestate
            if (board.IsDraw())
                // Discourage draws slightly
                return -15;
            if (board.IsInCheckmate())
                // Checkmate = 99999
                return -(99999 - board.PlyCount);

            // Determine if quiescence search should be continued
            int bestValue = Evaluate();

            alpha = Math.Max(alpha, bestValue);
            if (alpha >= beta)
                return bestValue;

            // If in check, look into all moves, otherwise just captures
            // Also no hash move for Quiescence search
            foreach (Move move in GetOrdererdMoves(Move.NullMove, !board.IsInCheck()))
            {
                board.MakeMove(move);
                int eval = -QuiescenceSearch(-beta, -alpha);
                board.UndoMove(move);

                bestValue = Math.Max(bestValue, eval);
                alpha = Math.Max(alpha, bestValue);
                if (alpha >= beta)
                    break;
            }
            return bestValue;
        }

        //
        // Move Ordering
        //

        // Generates the comment inside, but with 25 fewer tokens
        private int GetMVV_LVA(int victim, int attacker)
             // 0 = None, 6 = King, no bonuses for these two
             => victim == 0 || victim == 6 ? 0 : 10 * victim - attacker;

        // Scoring algorithm using MVVLVA
        // Taking into account the best move found from the previous search
        private Move[] GetOrdererdMoves(Move hashMove, bool onlyCaptures)
            => board.GetLegalMoves(onlyCaptures).OrderByDescending(move =>
            GetMVV_LVA((int)move.CapturePieceType, (int)move.MovePieceType) +
            (move == hashMove ? 100 : 0)).ToArray();

        //
        // Evaluation
        //

        #region Evaluation

        private readonly int[] GamePhaseIncrement = { 0, 1, 1, 2, 4, 0 };

        // None, Pawn, Knight, Bishop, Rook, Queen, King 
        private readonly short[] PieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                             94, 281, 297, 512, 936, 0}; // Endgame

        // Big table packed with data from premade piece square tables
        // Unpack using PackedEvaluationTables[set, rank] = file
        private readonly decimal[] PackedPestoTables = {
        63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m, 75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m, 936945638387574698250991104m, 75531285965747665584902616832m,
        77047302762000299964198997571m, 3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m, 3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m, 2475894077091727551177487608m,
        2458978764687427073924784380m, 3718684080556872886692423941m, 4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m, 9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
        77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m, 5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m, 5619082524459738931006868492m, 649197923531967450704711664m,
        75809334407291469990832437230m, 78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m, 5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m, 76772801025035254361275759599m,
        75502243563200070682362835182m, 78896921543467230670583692029m, 2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m, 3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
        73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m, 3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m, 78580145051212187267589731866m, 75798434925965430405537592305m,
        68369566912511282590874449920m, 72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m, 73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m, 70529879645288096380279255040m,
    };

        private readonly int[][] UnpackedPestoTables;

        public EvilBot()
        {
            UnpackedPestoTables = new int[64][];
            for (int i = 0; i < 64; i++)
            {
                int pieceType = 0;
                UnpackedPestoTables[i] = decimal.GetBits(PackedPestoTables[i]).Take(3)
                    .SelectMany(c => BitConverter.GetBytes(c)
                        .Select((byte square) => (int)((sbyte)square * 1.461) + PieceValues[pieceType++]))
                    .ToArray();
            }
        }

        private int Evaluate()
        {
            var middlegame = new int[2];
            var endgame = new int[2];

            int gamephase = 0;

            foreach (PieceList list in board.GetAllPieceLists())
                foreach (Piece piece in list)
                {
                    int pieceType = (int)list.TypeOfPieceInList - 1;
                    int colour = list.IsWhitePieceList ? 1 : 0;
                    int index = piece.Square.Index ^ (piece.IsWhite ? 56 : 0);

                    middlegame[colour] += UnpackedPestoTables[index][pieceType];
                    endgame[colour] += UnpackedPestoTables[index][pieceType + 6];
                    gamephase += GamePhaseIncrement[pieceType];
                }

            // Tapered evaluation
            int middlegamePhase = Math.Min(gamephase, 24);
            return ((middlegame[1] - middlegame[0]) * middlegamePhase + (endgame[1] - endgame[0]) * (24 - middlegamePhase)) / 24
                  * (board.IsWhiteToMove ? 1 : -1);
        }

        #endregion

        #region Transposition Table

        // 0x400000 represents the rough number of entries it would take to fill 256mb
        // Very lowballed to make sure I don't go over
        private readonly TTEntry[] transpositionTable = new TTEntry[0x400000];

        private TTEntry TTRetrieve()
            => transpositionTable[board.ZobristKey & 0x3FFFFF];

        private void TTInsert(Move bestMove, int score, int depth, int flag)
        {
            if (depth > 1 && depth > TTRetrieve().Depth)
                transpositionTable[board.ZobristKey & 0x3FFFFF] = new TTEntry(
                    board.ZobristKey,
                    bestMove,
                    score,
                    depth,
                    flag);
        }

        // public enum Flag
        // {
        //     0 = Invalid,
        //     1 = Exact
        //    -1 = Lowerbound,
        //     2 = Upperbound
        // }
        private record struct TTEntry(ulong Hash, Move BestMove, int Score, int Depth, int Flag);

        #endregion
    }
}