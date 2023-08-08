using ChessChallenge.API;
using System;
using System.Linq;

// TODO: Tons of token reduction. I can see lots of spots where a few tokens can be saved
// TODO: Test if old IsDraw and IsCheckmate work better than new detection

// Heuristics
// TODO: Aspiration Windows
// TODO: Try to get killers working again
// TODO: Passed pawn evaluation

public class MyBot : IChessBot
{
    private int searchMaxTime;
    private Timer searchTimer;

    // Only returns true when out of time AND a move has been found
    private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime;

    private int[,,] historyHeuristics;

    Board board;
    Move rootMove;

    public Move Think(Board newBoard, Timer timer)
    {
        // Cache the board to save precious tokens
        board = newBoard;
        rootMove = board.GetLegalMoves()[0];

        // Reset history heuristics and killer moves
        historyHeuristics = new int[2, 7, 64];

        // 1/30th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 30;
        searchTimer = timer;

        // Progressively increase search depth, starting from 2
        for (int depth = 1; ;)
        {
            PVS(++depth, -9999999, 9999999, 0);

            Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms with an eval of " + // #DEBUG
                transpositionTable[board.ZobristKey & 0x3FFFFF].Score + " centipawns"); // #DEBUG

            if (OutOfTime)
                return rootMove;
        }
    }

    #region Search

    // This method doubles as our PVS and QSearch in order to save tokens
    private int PVS(int depth, int alpha, int beta, int searchPly, bool allowNull = true)
    {
        // Declare some reused variables
        bool inQSearch = depth <= 0,
            inCheck = board.IsInCheck(),
            isPV = beta - alpha > 1,
            canPrune = false,
            notRoot = searchPly++ > 0,
            searchForPV = true;

        if (notRoot && board.IsRepeatedPosition())
            return 0;

        // Define best eval all the way up here to generate the standing pattern for QSearch
        int bestEval = -9999999, 
            originalAlpha = alpha,
            movesTried = 0,
            nextDepth = depth - 1,
            eval;

        // Transposition table lookup -> Found a valid entry for this position
        TTEntry entry = transpositionTable[board.ZobristKey & 0x3FFFFF];
        if (entry.Hash == board.ZobristKey && notRoot &&
            entry.Depth >= depth)
        {
            // Cache this value to save tokens by not referencing using the . operator
            int score = entry.Score;

            // Exact
            if (entry.Flag == 1)
                return score;

            // Lowerbound
            if (entry.Flag == 3)
                alpha = Math.Max(alpha, score);
            // Upperbound
            else
                beta = Math.Min(beta, score);

            if (alpha >= beta)
                return score;
        }

        if (inQSearch)
        {
            // Determine if quiescence search should be continued
            bestEval = Evaluate();

            alpha = Math.Max(alpha, bestEval);
            if (alpha >= beta)
                return bestEval;
        }
        // No extensions, NMP, or TT in QSearch
        else
        {
            // Check extensions
            if (inCheck)
                depth++;

            // If this node is NOT part of the PV and we're not in check
            if (!isPV && !inCheck)
            {
                // Static move pruning
                int staticEval = Evaluate();

                // Give ourselves a margin of 120 centipawns times depth.
                // If we're up by more than that margin, there's no point in
                // searching any further since our position is so good
                if (depth < 3 && staticEval - 120 * depth >= beta)
                    return staticEval - 120 * depth;

                // NULL move pruning
                if (depth > 2 && allowNull)
                {
                    board.TrySkipTurn();
                    eval = -PVS(depth - 3, -beta, 1 - beta, searchPly, false);
                    board.UndoSkipTurn();

                    // Failed high on the null move
                    if (eval >= beta)
                        return eval;
                }

                // Extended futility pruning
                // Can only prune when at lower depth and behind in evaluation by a large margin
                canPrune = depth <= 8 && staticEval + 40 + depth * 120 <= alpha;

                // TODO: Razoring
            }
        }

        // Generate appropriate moves depending on whether we're in QSearch
        // Using var to save a single token
        var moves = board.GetLegalMoves(inQSearch && !inCheck).OrderByDescending(move =>
        {
            return move == entry.BestMove ? 100000 :
            move.IsCapture ? 1000 * (int)move.CapturePieceType - (int)move.MovePieceType :
            historyHeuristics[board.IsWhiteToMove ? 1 : 0, (int)move.MovePieceType, move.TargetSquare.Index];
        }).ToArray();

        // Gamestate, checkmate and draws
        if (!inQSearch && moves.Length == 0)
            return inCheck ? searchPly - 99999 : 0;

        Move bestMove = default;
        foreach (Move move in moves)
        {
            // Return a large value guaranteed to be less than alpha when negated
            if (OutOfTime)
                return 99999999;

            bool tactical = searchForPV || move.IsCapture || move.IsPromotion;
            if (canPrune && !tactical)
                continue;

            board.MakeMove(move);

            // Always fully search the first child, search the rest with a null window
            /*
            int eval = -PVS(depth - R, searchForPV ? -beta : -alpha - 1, -alpha, searchPly);

            // Found a move that can raise alpha, do a research
            if (!searchForPV && alpha < eval && eval < beta)
                eval = -PVS(depth - 1, -beta, -alpha, searchPly);
            */

            // Evil local method to save tokens for similar calls to PVS
            int Search(int newDepth, int newAlpha) => -PVS(newDepth, -newAlpha, -alpha, searchPly);

            // Current work in progress LMR (around +40 elo)
            if (movesTried++ == 0 || inQSearch)
                // Always search first node with full depth
                eval = Search(nextDepth, beta);
            else
            {
                // LMR conditions
                eval = isPV || tactical || movesTried < 8 || depth < 3 || inCheck || board.IsInCheck()
                    // Do a full search
                    ? alpha + 1
                    // We're good to reduce -> search with reduced depth and a null window, and if we can raise alpha
                    : Search(nextDepth - depth / 3, alpha + 1);

                // If we raised alpha with the reduced depth search
                if (eval > alpha && 
                    // Update eval with a search with a null window - disgusting syntax that saves a few tokens
                    alpha < (eval = Search(nextDepth, alpha + 1)) && eval < beta)
                    // We raised alpha on the null window search, research with no null window
                    eval = Search(nextDepth, beta);
            }

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
                        historyHeuristics[board.IsWhiteToMove ? 1 : 0, (int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                    break;
                }
            }

            // Will set it to false if in a regular search,
            // but in QSearch will always search every node as if it's first
            searchForPV = inQSearch;
        }

        // Transposition table insertion
        transpositionTable[board.ZobristKey & 0x3FFFFF] = new TTEntry(
            board.ZobristKey,
            bestMove,
            bestEval,
            depth,
            bestEval >= beta ? 3 : bestEval <= originalAlpha ? 2 : 1);

        return bestEval;
    }

    #endregion

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

    // TODO: optimize
    public MyBot()
    {
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
            for (int piece = -1, square; ++piece < 6;)
            {
                ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove);
                while (mask != 0)
                {
                    // Gamephase, middlegame -> endgame
                    gamephase += GamePhaseIncrement[piece];

                    // Material and square evaluation
                    square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (sideToMove ? 56 : 0);
                    middlegame += UnpackedPestoTables[square][piece];
                    endgame += UnpackedPestoTables[square][piece + 6];
                }
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

    // enum Flag
    // {
    //     0 = Invalid,
    //     1 = Exact
    //     2 = Upperbound
    //     3 = Lowerbound,
    // }
    private record struct TTEntry(ulong Hash, Move BestMove, int Score, int Depth, int Flag);

    #endregion
}
