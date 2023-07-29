using ChessChallenge.API;
using System;
using System.Linq;

// TODO: MOST IMPORTANT: Rewrite table packing to use decimals instead of ulongs because of their size
// TODO: MOST IMPORTANT: Remove Quiescence unused depth parameter for maximum improvements and use ply to calculate faster mates
// TODO: Killer moves
// TODO: History heuristic
// TODO: Late move reductions
// TODO: Passed pawn evaluation
// TODO: Null move pruning
// TODO: Check and promotion extensions

public class MyBot : IChessBot
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

        // 1/20th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 30;
        searchTimer = timer;

        // Progressively increase search depth, starting from 2
        for (int depth = 2; ; depth++)
        {
            Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms");

            PVS(depth, -9999999, 9999999);

            if (OutOfTime)
            {
                Console.WriteLine("Hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms with an eval of " +
                    TTRetrieve().Score + " centipawns.");
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
            // SwiftCheckmateBonus = 5000
            return -(99999 + (depth * 5000));

        // Terminal node, calculate score
        if (depth <= 0)
            // Do a Quiescence Search
            return QuiescenceSearch(2, alpha, beta);

        // Transposition table lookup
        TTEntry entry = TTRetrieve();

        // Found a valid entry for this position
        if (entry.Hash == board.ZobristKey && entry.Depth >= depth)
        {
            switch (entry.Flag)
            {
                // Exact
                case 1:
                    return entry.Score;
                // Lowerbound
                case -1:
                    alpha = Math.Max(alpha, entry.Score);
                    break;
                // Default case for upperbound (2) to save a token
                default:
                    beta = Math.Min(beta, entry.Score);
                    break;
            }

            if (alpha >= beta)
                return entry.Score;
        }

        // Search at a deeper depth
        Move[] moves = GetOrdererdMoves(entry.BestMove, false);

        int bestEval = -9999999;
        Move bestMove = moves[0];

        int i = 0;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval;

            // Always fully search the first child
            if (i == 0)
                eval = -PVS(depth - 1, -beta, -alpha);
            else 
            {
                // Search with a null window
                eval = -PVS(depth - 1, -alpha - 1, -alpha);

                // Research if failed high
                if (alpha < eval && eval < beta)
                    eval = -PVS(depth - 1, -beta, -eval);
            }

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
            i++;
        }

        // Transposition table insertion
        TTInsert(bestMove, bestEval, depth, bestEval <= alpha ? 2 : 1);

        return alpha;
    }

    // Quiescence search with help from
    // https://stackoverflow.com/questions/48846642/is-there-something-wrong-with-my-quiescence-search
    private int QuiescenceSearch(int depth, int alpha, int beta)
    {
        if (OutOfTime)
            return 0;

        // Evaluate the gamestate
        if (board.IsDraw())
            // Discourage draws slightly
            return -15;
        if (board.IsInCheckmate())
            // Checkmate = 99999
            // SwiftCheckmateBonus = 5000
            return -(99999 + (depth * 5000));

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
            int eval = -QuiescenceSearch(depth - 1, -beta, -alpha);
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
    private int GetMVV_LVA(PieceType victim, PieceType attacker)
    {
        /*
        // Access with MVV_LVA [victim - 1, attacker - 1]
        private readonly int[,] MVV_LVA =
        {
            // Exclude None and Kings from being the victim because they cannot be captured
            { 15, 14, 13, 12, 11, 10 }, // victim P, attacker P, N, B, R, Q, K
            { 25, 24, 23, 22, 21, 20 }, // victim N, attacker P, N, B, R, Q, K
            { 35, 34, 33, 32, 31, 30 }, // victim B, attacker P, N, B, R, Q, K
            { 45, 44, 43, 42, 41, 40 }, // victim R, attacker P, N, B, R, Q, K
            { 55, 54, 53, 52, 51, 50 }, // victim Q, attacker P, N, B, R, Q, K
        };
        */

        switch (victim)
        {
            case PieceType.None:
            case PieceType.King:
                return 0;
            default:
                return 10 * (int)victim - (int)attacker;
        }
    }

    // Scoring algorithm using MVVLVA
    // Taking into account the best move found from the previous search
    private Move[] GetOrdererdMoves(Move hashMove, bool onlyCaptures)
        => board.GetLegalMoves(onlyCaptures).OrderByDescending(move =>
        GetMVV_LVA(move.CapturePieceType, move.MovePieceType) +
        (move == hashMove ? 100 : 0)).ToArray();

    //
    // Evaluation
    //

    #region Evaluation

    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly int[] PieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                           94, 281, 297, 512, 936, 0 };// Endgame

    private readonly int[] GamePhaseIncrement = { 0, 1, 1, 2, 4, 0 };

    // Big table packed with data from premade piece square tables
    // Unpack using PackedEvaluationTables[set, rank] = file
    private readonly ulong[] PackedEvaluationTables = {
        0, 17876852006827220035, 17442764802556560892, 17297209133870877174, 17223739749638733806, 17876759457677835758, 17373217165325565928, 0,
        13255991644549399438, 17583506568768513230, 2175898572549597664, 1084293395314969850, 18090411128601117687, 17658908863988562672, 17579252489121225964, 17362482624594506424,
        18088114097928799212, 16144322839035775982, 18381760841018841589, 18376121450291332093, 218152002130610684, 507800692313426432, 78546933140621827, 17502669270662184681,
        2095587983952846102, 2166845185183979026, 804489620259737085, 17508614433633859824, 17295224476492426983, 16860632592644698081, 14986863555502077410, 17214733645651245043,
        2241981346783428845, 2671522937214723568, 2819295234159408375, 143848006581874414, 18303471111439576826, 218989722313687542, 143563254730914792, 16063196335921886463,
        649056947958124756, 17070610696300068628, 17370107729330376954, 16714810863637820148, 15990561411808821214, 17219209584983537398, 362247178929505537, 725340149412010486,
        0, 9255278100611888762, 4123085205260616768, 868073221978132502, 18375526489308136969, 18158510399056250115, 18086737617269097737, 0,
        13607044546246993624, 15920488544069483503, 16497805833213047536, 17583469180908143348, 17582910611854720244, 17434276413707386608, 16352837428273869539, 15338966700937764332,
        17362778423591236342, 17797653976892964347, 216178279655209729, 72628283623606014, 18085900871841415932, 17796820590280441592, 17219225120384218358, 17653536572713270000,
        217588987618658057, 145525853039167752, 18374121343630509317, 143834816923107843, 17941211704168088322, 17725034519661969661, 18372710631523548412, 17439054852385800698,
        1010791012631515130, 5929838478495476, 436031265213646066, 1812447229878734594, 1160546708477514740, 218156326927920885, 16926762663678832881, 16497506761183456745,
        17582909434562406605, 580992990974708984, 656996740801498119, 149207104036540411, 17871989841031265780, 18015818047948390131, 17653269455998023918, 16424899342964550108,
    };

    private int GetSquareBonus(int type, bool isWhite, int file, int rank)
    {
        // Mirror vertically for white pieces, since piece arrays are flipped vertically
        if (isWhite)
            rank = 7 - rank;

        // Grab the correct byte representing the value
        // And multiply it by the reduction factor to get our original value again
        return (int)Math.Round(unchecked((sbyte)((PackedEvaluationTables[(type * 8) + rank] >> file * 8) & 0xFF)) * 1.461);
    }

    private int Evaluate()
    {
        var middlegame = new int[2];
        var endgame = new int[2];

        int gamephase = 0;

        foreach (PieceList list in board.GetAllPieceLists())
        {
            int pieceType = (int)list.TypeOfPieceInList - 1;
            int colour = list.IsWhitePieceList ? 1 : 0;

            // Material evaluation
            middlegame[colour] += PieceValues[pieceType] * list.Count;
            endgame[colour] += PieceValues[pieceType + 6] * list.Count;

            // Square evaluation
            foreach (Piece piece in list)
            {
                middlegame[colour] += GetSquareBonus(pieceType, piece.IsWhite, piece.Square.File, piece.Square.Rank);
                endgame[colour] += GetSquareBonus(pieceType + 6, piece.IsWhite, piece.Square.File, piece.Square.Rank);
            }
            gamephase += GamePhaseIncrement[pieceType];
        }

        // Tapered evaluation
        int middlegamePhase = Math.Min(gamephase, 24);
        int finalScore = ((middlegame[1] - middlegame[0]) * middlegamePhase + (endgame[1] - endgame[0]) * (24 - middlegamePhase)) / 24;
        return board.IsWhiteToMove ? finalScore : -finalScore;
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
