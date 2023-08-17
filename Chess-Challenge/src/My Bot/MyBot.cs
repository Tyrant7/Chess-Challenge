#define DEBUG

using ChessChallenge.API;
using System;
using System.Linq;

// TODO: More token saves
// TODO: Fully test promotion ordering

public class MyBot : IChessBot
{
    private int searchMaxTime;
    private Timer searchTimer;

    private int[,,] historyHeuristics;
    private Move[] killers = new Move[52];

    Board board;
    Move rootMove;

#if DEBUG
    long nodes;
#endif

    public Move Think(Board newBoard, Timer timer)
    {
#if DEBUG
        Console.WriteLine();
        nodes = 0;
#endif

        // Cache the board to save precious tokens
        board = newBoard;

        // Reset history heuristics and killer moves
        historyHeuristics = new int[2, 7, 64];

        // 1/30th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 30;
        searchTimer = timer;

        // Progressively increase search depth, starting from 2
        for (int depth = 2, alpha = -999999, beta = 999999, eval; ;)
        {
            eval = PVS(depth, alpha, beta, 0, true);

            // Out of time
            if (searchTimer.MillisecondsElapsedThisTurn > searchMaxTime)
                return rootMove;

            // Gradual widening
            // Fell outside window, retry with wider window search
            if (eval <= alpha)
                alpha -= 62;
            else if (eval >= beta)
                beta += 62;
            else
            {
#if DEBUG
                string evalWithMate = eval.ToString();
                if (Math.Abs(eval) > 50000)
                {
                    evalWithMate = eval < 0 ? "-" : "";
                    evalWithMate += $"M{Math.Ceiling((99999 - (double)eval) / 2)}";
                }

                Console.WriteLine("Info: depth: {0, 2} || eval: {1, 6} || nodes: {2, 9} || nps: {3, 8} || time: {4, 5}ms || best move: {5}{6}",
                    depth,
                    evalWithMate,
                    nodes,
                    1000 * nodes / (timer.MillisecondsElapsedThisTurn + 1),
                    timer.MillisecondsElapsedThisTurn,
                    rootMove.StartSquare.Name,
                    rootMove.TargetSquare.Name);
#endif

                // Set up window for next search
                alpha = eval - 17;
                beta = eval + 17;
                depth++;
            }
        }
    }

    #region Search

    // This method doubles as our PVS and QSearch in order to save tokens
    private int PVS(int depth, int alpha, int beta, int plyFromRoot, bool allowNull)
    {
#if DEBUG
        nodes++;
#endif

        // Declare some reused variables
        bool inCheck = board.IsInCheck(),
            isPV = beta - alpha > 1,
            canPrune = false,
            notRoot = plyFromRoot++ > 0;

        // Ply check is for long forced endgame draw sequences where search can get stuck forever
        if (notRoot && board.IsRepeatedPosition() || plyFromRoot > 50)
            return 0;

        ulong zobristKey = board.ZobristKey;
        ref TTEntry entry = ref transpositionTable[zobristKey & 0x3FFFFF];

        // Define best eval all the way up here to generate the standing pattern for QSearch
        int bestEval = -9999999,
            originalAlpha = alpha,
            movesTried = 0,
            entryScore = entry.Score,
            entryFlag = entry.Flag,
            movesScored = 0,
            eval;

        //
        // Evil local method to save tokens for similar calls to PVS (set eval inside search)
        int Search(int newAlpha, int R = 1, bool canNull = true) => eval = -PVS(depth - R, -newAlpha, -alpha, plyFromRoot, canNull);
        //
        //

        // Transposition table lookup -> Found a valid entry for this position
        if (entry.Hash == zobristKey && notRoot && entry.Depth >= depth && (
                // Exact
                entryFlag == 1 ||
                // Upperbound
                entryFlag == 2 && entryScore <= alpha ||
                // Lowerbound
                entryFlag == 3 && entryScore >= beta))
            return entryScore;

        // Check extensions
        if (inCheck)
            depth++;

        // Declare QSearch status here to prevent dropping into QSearch while in check
        bool inQSearch = depth <= 0;
        if (inQSearch)
        {
            // Determine if quiescence search should be continued
            bestEval = Evaluate();
            if (bestEval >= beta)
                return bestEval;
            alpha = Math.Max(alpha, bestEval);
        }
        // No pruning in QSearch
        // If this node is NOT part of the PV and we're not in check
        else if (!isPV && !inCheck)
        {
            // Reverse futility pruning
            int staticEval = Evaluate();

            // Give ourselves a margin of 96 centipawns times depth.
            // If we're up by more than that margin in material, there's no point in
            // searching any further since our position is so good
            if (depth <= 10 && staticEval - 96 * depth >= beta)
                return staticEval;

            // NULL move pruning
            if (allowNull && depth >= 2)
            {
                board.TrySkipTurn();
                Search(beta, 3 + depth / 5, false);
                board.UndoSkipTurn();

                // Failed high on the null move
                if (eval >= beta)
                    return eval;
            }

            // Extended futility pruning
            // Can only prune when at lower depth and behind in evaluation by a large margin
            canPrune = depth <= 8 && staticEval + depth * 141 <= alpha;

            // Razoring (reduce depth if up a significant margin at depth 3)
            /*
            if (depth == 3 && staticEval + 620 <= alpha)
                depth--;
            */
        }

        // Generate appropriate moves depending on whether we're in QSearch
        Span<Move> moveSpan = stackalloc Move[242];
        board.GetLegalMovesNonAlloc(ref moveSpan, inQSearch && !inCheck);

        // Order moves in reverse order -> negative values are ordered higher hence the strange equations
        Span<int> moveScores = stackalloc int[moveSpan.Length];
        foreach (Move move in moveSpan)
            moveScores[movesScored++] = -(
            // Hash move
            move == entry.BestMove ? 9_000_000 :
            // Promotions
            // move.IsPromotion ? 8_000_000 :
            // MVVLVA
            move.IsCapture ? 1_000_000 * (int)move.CapturePieceType - (int)move.MovePieceType :
            // Killers
            killers[plyFromRoot] == move ? 900_000 :
            // History
            historyHeuristics[plyFromRoot & 1, (int)move.MovePieceType, move.TargetSquare.Index]);

        moveScores.Sort(moveSpan);

        // Gamestate, checkmate and draws
        if (!inQSearch && moveSpan.IsEmpty)
            return inCheck ? plyFromRoot - 99999 : 0;

        Move bestMove = default;
        foreach (Move move in moveSpan)
        {
            bool tactical = movesTried == 0 || move.IsCapture || move.IsPromotion;
            if (canPrune && !tactical)
                continue;

            board.MakeMove(move);

            //////////////////////////////////////////////////////
            ////                                              ////
            ////                                              ////
            ////     [You're about to see some terrible]      ////
            //// [disgusting syntax that saves a few tokens]  ////
            ////                                              ////
            ////                                              ////
            ////                                              ////
            //////////////////////////////////////////////////////

            // LMR + PVS
            if (movesTried++ == 0 || inQSearch)
                // Always search first node with full depth
                Search(beta);

            // Set eval to appropriate alpha to be read from later
            // -> if reduction is applicable do a reduced search with a null window,
            // othewise automatically set alpha be above the threshold
            else if ((tactical || movesTried < 6 || depth < 2 || inCheck
                    ? eval = alpha + 1
                    : Search(alpha + 1, 1 + movesTried / 7 + depth / 5)) > alpha &&

                    // If alpha was above threshold, update eval with a search with a null window
                    alpha < Search(alpha + 1))
                // We raised alpha on the null window search, research with no null window
                Search(beta);

            //////////////////////////////////////////////
            ////                                      ////
            ////       [~ Exiting syntax hell ~]      ////
            ////           [Or so you think]          ////
            ////                                      ////
            ////                                      ////
            //////////////////////////////////////////////

            board.UndoMove(move);

            if (eval > bestEval)
            {
                bestMove = move;
                bestEval = eval;

                // Update the root move
                if (!notRoot)
                    rootMove = move;

                alpha = Math.Max(eval, alpha);

                // Cutoff
                if (alpha >= beta)
                {
                    // Update history tables
                    if (!move.IsCapture)
                    {
                        historyHeuristics[plyFromRoot & 1, (int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                        killers[plyFromRoot] = move;
                    }
                    break;
                }
            }

            // Out of time => return a large value guaranteed to be less than alpha when negated
            if (searchTimer.MillisecondsElapsedThisTurn > searchMaxTime)
                return 99999999;
        }

        // Transposition table insertion
        entry = new(
            zobristKey,
            bestMove,
            bestEval,
            depth,
            bestEval >= beta ? 3 : bestEval <= originalAlpha ? 2 : 1);

        return bestEval;
    }

    #endregion

    #region Evaluation

    private readonly int[] GamePhaseIncrement = { 0, 1, 1, 2, 4, 0 };

    // Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly short[] PieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                             94, 281, 297, 512, 936, 0 }; // Endgame

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
        UnpackedPestoTables = PackedPestoTables.Select(packedTable =>
        {
            int pieceType = 0;
            return new System.Numerics.BigInteger(packedTable).ToByteArray().Take(12)
                    .Select(square => (int)((sbyte)square * 1.461) + PieceValues[pieceType++])
                .ToArray();
        }).ToArray();
    }

    private int Evaluate()
    {
        int middlegame = 0, endgame = 0, gamephase = 0, sideToMove = 2, piece, square;
        for (; --sideToMove >= 0; middlegame = -middlegame, endgame = -endgame)
            for (piece = -1; ++piece < 6;)
                for (ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;)
                {
                    // Gamephase, middlegame -> endgame
                    gamephase += GamePhaseIncrement[piece];

                    // Material and square evaluation
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;
                    middlegame += UnpackedPestoTables[square][piece];
                    endgame += UnpackedPestoTables[square][piece + 6];
                }
                                                                                                        // Tempo bonus to help with aspiration windows
        return (middlegame * gamephase + endgame * (24 - gamephase)) / 24 * (board.IsWhiteToMove ? 1 : -1) + gamephase / 2;
    }

    #endregion

    #region Transposition Table

    // 0x400000 represents the rough number of entries it would take to fill 256mb
    // Very lowballed to make sure I don't go over
    private readonly TTEntry[] transpositionTable = new TTEntry[0x400000];

    // enum Flag
    // {
    //     0 = Invalid,
    //     1 = Exact,
    //     2 = Upperbound,
    //     3 = Lowerbound
    // }
    private record struct TTEntry(ulong Hash, Move BestMove, int Score, int Depth, int Flag);

    #endregion
}
