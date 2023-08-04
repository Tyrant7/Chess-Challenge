using ChessChallenge.API;
using System;
using System.Linq;
using System.Security.Principal;

// TODO: Try swapping out the draw and checkmate logic to be more concise and make the bot faster
// TODO: Tune NMP with a small and big R value depending on depth (depth > 6) R = 3, else R = 2

// Heuristics
// TODO: Aspiration Windows
// TODO: Late move reductions
// TODO: Passed pawn evaluation
// TODO: Experiment with new sorting techniques for moves

public class MyBot : IChessBot
{
    private int searchMaxTime;
    private Timer searchTimer;

    // Return true if out of time AND a valid move has been found
    private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime &&
                              TTRetrieve.Hash == board.ZobristKey &&
                              TTRetrieve.BestMove != Move.NullMove;

    Board board;

    public Move Think(Board newBoard, Timer timer)
    {
        // Cache the board to save precious tokens
        board = newBoard;

        // Reset history heuristics and killer moves
        historyHeuristics = new int[2, 7, 64];

        // 1/30th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 30;
        searchTimer = timer;

        // Progressively increase search depth, starting from 2
        for (int depth = 2; ;)
        {
            Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms"); // #DEBUG

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
                return TTRetrieve.BestMove;
        }
    }

    #region Search

    // This method doubles as our PVS and QSearch in order to save tokens
    private int PVS(int depth, int alpha, int beta, int searchPly, bool allowNull = true)
    {
        // Use this local function when updating alpha to save tokens
        /*
        void UpdateAlpha(int contender) => alpha = Math.Max(alpha, contender);
        */

        // Evaluate the gamestate
        if (board.IsDraw())
            // Discourage draws slightly
            return 0;
        if (board.IsInCheckmate())
            // Checkmate = 99999
            return -(99999 - searchPly);

        // Declare some reused variables after gamestate
        bool inQSearch = depth <= 0;
        bool inCheck = board.IsInCheck();
        bool isPVNode = beta - alpha > 1;

        // Define best eval all the way up here to generate the standing pattern
        int bestEval = -9999999;
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

            // IMPORTANT NOTE: Increment the value of searchPly after comparison to save tokens since blocks below this do not care
            // about the value and are simply passing searchPly + 1 to the next iteration

            // Transposition table lookup -> Found a valid entry for this position
            if (TTRetrieve.Hash == board.ZobristKey && searchPly++ > 0 &&
                TTRetrieve.Depth >= depth)
            {
                // Cache this value to save tokens by not referencing using the . operator
                int score = TTRetrieve.Score;

                // Exact
                if (TTRetrieve.Flag == 1)
                    return score;

                // Lowerbound
                if (TTRetrieve.Flag == -1)
                    alpha = Math.Max(alpha, score);
                // Upperbound
                else
                    beta = Math.Min(beta, score);

                if (alpha >= beta)
                    return score;
            }

            // If this node is NOT part of the PV
            if (!isPVNode)
            {
                int eval;
                if (depth < 3 && !inCheck)
                {
                    // Static move pruning
                    eval = Evaluate();

                    // Give ourselves a margin of 120 centipawns times depth.
                    // If we're up by more than that margin, there's no point in
                    // searching any further since our position is so good
                    if (eval - 120 * depth >= beta)
                        return eval - 120 * depth;
                }

                // NULL move pruning
                if (depth > 2 && allowNull && board.TrySkipTurn())
                {
                    eval = -PVS(depth - 3, -beta, 1 - beta, searchPly, false);
                    board.UndoSkipTurn();

                    // Failed high on the null move
                    if (eval >= beta)
                        return eval;
                }
            }
        }

        // Generate appropriate moves depending on whether we're in QSearch
        // Using var to save a single token
        var moves = inQSearch ? GetOrderedMoves(Move.NullMove, searchPly, !inCheck) : 
                                GetOrderedMoves(TTRetrieve.BestMove, searchPly, false);

        Move bestMove = inQSearch ? Move.NullMove : moves[0];
        int index = 0;
        foreach (Move move in moves)
        {
            // Calculate reduction factor
            int R = inQSearch || move.IsCapture || move.IsPromotion || inCheck || isPVNode || depth < 3 || index < 5 
                ? 1 
                : 2;

            board.MakeMove(move);

            // Always fully search the first child, search the rest with a null window
            int eval = -PVS(depth - R, index == 0 ? -beta : -alpha - 1, -alpha, searchPly); 

            // Found a move that can raise alpha, do a research without reduction
            if (index > 0 && alpha < eval && eval < beta)
                eval = -PVS(depth - 1, -beta, -alpha, searchPly);

            // Reduced search raised alpha, research it
            if (R > 1 && alpha < eval)
                eval = -PVS(depth - 1, -beta, -alpha, searchPly);

            board.UndoMove(move);

            if (OutOfTime)
                return 0;

            if (eval > bestEval)
            {
                bestMove = move;
                bestEval = eval;
                alpha = Math.Max(eval, alpha);

                // Cutoff
                if (alpha >= beta)
                {
                    // Update history tables
                    if (!move.IsCapture)
                        historyHeuristics[board.IsWhiteToMove ? 1 : 0, (int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;

                    if (!inQSearch)
                        TTInsert(move, eval, depth, -1);

                    return eval;
                }
            }

            // No LMR or PVS in QSearch
            if (!inQSearch)
                index++;
        }

        // Transposition table insertion
        if (!inQSearch)
            TTInsert(bestMove, bestEval, depth, bestEval <= alpha ? 2 : 1);

        return alpha;
    }

    #endregion

    #region Move Ordering

    int[,,] historyHeuristics;

    // Scoring algorithm using MVVLVA
    // Taking into account the best move found from the previous search
    private Move[] GetOrderedMoves(Move hashMove, int searchPly, bool onlyCaputures)
        => board.GetLegalMoves(onlyCaputures).OrderByDescending(move =>
        {
            // Cache this here to save tokens
            int victim = (int)move.CapturePieceType;

            // MVVLVA: 0 = None, 6 = King, no bonuses for these two
            return (victim == 0 || victim == 6 ? 0 : 1000 * victim - (int)move.MovePieceType) +

            // Always check the hash move first
            (move == hashMove ? 9000 : 0) +

            // History heuristic
            historyHeuristics[board.IsWhiteToMove ? 1 : 0, (int)move.MovePieceType, move.TargetSquare.Index];
        }).ToArray();

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
            for (int piece = -1, square; ++piece < 6; )
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

    private TTEntry TTRetrieve
        => transpositionTable[board.ZobristKey & 0x3FFFFF];

    private void TTInsert(Move bestMove, int score, int depth, int flag)
    {
        if (depth > TTRetrieve.Depth)
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
