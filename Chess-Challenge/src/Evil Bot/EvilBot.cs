using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
    {
        TranspositionTable transpositionTable = new();

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

        private int searchMaxTime;
        private Timer searchTimer;
        private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime;

        public Move Think(Board board, Timer timer)
        {
            Move[] moves = OrderMoves(board, board.GetLegalMoves());

            // One less than the minimum evaluation so that there will never be no move chosen even if there are no legal moves
            int bestScore = -10000000;
            Move bestMove = moves[0];

            // One fifteenth of our remaining time, split among all of the moves
            searchMaxTime = timer.MillisecondsRemaining / 15;
            searchTimer = timer;

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
            => move.CapturePieceType != PieceType.None ? MVV_LVA[(int)move.CapturePieceType - 1, (int)move.MovePieceType - 1] : 0;

        private int Negamax(Board board, int depth, int alpha, int beta, int colour)
        {
            if (OutOfTime)
                return 0;

            int originalAlpha = alpha;

            // Transposition table lookup
            PositionInfo position = transpositionTable.Lookup(board.ZobristKey);
            if (position.IsValid && position.depthChecked >= depth)
            {
                switch (position.flag)
                {
                    case Flag.Exact:
                        return position.score;
                    case Flag.Lowerbound:
                        alpha = Math.Max(alpha, position.score);
                        break;
                    // Default case for PositionInfo.Flag.Lowerbound to save tokens
                    default:
                        beta = Math.Min(beta, position.score);
                        break;
                }

                if (alpha >= beta)
                    return position.score;
            }

            // Evaluate the gamestate
            if (board.IsDraw())
                return 0;
            if (board.IsInCheckmate())
                // Checkmate = 99999
                // SwiftCheckmateBonus = 5000
                return colour * (board.IsWhiteToMove ? -99999 - (depth * 5000) : 99999 + (depth * 5000));

            // Terminal node, calculate score
            if (depth <= 0)
                // Score from white's perspective, times -1 for black
                return colour * (EvaluateMaterial(board) + EvaluateSquares(board));

            // Search at a deeper depth
            Move[] moves = OrderMoves(board, board.GetLegalMoves());
            int eval = -9999999;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                eval = Math.Max(eval, -Negamax(board, depth - 1, -beta, -alpha, -colour));
                board.UndoMove(move);

                if (OutOfTime)
                    return 0;

                // If there is a worse branching path, cut this branch
                // as this move won't be benificial assuming the opponent plays the best move
                alpha = Math.Max(alpha, eval);
                if (alpha >= beta)
                    break;
            }

            // Transposition table storage
            Flag flag = Flag.Exact;
            if (eval <= originalAlpha)
                flag = Flag.Upperbound;
            else if (eval >= beta)
                flag = Flag.Lowerbound;

            PositionInfo positionInfo = new(eval, depth, flag);
            transpositionTable.Add(board.ZobristKey, positionInfo);

            return eval;
        }

        // => instead of return { }
        // because it saves one token
        private int EvaluateMaterial(Board board)
            => board.GetAllPieceLists()
                .Sum(list => PieceValues[(int)list.TypeOfPieceInList] * list.Count * (list.IsWhitePieceList ? 1 : -1));

        private int EvaluateSquares(Board board)
            => board.GetAllPieceLists()
                .SelectMany(list => list)
                .Sum(piece => (piece.IsWhite ? 1 : -1) * PieceSquareTable.GetSquareBonus(piece.Square, piece.PieceType, piece.IsWhite));
    }
}