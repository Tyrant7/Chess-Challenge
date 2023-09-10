using ChessChallenge.API;
using System;
using System.Linq;

public class EvilBot : IChessBot
{
    private readonly int[][] UnpackedPestoTables;

    // enum Flag
    // {
    //     0 = Invalid,
    //     1 = Exact,
    //     2 = Upperbound,
    //     3 = Lowerbound
    // }

    // 0x400000 represents the rough number of entries it would take to fill 256mb
    // Very lowballed to make sure I don't go over
    // Hash, Move, Score, Depth, Flag
    private readonly (ulong, Move, int, int, int)[] transpositionTable = new (ulong, Move, int, int, int)[0x400000];

    private readonly Move[] killers = new Move[2048];

    // Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly int[] PieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                           94, 281, 297, 512, 936, 0 }, // Endgame
                           MoveScores = new int[218];

    private int searchMaxTime;

    Move rootMove;

    public EvilBot()
    {
        // Big table packed with data from premade piece square tables
        // Access using using PackedEvaluationTables[square][pieceType] = score
        UnpackedPestoTables = new[] {
            63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m, 75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m, 936945638387574698250991104m, 75531285965747665584902616832m,
            77047302762000299964198997571m, 3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m, 3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m, 2475894077091727551177487608m,
            2458978764687427073924784380m, 3718684080556872886692423941m, 4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m, 9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
            77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m, 5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m, 5619082524459738931006868492m, 649197923531967450704711664m,
            75809334407291469990832437230m, 78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m, 5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m, 76772801025035254361275759599m,
            75502243563200070682362835182m, 78896921543467230670583692029m, 2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m, 3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
            73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m, 3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m, 78580145051212187267589731866m, 75798434925965430405537592305m,
            68369566912511282590874449920m, 72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m, 73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m, 70529879645288096380279255040m,
        }.Select(packedTable =>
        new System.Numerics.BigInteger(packedTable).ToByteArray().Take(12)
                    // Using search max time since it's an integer than initializes to zero and is assgined before being used again 
                    .Select(square => (int)((sbyte)square * 1.461) + PieceValues[searchMaxTime++ % 12])
                .ToArray()
        ).ToArray();
    }

#if DEBUG
    long nodes;
#endif

    public Move Think(Board board, Timer timer)
    {
#if DEBUG
        Console.WriteLine();
        nodes = 0;
#endif

        // Reset history tables
        var historyHeuristics = new int[2, 7, 64];

        // 1/13th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 13;

        // Progressively increase search depth, starting from 2
        for (int depth = 2, alpha = -999999, beta = 999999, eval; ;)
        {
            eval = PVS(depth, alpha, beta, 0, true);

            // Out of time -> soft bound exceeded
            if (timer.MillisecondsElapsedThisTurn > searchMaxTime / 3)
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
                    evalWithMate += $"M{Math.Ceiling((99998 - Math.Abs((double)eval)) / 2)}";
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

        // This method doubles as our PVS and QSearch in order to save tokens
        int PVS(int depth, int alpha, int beta, int plyFromRoot, bool allowNull)
        {
#if DEBUG
            nodes++;
#endif

            // Declare some reused variables
            bool inCheck = board.IsInCheck(),
                canFPrune = false,
                isRoot = plyFromRoot++ == 0,
                notPV = beta - alpha == 1;

            // Draw detection
            if (!isRoot && board.IsRepeatedPosition())
                return 0;

            ulong zobristKey = board.ZobristKey;
            var (entryKey, entryMove, entryScore, entryDepth, entryFlag) = transpositionTable[zobristKey & 0x3FFFFF];

            // Define best eval all the way up here to generate the standing pattern for QSearch
            int bestEval = -9999999,
                newTTFlag = 2,
                movesTried = 0,
                movesScored = 0,
                eval;

            //
            // Evil local method to save tokens for similar calls to PVS (set eval inside search)
            int Search(int newAlpha, int R = 1, bool canNull = true) => eval = -PVS(depth - R, -newAlpha, -alpha, plyFromRoot, canNull);
            //
            //

            // Check extensions
            if (inCheck)
                depth++;

            // TODO: Look into Broxholmes' suggestion

            // Transposition table lookup -> Found a valid entry for this position
            // Avoid retrieving mate scores from the TT since they aren't accurate to the ply
            if (entryKey == zobristKey && !isRoot && entryDepth >= depth && Math.Abs(entryScore) < 50000 && (
                    // Exact
                    entryFlag == 1 ||
                    // Upperbound
                    entryFlag == 2 && entryScore <= alpha ||
                    // Lowerbound
                    entryFlag == 3 && entryScore >= beta))
                return entryScore;

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
            else if (notPV && !inCheck)
            {
                // Reverse futility pruning
                int staticEval = Evaluate();

                // Give ourselves a margin of 74 centipawns times depth.
                // If we're up by more than that margin in material, there's no point in
                // searching any further since our position is so good
                if (depth <= 7 && staticEval - 74 * depth >= beta)
                    return staticEval;

                // NULL move pruning
                if (depth >= 2 && staticEval >= beta && allowNull)
                {
                    board.ForceSkipTurn();

                    // TODO: Play with values: Try a max of 4 instead of 6
                    Search(beta, 3 + depth / 4 + Math.Min(6, (staticEval - beta) / 175), false);
                    board.UndoSkipTurn();

                    // Failed high on the null move
                    if (eval >= beta)
                        return eval;
                }

                // Extended futility pruning
                // Can only prune when at lower depth and behind in evaluation by a large margin
                canFPrune = depth <= 8 && staticEval + depth * 141 <= alpha;

                // Razoring (reduce depth if up a significant margin at depth 3)
                /*
                if (depth == 3 && staticEval + 620 <= alpha)
                    depth--;
                */
            }

            // Generate appropriate moves depending on whether we're in QSearch
            Span<Move> moveSpan = stackalloc Move[218];
            board.GetLegalMovesNonAlloc(ref moveSpan, inQSearch && !inCheck);

            // Order moves in reverse order -> negative values are ordered higher hence the flipped values
            foreach (Move move in moveSpan)
                MoveScores[movesScored++] = -(
                // Hash move
                move == entryMove ? 9_000_000 :
                // MVVLVA
                move.IsCapture ? 1_000_000 * (int)move.CapturePieceType - (int)move.MovePieceType :
                // Killers
                killers[plyFromRoot] == move ? 900_000 :
                // History
                historyHeuristics[plyFromRoot & 1, (int)move.MovePieceType, move.TargetSquare.Index]);

            MoveScores.AsSpan(0, moveSpan.Length).Sort(moveSpan);

            Move bestMove = default;
            foreach (Move move in moveSpan)
            {
                // Out of time -> hard bound exceeded
                // -> Return checkmate so that this move is ignored
                // but better than the worst eval so a move is still picked if no moves are looked at
                // -> Depth check is to disallow timeouts before the bot has finished one round of ID
                if (depth > 2 && timer.MillisecondsElapsedThisTurn > searchMaxTime)
                    return 99999;

                // Futility pruning
                if (canFPrune && !(movesTried == 0 || move.IsCapture || move.IsPromotion))
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
                // Do a full window search if haven't tried any moves or in QSearch
                if (movesTried++ == 0 || inQSearch ||

                    // Otherwise, skip reduced search if conditions are not met
                    (movesTried < 6 || depth < 2 ||

                        // If reduction is applicable do a reduced search with a null window
                        (Search(alpha + 1, 1 + movesTried / 13 + depth / 9 + (notPV ? 1 : 0)) > alpha)) &&

                        // If alpha was above threshold after reduced search, or didn't match reduction conditions,
                        // update eval with a search with a null window
                        alpha < Search(alpha + 1))

                    // We either raised alpha on the null window search, or haven't searched yet,
                    // -> research with no null window
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
                    bestEval = eval;
                    if (eval > alpha)
                    {
                        alpha = eval;
                        bestMove = move;
                        newTTFlag = 1;

                        // Update the root move
                        if (isRoot)
                            rootMove = move;
                    }

                    // Cutoff
                    if (alpha >= beta)
                    {
                        // Update history tables
                        if (!move.IsCapture)
                        {
                            historyHeuristics[plyFromRoot & 1, (int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                            killers[plyFromRoot] = move;
                        }
                        newTTFlag = 3;
                        break;
                    }
                }
            }

            // Gamestate, checkmate and draws
            // -> no moves were looked at and eval was unchanged
            // -> must not be in QSearch and have had no legal moves
            if (bestEval == -9999999)
                return inCheck ? plyFromRoot - 99999 : 0;

            // Transposition table insertion
            transpositionTable[zobristKey & 0x3FFFFF] = (
                zobristKey,
                bestMove == default ? entryMove : bestMove,
                bestEval,
                depth,
                newTTFlag);

            return bestEval;
        }

        int Evaluate()
        {
            int middlegame = 0, endgame = 0, gamephase = 0, sideToMove = 2, piece, square;
            for (; --sideToMove >= 0; middlegame = -middlegame, endgame = -endgame)
                for (piece = 6; --piece >= 0;)
                    for (ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;)
                    {
                        // Gamephase, middlegame -> endgame
                        // Multiply, then shift, then mask out 4 bits for value (0-16)
                        gamephase += 0x00042110 >> piece * 4 & 0x0F;

                        // Material and square evaluation
                        square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;
                        middlegame += UnpackedPestoTables[square][piece];
                        endgame += UnpackedPestoTables[square][piece + 6];

                        // Bishop pair bonus (+14.1 elo alone)
                        if (piece == 2 && mask != 0)
                        {
                            middlegame += 22;
                            endgame += 18;
                        }

                        // Semi-open file bonus for rooks (+14.6 elo alone)
                        /*
                        if (piece == 3 && (0x101010101010101UL << (square & 7) & board.GetPieceBitboard(PieceType.Pawn, sideToMove > 0)) == 0)
                        {
                            middlegame += 13;
                            endgame += 10;
                        }
                        */

                        // Mobility bonus (+15 elo alone)
                        /*
                        if (piece >= 2 && piece <= 4)
                        {
                            int bonus = BitboardHelper.GetNumberOfSetBits(
                                BitboardHelper.GetPieceAttacks((PieceType)piece + 1, new Square(square ^ 56 * sideToMove), board, sideToMove > 0));
                            middlegame += bonus;
                            endgame += bonus * 2;
                        }
                        */
                    }
            return (middlegame * gamephase + endgame * (24 - gamephase)) / (board.IsWhiteToMove ? 24 : -24)
            // Tempo bonus to help with aspiration windows
                + 16;
        }
    }
}