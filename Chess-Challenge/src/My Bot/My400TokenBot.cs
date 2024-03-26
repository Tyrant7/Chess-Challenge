using ChessChallenge.API;
using System;
using System.Linq;

public class My400TokenBot : IChessBot
{
    Move rootMove;

    ulong[] packedTables =
    {
        //Pawn files
        943240312410411277ul,                      
        //Knight files
        4197714699149851955ul,                     
        //Bishop files
        4848484849616963136ul,                        
        //Rook files
        6658122805863343458ul,                          
        //Queen files
        17289018720893200097ul,                         
        //King files
        508351539015584769ul,                        
        //Pawn ranks
        2313471533096915729ul,
        //Knight ranks
        4777002364955480891ul,                        
        //Bishop ranks
        5717702758025484112ul,                          
        //Rook ranks
        9909758167411563417ul,                          
        //Queen ranks
        17073413321325017080ul,                       
        //King ranks
        1447370843669012753ul,    
    };

    Move[] transpositionTable = new Move[0x800000];

    public Move Think(Board board, Timer timer)
    {
        // 1/13th of our remaining time, split among all of the moves
        int searchMaxTime = timer.MillisecondsRemaining / 13,
            // Progressively increase search depth, starting from 2
            depth = 1;

        // Iterative deepening loop
        // Out of time -> soft bound exceeded
        for (; timer.MillisecondsElapsedThisTurn < searchMaxTime / 2; )
        {
            int eval = PVS(depth++, -999999, 999999, 0);
            Console.WriteLine($"Depth: {depth - 1,2} | Eval: {eval,5} | Time: {timer.MillisecondsElapsedThisTurn,5}");
        }
        return rootMove;

        // This method doubles as our PVS and QSearch in order to save tokens
        int PVS(int depth, int alpha, int beta, int plyFromRoot)
        {
            // Declare some reused variables
            bool inCheck = board.IsInCheck(),
                notPV = beta - alpha == 1;

            // Draw detection
            if (plyFromRoot++ > 0 && board.IsRepeatedPosition())
                return 0;

            // Define best eval all the way up here to generate the standing pattern for QSearch
            int bestEval = -9999999,
                sideToMove = 2,
                eval,
                piece,
                square;

            // Check extensions
            if (inCheck)
                depth++;

            // Declare QSearch status here to prevent dropping into QSearch while in check
            bool inQSearch = depth <= 0;
            if (inQSearch)
            {
                // Our evaluation
                bestEval = 0;
                for (; --sideToMove >= 0; bestEval = -bestEval)
                    for (piece = 6; --piece >= 0;)
                        for (ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;

                            // Our evaluation here runs in the increment step of the for loop, making it run after getting our square
                            // This is good because it allows us to have only a single step in our for loop, and as a result remove the braces
                            // Evaluate our file
                            bestEval -= (int)((packedTables[piece] >> square % 8 * 8 & 0xFFul) +
                                     // And our rank
                                     // Unfortunately the divison here is still necessary, as it forces the rank to truncate
                                     (packedTables[piece + 6] >> square / 8 * 8 & 0xFFul)))

                            // Get our square and flip for side to move
                            square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;

                // Flip for white. Consider negative evaluations good to save a token on the "!" here
                if (board.IsWhiteToMove)
                    bestEval = -bestEval;

                // Standpat check -> determine if quiescence search should be continued
                if (bestEval >= beta)
                    return bestEval;
                alpha = Math.Max(alpha, bestEval);
            }

            // Lookup our bestMove in the TT for move ordering
            Move bestMove = transpositionTable[board.ZobristKey & 0x7FFFFF];
            foreach (Move move in board.GetLegalMoves(inQSearch && !inCheck)
                // MVVLVA ordering with hash move
                .OrderByDescending(move => move == bestMove ? 1000 : (int)move.CapturePieceType - (int)move.MovePieceType))
            {
                // Out of time -> hard bound exceeded
                // -> Return checkmate so that this move is ignored
                // but better than the worst eval so a move is still picked if no moves are looked at
                // -> Depth check is to disallow timeouts before the bot has finished one round of ID
                if (timer.MillisecondsElapsedThisTurn > searchMaxTime)
                    return 99999;

                board.MakeMove(move);
                eval = -PVS(depth - 1, -beta, -alpha, plyFromRoot);
                board.UndoMove(move);

                if (eval > bestEval)
                {
                    bestEval = eval;
                    if (eval > alpha)
                    {
                        alpha = eval;
                        bestMove = move;

                        // Update the root move
                        // Ply should be 1 now since we incremented last time we checked
                        if (plyFromRoot == 1)
                            rootMove = move;
                    }

                    // Cutoff
                    if (alpha >= beta)
                        break;
                }
            }

            // Transposition table insertion
            transpositionTable[board.ZobristKey & 0x7FFFFF] = bestMove;

            return bestEval == -9999999
                 // Gamestate, checkmate and draws
                 // -> no moves were looked at and eval was unchanged
                 // -> must not be in QSearch and have had no legal moves
                 ? inCheck ? plyFromRoot - 99999 : 0
                 : bestEval;
        }
    }
}
