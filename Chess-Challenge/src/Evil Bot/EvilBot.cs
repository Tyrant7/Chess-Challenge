using ChessChallenge.API;
using System;
using System.Linq;

public class EvilBot : IChessBot
{
    // Pawn, Knight, Bishop, Rook, Queen, King 
    private static readonly int[] PieceValues = { 85, 303, 311, 417, 884, 0, // Middlegame
                                                 112, 331, 337, 580, 1111, 0, }, // Endgame
    MoveScores = new int[218],

    // Big table packed with data from premade piece square tables
    // Access using using PackedEvaluationTables[square * 16 + pieceType] = score
    UnpackedPestoTables =
        new[] {
            59451448864524338001597731328m, 70297936153099270235372369920m, 71546770655759800448312074240m, 78964725501995133623336103936m, 76793471081097277139317690624m, 78027755879020984261820077568m, 76754771103230419112346041344m, 61623855522740025206790601216m,
            77071542558188950228672375347m, 3130041172701389648840031801m, 5018378654430835856770407729m, 2870079712769514283296824390m, 5667548133573827106368920637m, 8110758880242181296406805300m, 8095024029349579853503529731m, 2492833095129435013200350447m,
            628740724180444160622460668m, 4360694674712635777381242626m, 8099897587059896946498675483m, 9342659088733365821012848156m, 9976103197600551162575996450m, 9028295965483352796873836855m, 9306329921943772812013611294m, 4343679950149864891474252548m,
            77697761688282250833068490995m, 5609501083037295436368382718m, 9022322210355851632431143170m, 11829424536278156635329603588m, 11840276405471012209420868370m, 11210416589768263512308133140m, 9341407772286001669087038990m, 4677357644025564526222055678m,
            75533760804066413491965917418m, 3132412022798911576025137655m, 7783149762686656643580103927m, 11208027109652607354447269378m, 11511458064233210962270098435m, 9027115467527842737865494789m, 6536685684726919673709727748m, 3122674188792300577635761649m,
            74282494227470008748515519466m, 326466768824046849819802612m, 4683426070573634623011096054m, 7462756012216654057758722037m, 7467572862848926102311407105m, 5914088943036848613743986689m, 2185700139537467934989945616m, 78306927871678352130573137657m,
            70260393247401643753538446569m, 76141826786834425963180780533m, 313173122634429358295414769m, 2488021375106781445697306603m, 3418870699646087659834900473m, 1538981457620295479601201421m, 76723258770281358508237781014m, 72994950155497045449937319925m,
            64068289144638863735294183680m, 67473838121788360080042093312m, 71190094760757806174662484224m, 76129751751074526739819719424m, 70569878258012769137276087808m, 75518016359557778914059548672m, 69617230359148433123381275648m, 64044058571390684370663495680m,
        }.SelectMany(packedTable =>
        decimal.GetBits(packedTable).SelectMany(BitConverter.GetBytes)
                    // No point in only taking 12 bytes. Since we never access the last 4 anyway, we can just leave them as garbage
                    .Select((square, index) => (int)((sbyte)square * 1.461) + PieceValues[index % 12])
                .ToArray()
        ).ToArray();

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

    Move rootMove;

#if DEBUG
    long nodes;
#endif

    public Move Think(Board board, Timer timer)
    {
#if DEBUG
        Console.WriteLine();
        nodes = 0;
#endif

        // Reset history tables (thank you, Broxholme for this approach at history)
        var historyHeuristics = new int[65536];

        // 1/13th of our remaining time, split among all of the moves
        int searchMaxTime = timer.MillisecondsRemaining / 13,
            // Progressively increase search depth, starting from 2
            depth = 2, alpha = -999999, beta = 999999, eval;

        // Iterative deepening loop
        for (; ; )
        {
            eval = PVS(depth, alpha, beta, 0, true);

            // Out of time -> soft bound exceeded
            if (timer.MillisecondsElapsedThisTurn > searchMaxTime / 3)
                return rootMove;

            // Fell outside window, retry with a full window search
            // (more token efficient than gradual widening, without any elo loss)
            if (eval <= alpha || eval >= beta)
            {
                alpha -= 999999;
                beta += 999999;
            }
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
                notRoot = plyFromRoot++ > 0,
                notPV = beta - alpha == 1;

            // Draw detection
            if (notRoot && board.IsRepeatedPosition())
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

            // Transposition table lookup -> Found a valid entry for this position
            // Avoid retrieving mate scores from the TT since they aren't accurate to the ply
            // No need for EXACT flag if we just invert some conditions. Thank you Broxholme for this suggestion
            if (entryKey == zobristKey && notPV && entryDepth >= depth && Math.Abs(entryScore) < 50000 &&
                    // Lowerbound
                    entryFlag != 3 | entryScore >= beta &&
                    // Upperbound
                    entryFlag != 2 | entryScore <= alpha)
                return entryScore;

            // Internal Iterative Reductions
            /*
            if (entryMove == default && depth > 4)
                depth--;
            */

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

                    // TODO: Play with values: Try a max of 4 or 5 instead of 6 along with a different divisor
                    Search(beta, 3 + depth / 4 + Math.Min(6, (staticEval - beta) / 175), false);
                    board.UndoSkipTurn();

                    // Failed high on the null move
                    if (eval >= beta)
                        return eval;
                }

                // Extended futility pruning
                // Can only prune when at lower depth and behind in evaluation by a large margin
                canFPrune = depth <= 8 && staticEval + depth * 141 <= alpha;
            }

            // Generate appropriate moves depending on whether we're in QSearch
            Span<Move> moveSpan = stackalloc Move[218];
            board.GetLegalMovesNonAlloc(ref moveSpan, inQSearch && !inCheck);

            // Order moves in reverse order -> negative values are ordered higher hence the flipped values
            foreach (Move move in moveSpan)
                MoveScores[movesScored++] = -(
                // Hash move
                move == entryMove ? 9_000_000 :
                // Promotions
                // move.IsPromotion ? 8_000_000 : 
                // MVVLVA
                move.IsCapture ? 1_000_000 * (int)move.CapturePieceType - (int)move.MovePieceType :
                // Killers
                killers[plyFromRoot] == move ? 900_000 :
                // History
                historyHeuristics[move.RawValue]);

            MoveScores.AsSpan(0, moveSpan.Length).Sort(moveSpan);

            Move bestMove = entryMove;
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

                // Ciekce's LMP
                /*
                if (quiet or losing capture
                    && best score is not mated
                    && !pv
                    && depth <= 8
                    && legal moves made >= 3 + depth * depth / (improving ? 1 : 2))
                    break;
                */

                board.MakeMove(move);

                //
                // Ugly syntax warning
                //

                // LMR + PVS
                // Do a full window search if haven't tried any moves or in QSearch
                if (movesTried++ == 0 || inQSearch ||

                    // Otherwise, skip reduced search if conditions are not met
                    (movesTried < 6 || depth < 2 ||

                        // If reduction is applicable do a reduced search with a null window
                        (Search(alpha + 1, Math.Min((notPV ? 2 : 1) + movesTried / 13 + depth / 9, depth)) > alpha)) &&

                        // If alpha was above threshold after reduced search, or didn't match reduction conditions,
                        // update eval with a search with a null window
                        alpha < Search(alpha + 1))

                    // We either raised alpha on the null window search, or haven't searched yet,
                    // -> research with no null window
                    Search(beta);

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
                        if (!notRoot)
                            rootMove = move;
                    }

                    // Cutoff
                    if (alpha >= beta)
                    {
                        // Update history tables
                        if (!move.IsCapture)
                        {
                            // Note:
                            // This will possibly mess up ordering on promotions due to them having different key values in the array,
                            // but the history relying on the information of from->to,
                            // So far I have not found it worth adding promotion ordering, but be aware of that
                            historyHeuristics[move.RawValue] += depth * depth;
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
                bestMove,
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
                        middlegame += UnpackedPestoTables[square * 16 + piece];
                        endgame += UnpackedPestoTables[square * 16 + piece + 6];

                        // Bishop pair bonus
                        if (piece == 2 && mask != 0)
                        {
                            middlegame += 24;
                            endgame += 63;
                        }

                        // Doubled pawns penalty (brought to my attention by Y3737)
                        if (piece == 0 && (0x101010101010101UL << (square & 7) & mask) > 0)
                        {
                            middlegame -= 22;
                            endgame -= 35;
                        }

                        // Open file bonus
                        /*
                        if (piece == 3 && BitboardHelper.GetNumberOfSetBits(0x101010101010101UL << (square & 7) & board.AllPiecesBitboard) == 1)
                        {
                            middlegame += 25;
                            endgame += 9;
                        }
                        */

                        // Semi-open file bonus for rooks
                        if (piece == 3 && (0x101010101010101UL << (square & 7) & board.GetPieceBitboard(PieceType.Pawn, sideToMove > 0)) == 0)
                        {
                            middlegame += 29;
                            endgame += 17;
                        }

                        // Mobility bonus
                        /*
                        if (piece >= 2 && piece <= 4)
                        {
                            double bonus = BitboardHelper.GetNumberOfSetBits(
                                BitboardHelper.GetPieceAttacks((PieceType)piece + 1, new Square(square ^ 56 * sideToMove), board, false));
                            middlegame += (int)(bonus * 5.23745);
                            endgame += (int)(bonus * 4.25251);
                        }
                        */
                    }
            return (middlegame * gamephase + endgame * (24 - gamephase)) / (board.IsWhiteToMove ? 24 : -24)
            // Tempo bonus to help with aspiration windows
                + 16;
        }
    }
}