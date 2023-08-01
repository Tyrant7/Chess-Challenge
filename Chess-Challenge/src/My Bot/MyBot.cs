using ChessChallenge.API;
using System;
using System.Linq;

<<<<<<< HEAD
// TODO: MOST IMPORTANT: Null move pruning
// TODO: MOST IMPORTANT: Quiescence search
// TODO: King safety
// TODO: Check extensions
// TODO: Can probably optimize MVV_LVA with a simple mathematical function
// TODO: Reimplement killer moves with more efficiency

public class MyBot : IChessBot
{
    TranspositionTable transpositionTable = new();

    // 2 is the number of killer moves
    // 12 as a placeholder max ply value
    Move[,] killerMoves = new Move[2, 12];

    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly int[] PieceValues = { 0, 100, 320, 320, 500, 900, 0 };

    // MVV_LVA [victim - 1, attacker - 1]
    private readonly int[,] MVV_LVA =
    {
        // When accessing, since none is not an opiton, use victim - 1 and attacker - 1 since pawns are indexed at 0
        // Also exclude kings from being the victim because they cannot be captured
        { 15, 14, 13, 12, 11, 10 }, // victim P, attacker P, N, B, R, Q, K
        { 25, 24, 23, 22, 21, 20 }, // victim N, attacker P, N, B, R, Q, K
        { 35, 34, 33, 32, 31, 30 }, // victim B, attacker P, N, B, R, Q, K
        { 45, 44, 43, 42, 41, 40 }, // victim R, attacker P, N, B, R, Q, K
        { 55, 54, 53, 52, 51, 50 }, // victim Q, attacker P, N, B, R, Q, K
    };

    private int initialSearchPly;

=======
// TODO: Most Important: Combine PVS and QSearch into 1 function
// TODO: Most Important: Experiment with a larger TT size to improve bot and hopefully fix PVS as well
// TODO: Most Important: Setup that faster testing environment that everybody seems to have
// TODO: Most Important: Implement history heuristic with piece-to

// Heuristics
// TODO: Killer moves
// TODO: Late move reductions
// TODO: Passed pawn evaluation
// TODO: Null move pruning
// TODO: Check and promotion extensions
// TODO: Experiment with new sorting techniques for moves

public class MyBot : IChessBot
{
>>>>>>> 04ec998
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
<<<<<<< HEAD
        initialSearchPly = board.PlyCount;
        Move[] moves = OrderMoves(board, board.GetLegalMoves());
=======
        // Cache the board to save precious tokens
        board = newBoard;
>>>>>>> 04ec998

        // Reset history heuristics
        historyHeuristics = new int[2, 64, 64];

        // 1/30th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 30;
        // searchMaxTime = 60000;
        searchTimer = timer;

<<<<<<< HEAD
        // No max depth, keep going until time limit is reached
        for (int depth = 1; ; depth++)
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int moveScore = -Negamax(board, depth, -9999999, 9999999, board.IsWhiteToMove ? 1 : -1);
                board.UndoMove(move);

                // Place this after the negamax in case we ran out of time during the negamax search
                if (OutOfTime)
                    return bestMove;

                if (moveScore > bestScore)
                {
                    bestScore = moveScore;
                    bestMove = move;
                }
            }
    }

    private Move[] OrderMoves(Board board, Move[] moves)
        => moves.OrderByDescending(move => ScoreMove(board, move)).ToArray();

    private int ScoreMove(Board board, Move move)
    {
        if (move.CapturePieceType != PieceType.None)
            // MVV_LVA_Offset = int.MaxValue - 256
            // Or 2147483391
            return 2147483391 + MVV_LVA[(int)move.CapturePieceType - 1, (int)move.MovePieceType - 1];
        else
            for (int n = 0; n < 2; n++)
                if (move == killerMoves[n, board.PlyCount - initialSearchPly])
                    // If killer move matches at spot 0, we'll end up with
                    // MVV_LVA_Offset - 10, the second would be MVV_LVA_Offset - 20
                    // This will always place killer moves just below captures
                    return 2147483391 - ((n + 1) * 10);
        return 0;
    }

    private int Negamax(Board board, int depth, int alpha, int beta, int colour)
    {
        if (OutOfTime)
            return 0;

        int originalAlpha = alpha;

        // Transposition table lookup
        PositionInfo position = transpositionTable.Lookup(board.ZobristKey);
        if (position.IsValid && position.depthChecked >= depth)
=======
        // Progressively increase search depth, starting from 2
        for (int depth = 2; ;)
>>>>>>> 04ec998
        {
            // Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms");

            PVS(depth++, -9999999, 9999999, 0);

            /*
            if (OutOfTime)
            {
                Console.WriteLine("Hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms with an eval of " +
                    TTRetrieve().Score + " centipawns.");
                return TTRetrieve().BestMove;
            }
            */

            if (OutOfTime)
                return TTRetrieve().BestMove;
        }

        // DEBUG
        /*
        board = newBoard;

        int startMS = timer.MillisecondsElapsedThisTurn;
        for (int i = 0; i < 500000; i++)
        {
            MyEvaluate();
        }

        Console.WriteLine("My evaluation: " + MyEvaluate());
        Console.WriteLine(timer.MillisecondsElapsedThisTurn - startMS + "ms to evaluate using my method");

        startMS = timer.MillisecondsElapsedThisTurn;
        for (int i = 0; i < 500000; i++)
        {
            Evaluate();
        }

        Console.WriteLine("JW's evaluation: " + Evaluate());
        Console.WriteLine(timer.MillisecondsElapsedThisTurn - startMS + "ms to evaluate using JW's method");

        return newBoard.GetLegalMoves()[0];
        */
    }

    private int PVS(int depth, int alpha, int beta, int searchPly, bool allowNull = true)
    {
        // Evaluate the gamestate
        if (board.IsDraw())
            // Discourage draws slightly
            return 0;
        if (board.IsInCheckmate())
            // Checkmate = 99999
            return -(99999 - searchPly);

        // Terminal node, start QSearch
        if (depth <= 0)
            return QuiescenceSearch(alpha, beta, searchPly + 1);

        // Transposition table lookup
        TTEntry entry = TTRetrieve();

        // Found a valid entry for this position
        if (entry.Hash == board.ZobristKey && searchPly > 0 &&
            entry.Depth >= depth)
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

        // TODO: Test this with the fixed PVS against fixed PVS without NMP
        // NULL move pruning
        // If this node is NOT part of the PV
        /*
        if (beta - alpha <= 1 && depth > 3 && allowNull && board.TrySkipTurn())
        {
            int eval = -PVS(depth - 2, -beta, 1 - beta, false);
            board.UndoSkipTurn();

            // Failed high on the null move
            if (eval >= beta)
                return eval;
        }
        */

        // Using var to save a single token
        var moves = GetOrderedMoves(entry.BestMove, false);

        int bestEval = -9999999;
        Move bestMove = moves[0];

        bool searchForPV = true;
        foreach (Move move in moves)
        {
            board.MakeMove(move);

            // Always fully search the first child, search the rest with a null window
            int eval = -PVS(depth - 1, searchForPV ? -beta : -alpha - 1, -alpha, searchPly + 1);

            // Found a move that can raise alpha, do a research
            if (!searchForPV && alpha < eval && eval < beta)
                eval = -PVS(depth - 1, -beta, -alpha, searchPly + 1);

            board.UndoMove(move);

            if (OutOfTime)
                return 0;

            if (eval > bestEval)
            {
                bestMove = move;
                bestEval = eval;
                alpha = Math.Max(eval, alpha);

                if (alpha >= beta)
                {
                    if (!move.IsCapture)
                        historyHeuristics[board.IsWhiteToMove ? 1 : 0, move.StartSquare.Index, move.TargetSquare.Index] += depth * depth;

                    TTInsert(move, eval, depth, -1);
                    return eval;
                }
            }
            searchForPV = false;
        }

        // Transposition table insertion
        TTInsert(bestMove, bestEval, depth, bestEval <= alpha ? 2 : 1);

        return alpha;
    }

    // Quiescence search with help from
    // https://stackoverflow.com/questions/48846642/is-there-something-wrong-with-my-quiescence-search
    private int QuiescenceSearch(int alpha, int beta, int searchPly)
    {
        if (OutOfTime)
            return 0;

        // Evaluate the gamestate
        if (board.IsDraw())
            // Discourage draws slightly
            return 0;
        if (board.IsInCheckmate())
            // Checkmate = 99999
            return -(99999 - searchPly);

        // Determine if quiescence search should be continued
        int bestValue = Evaluate();

        alpha = Math.Max(alpha, bestValue);
        if (alpha >= beta)
            return bestValue;

        // If in check, look into all moves, otherwise just captures
        // Also no hash move for Quiescence search
        foreach (Move move in GetOrderedMoves(Move.NullMove, !board.IsInCheck()))
        {
            board.MakeMove(move);
            int eval = -QuiescenceSearch(-beta, -alpha, searchPly + 1);
            board.UndoMove(move);

            bestValue = Math.Max(bestValue, eval);
            alpha = Math.Max(alpha, bestValue);
            if (alpha >= beta)
            {
                if (move.CapturePieceType == PieceType.None)
                    StoreKillerMove(move, board.PlyCount - initialSearchPly);
                break;
            }
        }
        return bestValue;
    }

<<<<<<< HEAD
    private void StoreKillerMove(Move move, int searchPly)
    {
        Move firstKiller = killerMoves[0, searchPly];

        // Don't store the same killer moves
        if (firstKiller != move)
        {
            // 2: Hardcoded max number of killer moves
            // Shift all moves one index upwards
            for (int i = 1; i < 2; i++)
            {
                Move previous = killerMoves[i - 1, searchPly];
                killerMoves[i, searchPly] = previous;
            }

            // Add the new killer move in the first spot
            killerMoves[0, searchPly] = move;
        }
    }

    // => instead of return { }
    // because it saves one token
    private int EvaluateMaterial(Board board)
        => board.GetAllPieceLists()
            .Sum(list => PieceValues[(int)list.TypeOfPieceInList] * list.Count * (list.IsWhitePieceList ? 1 : -1));
=======
    //
    // Move Ordering
    //
>>>>>>> 04ec998

    int[,,] historyHeuristics;

    // Scoring algorithm using MVVLVA
    // Taking into account the best move found from the previous search
    private Move[] GetOrderedMoves(Move hashMove, bool onlyCaputures)
        => board.GetLegalMoves(onlyCaputures).OrderByDescending(move =>
        {
            // Cache this here to save tokens
            int victim = (int)move.CapturePieceType;

            // MVVLVA: 0 = None, 6 = King, no bonuses for these two
            return (victim == 0 || victim == 6 ? 0 : 1000 * victim - (int)move.MovePieceType) +

            // Always check the hash move first
            (move == hashMove ? 9000 : 0) +

            // History heuristic
            historyHeuristics[board.IsWhiteToMove ? 1 : 0, move.StartSquare.Index, move.TargetSquare.Index];
        }).ToArray();

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

    public MyBot()
    {
        UnpackedPestoTables = new int[64][];
        UnpackedPestoTables = PackedPestoTables.Select(packedTable =>
        {
            int pieceType = 0;
            return decimal.GetBits(packedTable).Take(3)
                .SelectMany(c => BitConverter.GetBytes(c)
                    .Select((byte square) => (int)((sbyte)square * 1.461) + PieceValues[pieceType++]))
                .ToArray();
        }).ToArray();
    }

    private int Evaluate()
    {
        int middlegame = 0, endgame = 0, gamephase = 0;
        foreach (bool sideToMove in new[] { true, false })
        {
            // Initialize to the pawn bitboard
            ulong mask = board.GetPieceBitboard(PieceType.Pawn, sideToMove);

            // Start from the second bitboard and up since pawns have already been handled
            for (int piece = 0, square; piece < 5; mask = board.GetPieceBitboard((PieceType)(++piece + 1), sideToMove))
                while (mask != 0)
                {
                    gamephase += GamePhaseIncrement[piece];
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (sideToMove ? 56 : 0);
                    middlegame += UnpackedPestoTables[square][piece];
                    endgame += UnpackedPestoTables[square][piece + 6];
                }

            middlegame = -middlegame;
            endgame = -endgame;
        }
        return (middlegame * gamephase + endgame * (24 - gamephase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
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
