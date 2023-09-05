#define DEBUG

using ChessChallenge.API;
using System;
using System.Linq;

// TODO: test performance using piecevalues as integers instead of shorts ----
// TODO: Look into adding a soft and hard bound for time management
// TODO: Look into Broxholmes' suggestion
// TODO: Optimize PST unpacking
// TODO: LMR log formula
// TODO: LMP after new LMR reduction formula
// TODO: Explore butterfly tables or something similar

public class MyBot : IChessBot
{
    // Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly short[] PieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                           94, 281, 297, 512, 936, 0 }; // Endgame

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
    private readonly int[] moveScores = new int[218];

    private int searchMaxTime;

    Move rootMove;

    public MyBot()
    {
        // Big table packed with data from premade piece square tables
        // Access using using PackedEvaluationTables[square][pieceType] = score
        UnpackedPestoTables = (new[] {
               49963896677135869199843363m, 12157341685332238687190525219m, 17418624817495840828516490275m, 17424655333753799518460923171m, 19591036253634292804283382563m, 27627975065827113910386826531m, 24212740585781868616715169571m, 17725630338909050058392081187m,
            19228353767419218320000824709m, 28248158827307447300686763433m, 27334211109363750487860495968m, 28273536677633797799754965634m, 28294022468956437828288419175m, 34753341271731719071624707233m, 30117134531444035957867971909m, 26367022840688190996605005080m,
            26033378501079847164197359389m, 28231205456961757759839648298m, 30091742443080106259817276221m, 27664209934037077133641826114m, 29209212389967669802267169380m, 36931797616097216998953779035m, 36602960237036212069630199612m, 26996845191313558226343078415m,
            20490439359300546273855042837m, 29797954744946919087415988016m, 30419389896599906306413138217m, 31373175644535553776217797432m, 31078202559147813936800703290m, 33224041092019382447915909935m, 31078188188130739812438741044m, 23934659666807078932767085580m,
            17370197041091032557304967432m, 21758607377909416796425781793m, 29484866644215262053065002526m, 30447152818490419087992982319m, 31356222311532819507655776820m, 30121904382751810780988870953m, 25795149188876773894362272557m, 19586091899969712615671100938m,
            17063096715300289433457288969m, 22001577745354674128843535647m, 26385119358499537140509149727m, 29469108051850741270987245593m, 30091676569733043040525783334m, 27934929110302548833329844006m, 25141120244947794897460827972m, 20183277532056286358975311383m,
            14579953598818842612468179200m, 19530504757937639195506985250m, 24164345962354154380074316303m, 26966645567995084297611672332m, 27276078724235878491479440660m, 24172765904176786291485544507m, 21371675224500677890180083785m, 17662728365978185821817367309m,
             6520030827261036734436152611m, 12406342845665183717458405667m, 16436905994390009507312059427m, 19506350093543403096490466595m, 14291025143968649967453957411m, 18591136376567005219387761187m, 15510873869027537815891307299m,  9605157681044739148385969955m
        }).Select(packedTable =>
        new System.Numerics.BigInteger(packedTable).ToByteArray().Take(12)
                    // Using search max time since it's an integer than initializes to zero and is assgined before being used again 
                    .Select(square => (int)(square + 0x01283211C521F823 >> searchMaxTime++ % 6 * 10))
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
        int[,,] historyHeuristics = new int[2, 7, 64];

        // 1/30th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 30;

        // Progressively increase search depth, starting from 2
        for (int depth = 2, alpha = -999999, beta = 999999, eval; ;)
        {
            eval = PVS(depth, alpha, beta, 0, true);

            // Out of time
            if (timer.MillisecondsElapsedThisTurn > searchMaxTime)
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
                isRoot = plyFromRoot++ == 0;

            // Draw detection
            if (!isRoot && board.IsRepeatedPosition())
                return 0;

            ulong zobristKey = board.ZobristKey;
            ref var entry = ref transpositionTable[zobristKey & 0x3FFFFF];

            // Define best eval all the way up here to generate the standing pattern for QSearch
            int bestEval = -9999999,
                originalAlpha = alpha,
                movesTried = 0,
                entryScore = entry.Item3,
                entryFlag = entry.Item5,
                movesScored = 0,
                eval;

            //
            // Evil local method to save tokens for similar calls to PVS (set eval inside search)
            int Search(int newAlpha, int R = 1, bool canNull = true) => eval = -PVS(depth - R, -newAlpha, -alpha, plyFromRoot, canNull);
            //
            //

            // Transposition table lookup -> Found a valid entry for this position
            // Avoid retrieving mate scores from the TT since they aren't accurate to the ply
            if (entry.Item1 == zobristKey && !isRoot && entry.Item4 >= depth && Math.Abs(entryScore) < 50000 && (
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
            else if (beta - alpha == 1 && !inCheck)
            {
                // Reverse futility pruning
                int staticEval = Evaluate();

                // Give ourselves a margin of 96 centipawns times depth.
                // If we're up by more than that margin in material, there's no point in
                // searching any further since our position is so good
                if (depth <= 10 && staticEval - 96 * depth >= beta)
                    return staticEval;

                // NULL move pruning
                if (depth >= 2 && allowNull)
                {
                    board.ForceSkipTurn();
                    Search(beta, 3 + (depth >> 2), false);
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
                moveScores[movesScored++] = -(
                // Hash move
                move == entry.Item2 ? 9_000_000 :
                // MVVLVA
                move.IsCapture ? 1_000_000 * (int)move.CapturePieceType - (int)move.MovePieceType :
                // Killers
                killers[plyFromRoot] == move ? 900_000 :
                // History
                historyHeuristics[plyFromRoot & 1, (int)move.MovePieceType, move.TargetSquare.Index]);

            moveScores.AsSpan(0, moveSpan.Length).Sort(moveSpan);

            // Gamestate, checkmate and draws
            if (!inQSearch && moveSpan.IsEmpty)
                return inCheck ? plyFromRoot - 99999 : 0;

            Move bestMove = default;
            foreach (Move move in moveSpan)
            {
                // Out of time -> return checkmate so that this move is ignored
                // but better than the worst eval so a move is still picked if no moves are looked at
                // Depth check is to disallow timeouts before the bot has found a move
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
                if (movesTried++ == 0 || inQSearch)
                    // Always search first node with full depth
                    Search(beta);

                // Set eval to appropriate alpha to be read from later
                // -> if reduction is applicable do a reduced search with a null window,
                // othewise automatically set alpha be above the threshold
                else if ((movesTried < 6 || depth < 2
                        ? eval = alpha + 1
                        : Search(alpha + 1, 3)) > alpha &&

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
                    bestEval = eval;
                    if (eval > alpha)
                    {
                        alpha = eval;
                        bestMove = move;

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
                        break;
                    }
                }
            }

            // Transposition table insertion
            entry = new(
                zobristKey,
                bestMove == default ? entry.Item2 : bestMove,
                bestEval,
                depth,
                bestEval >= beta ? 3 : bestEval <= originalAlpha ? 2 : 1);

            return bestEval;
        }

        int Evaluate()
        {
            int middlegame = 0, endgame = 0, gamephase = 0, sideToMove = 2, piece, square;
            for (; --sideToMove >= 0; middlegame = -middlegame, endgame = -endgame)
                for (piece = -1; ++piece < 6;)
                    for (ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;)
                    {
                        // Gamephase, middlegame -> endgame
                        // Multiply, then shift, then mask out 4 bits for value (0-16)
                        gamephase += 0x00042110 >> piece * 4 & 0x0F;

                        // Material and square evaluation
                        square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;
                        middlegame += UnpackedPestoTables[square][piece];
                        endgame += UnpackedPestoTables[square][piece + 6];
                    }
            // Tempo bonus to help with aspiration windows
            return (middlegame * gamephase + endgame * (24 - gamephase)) / 24 * (board.IsWhiteToMove ? 1 : -1) + gamephase / 2;
        }
    }
}
