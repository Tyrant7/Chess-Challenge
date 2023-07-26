using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
    {
        TranspositionTable transpositionTable = new();

        // None, Pawn, Knight, Bishop, Rook, Queen, King 
        private readonly int[] PieceValues = { 0, 100, 320, 320, 500, 900, 0 };

        private int searchMaxTime;
        private Timer searchTimer;
        private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime;

        public Move Think(Board board, Timer timer)
        {
            Move[] moves = OrderMoves(board.GetLegalMoves());

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
                    int moveScore = -PVS(board, depth, -9999999, 9999999, board.IsWhiteToMove ? 1 : -1);
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
                    return 10 * (int)victim + (5 - (int)attacker);
            }
        }

        private Move[] OrderMoves(Move[] moves)
            // Little scoring algorithm using MVVLVA
            => moves.OrderByDescending(move => GetMVV_LVA(move.CapturePieceType, move.MovePieceType)).ToArray();

        private int PVS(Board board, int depth, int alpha, int beta, int colour)
        {
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
                    // Default case for PositionInfo.Flag.Upperbound to save tokens
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
                // Do a Quiescence Search with a depth of 3, which will return a score from white's perspective
                // Multiple that score by -1 for black
                return QuiescenceSearch(board, 2, alpha, beta, colour);

            // Search at a deeper depth
            Move[] moves = OrderMoves(board.GetLegalMoves());
            int eval = -9999999;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                eval = -PVS(board, depth - 1, -alpha - 1, -alpha, -colour);
                if (alpha < eval && eval < beta)
                    eval = -PVS(board, depth - 1, -beta, -eval, -colour);

                // Old Negamax search logic
                // eval = Math.Max(eval, -PVS(board, depth - 1, -beta, -alpha, -colour));
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

            return alpha;
        }

        // Quiescence search with help from
        // https://stackoverflow.com/questions/48846642/is-there-something-wrong-with-my-quiescence-search
        private int QuiescenceSearch(Board board, int depth, int alpha, int beta, int colour)
        {
            // Determine if quiescence search should be continued
            int bestValue = colour * Evaluate(board);

            alpha = Math.Max(alpha, bestValue);
            if (alpha >= beta)
                return bestValue;

            // If in check, look into all moves, otherwise just captures
            foreach (Move move in OrderMoves(board.GetLegalMoves(!board.IsInCheck())))
            {
                board.MakeMove(move);
                int eval = -QuiescenceSearch(board, depth - 1, -beta, -alpha, -colour);
                board.UndoMove(move);

                if (OutOfTime)
                    return 0;

                bestValue = Math.Max(bestValue, eval);
                alpha = Math.Max(alpha, bestValue);
                if (alpha >= beta)
                    break;
            }
            return bestValue;
        }

        //
        // Evaluation
        //

        private readonly static int[] DistFromCentre =
        {
        3, 3, 3, 3, 3, 3, 3, 3,
        3, 2, 2, 2, 2, 2, 2, 3,
        3, 2, 1, 1, 1, 1, 2, 3,
        3, 2, 1, 0, 0, 1, 2, 3,
        3, 2, 1, 0, 0, 1, 2, 3,
        3, 2, 1, 1, 1, 1, 2, 3,
        3, 2, 2, 2, 2, 2, 2, 3,
        3, 3, 3, 3, 3, 3, 3, 3
    };

        // NOT CURRENTLY WORTH IT TO HAVE
        // Generates an array identical to the one above, but in 1 fewer token
        // Courtesy of ChatGPT for this code. I have very little idea on how it works
        /*
        private static int[] DistFromCentre = new int[64]
            .Select((_, i) =>
                Math.Max(Math.Max(Math.Abs(i % 8 - 3), Math.Abs(i / 8 - 3)),
                Math.Max(Math.Abs(i % 8 - 4), Math.Abs(i / 8 - 4))) - 1
            ).ToArray();
        */

        public static int GetSquareBonus(Square square, PieceType type, bool isWhite)
        {
            int rank = isWhite ? square.Rank : 7 - square.Rank;
            int centreDist = DistFromCentre[square.Index];

            switch (type)
            {
                // Use some simple equations to determine generally good squares without using a table
                case PieceType.Pawn:
                    // Pawn gets bonuses for being further forward
                    // but also get a bonus for being close to the centre
                    return rank * 5 + (centreDist == 1 ? 10 : 0) + (centreDist == 0 ? 15 : 0);
                case PieceType.Knight:
                    // Get a bonus for being in the centre, and a penalty for being further away
                    return -(centreDist - 1) * 15;
                case PieceType.Bishop:
                    // Same here, but less
                    return -(centreDist - 1) * 10;
                case PieceType.Rook:
                    // Bonus for sitting on second or seventh rank, depending on side
                    return (square.Rank == (isWhite ? 6 : 1)) ? 10 : 0;
                case PieceType.Queen:
                    // Bonus for being in centre, just like knights, but less
                    return -(centreDist - 1) * 5;
                case PieceType.King:
                    // King gets a base +10 bonus for being on back rank, then -10 for every step forward
                    return (-rank * 10) + 10;
            }
            return 0;
        }

        // => instead of return { }
        // because it saves one token
        private int Evaluate(Board board)
        {
            int score = 0;
            foreach (PieceList list in board.GetAllPieceLists())
            {
                // Material evaluation
                int multiplier = list.IsWhitePieceList ? 1 : -1;
                score += PieceValues[(int)list.TypeOfPieceInList] * list.Count * multiplier;

                // Placement evaluation
                foreach (Piece piece in list)
                {
                    score += GetSquareBonus(piece.Square, piece.PieceType, piece.IsWhite) * multiplier;
                }
            }
            return score;
        }
    }

    //
    // Transposition table
    //

    public class TranspositionTable
    {
        private readonly Dictionary<ulong, PositionInfo> table = new();
        private readonly Queue<ulong> addedPositions = new();

        public void Add(ulong zobristKey, PositionInfo parameters)
        {
            if (table.TryAdd(zobristKey, parameters))
            {
                addedPositions.Enqueue(zobristKey);

                // A very rough approximation of how many transposition table entries it would take to reach 256mb
                if (table.Count > 340000)
                    table.Remove(addedPositions.Dequeue());
            }
        }

        public PositionInfo Lookup(ulong zobristKey)
            => table.TryGetValue(zobristKey, out PositionInfo parameters) ? parameters : PositionInfo.Invalid;
    }

    public enum Flag
    {
        Upperbound, Lowerbound, Exact
    }

    public record struct PositionInfo(int score, int depthChecked, Flag flag)
    {
        public readonly bool IsValid => depthChecked > 0;
        public static PositionInfo Invalid => new(int.MinValue, -1, Flag.Exact);
    }

}