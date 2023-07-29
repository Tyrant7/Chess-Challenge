using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
    {

        // TODO: MOST IMPORTANT: Implement endgame tables
        // TODO: MOST IMPORTANT: Remove Quiescence search depth for maximum improvements and use ply to calculate faster mates
        // TODO: History heuristic
        // TODO: Late move reductions
        // TODO: Passed pawn evaluation
        // TODO: Null move pruning
        // TODO: King safety
        // TODO: Check and promotion extensions

        // None, Pawn, Knight, Bishop, Rook, Queen, King 
        private readonly int[] PieceValues = { 0, 100, 320, 320, 500, 900, 0 };

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
            // Cache the board to save precious tokens
            board = newBoard;

            // 1/20th of our remaining time, split among all of the moves
            searchMaxTime = timer.MillisecondsRemaining / 30;
            searchTimer = timer;

            // Progressively increase search depth, starting from 2
            for (int depth = 2; ; depth++)
            {
                // Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms");

                PVS(depth, -9999999, 9999999);

                if (OutOfTime)
                {
                    /*
                    Console.WriteLine("Hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms with an eval of " +
                        TTRetrieve().Score + " centipawns.");
                    */
                    return TTRetrieve().BestMove;
                }
            }
        }

        private int PVS(int depth, int alpha, int beta)
        {
            // Evaluate the gamestate
            if (board.IsDraw())
                // Discourage draws slightly, unless losing
                return -15;
            if (board.IsInCheckmate())
                // Checkmate = 99999
                // SwiftCheckmateBonus = 5000
                return -(99999 + (depth * 5000));

            // Terminal node, calculate score
            if (depth <= 0)
                // Do a Quiescence Search
                return QuiescenceSearch(2, alpha, beta);

            // Transposition table lookup
            TTEntry entry = TTRetrieve();

            // Found a valid entry for this position
            if (entry.Hash == board.ZobristKey && entry.Depth >= depth)
            {
                switch (entry.Flag)
                {
                    // Exact
                    case 1:
                        return entry.Score;
                    // Lowerbound
                    case -1:
                        alpha = Math.Max(alpha, entry.Score);
                        break;
                    // Default case for upperbound (2) to save a token
                    default:
                        beta = Math.Min(beta, entry.Score);
                        break;
                }

                if (alpha >= beta)
                    return entry.Score;
            }

            // Search at a deeper depth
            Move[] moves = GetOrdererdMoves(entry.BestMove, false);

            int bestEval = -9999999;
            Move bestMove = moves[0];

            int i = 0;
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int eval;

                // Always fully search the first child
                if (i == 0)
                    eval = -PVS(depth - 1, -beta, -alpha);
                else
                {
                    // Search with a null window
                    eval = -PVS(depth - 1, -alpha - 1, -alpha);

                    // Research if failed high
                    if (alpha < eval && eval < beta)
                        eval = -PVS(depth - 1, -beta, -eval);
                }

                board.UndoMove(move);

                if (OutOfTime)
                    return 0;

                if (eval > bestEval)
                {
                    // Beta cutoff
                    if (eval >= beta)
                    {
                        TTInsert(move, eval, depth, -1);
                        return eval;
                    }

                    bestMove = move;
                    bestEval = eval;
                    alpha = Math.Max(alpha, bestEval);
                }
                i++;
            }

            // Transposition table insertion
            if (bestEval <= alpha)
                TTInsert(bestMove, bestEval, depth, 2);
            else
                TTInsert(bestMove, bestEval, depth, 1);

            return alpha;
        }

        // Quiescence search with help from
        // https://stackoverflow.com/questions/48846642/is-there-something-wrong-with-my-quiescence-search
        private int QuiescenceSearch(int depth, int alpha, int beta)
        {
            if (OutOfTime)
                return 0;

            // Evaluate the gamestate
            if (board.IsDraw())
                // Discourage draws slightly, unless losing
                return -15;
            if (board.IsInCheckmate())
                // Checkmate = 99999
                // SwiftCheckmateBonus = 5000
                return -(99999 + (depth * 5000));

            // Determine if quiescence search should be continued
            int bestValue = Evaluate();

            alpha = Math.Max(alpha, bestValue);
            if (alpha >= beta)
                return bestValue;

            // If in check, look into all moves, otherwise just captures
            // Also no hash move for Quiescence search
            foreach (Move move in GetOrdererdMoves(Move.NullMove, !board.IsInCheck()))
            {
                board.MakeMove(move);
                int eval = -QuiescenceSearch(depth - 1, -beta, -alpha);
                board.UndoMove(move);

                bestValue = Math.Max(bestValue, eval);
                alpha = Math.Max(alpha, bestValue);
                if (alpha >= beta)
                    break;
            }
            return bestValue;
        }

        //
        // Move Ordering
        //

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
                    return 10 * (int)victim - (int)attacker;
            }
        }

        // Scoring algorithm using MVVLVA
        // Taking into account the best move found from the previous search
        private Move[] GetOrdererdMoves(Move hashMove, bool onlyCaptures)
            => board.GetLegalMoves(onlyCaptures).OrderByDescending(move =>
            GetMVV_LVA(move.CapturePieceType, move.MovePieceType) +
            (move == hashMove ? 100 : 0)).ToArray();

        //
        // Evaluation
        //

        // Big table packed with data from premade piece square tables
        private readonly ulong[,] PackedEvaluationTables = {
        { 58233348458073600, 61037146059233280, 63851895826342400, 66655671952007680 },
        { 63862891026503730, 66665589183147058, 69480338950193202, 226499563094066 },
        { 63862895153701386, 69480338782421002, 5867015520979476,  8670770172137246 },
        { 63862916628537861, 69480338782749957, 8681765288087306,  11485519939245081 },
        { 63872833708024320, 69491333898698752, 8692760404692736,  11496515055522836 },
        { 63884885386256901, 69502350490469883, 5889005753862902,  8703755520970496 },
        { 63636395758376965, 63635334969551882, 21474836490,       1516 },
        { 58006849062751744, 63647386663573504, 63625396431020544, 63614422789579264 }
    };

        private int GetSquareBonus(PieceType type, bool isWhite, int file, int rank)
        {
            // Because arrays are only 4 squares wide, mirror across files
            if (file > 3)
                file = 7 - file;

            // Mirror vertically for white pieces, since piece arrays are flipped vertically
            if (isWhite)
                rank = 7 - rank;

            // First, shift the data so that the correct byte is sitting in the least significant position
            // Then, mask it out
            sbyte unpackedData = unchecked((sbyte)((PackedEvaluationTables[rank, file] >> 8 * ((int)type - 1)) & 0xFF));

            // Invert eval scores for black pieces
            return isWhite ? unpackedData : -unpackedData;
        }

        private int Evaluate()
        {
            int score = 0;
            foreach (PieceList list in board.GetAllPieceLists())
            {
                // Material evaluation
                score += PieceValues[(int)list.TypeOfPieceInList] * list.Count * (list.IsWhitePieceList ? 1 : -1);

                // Placement evaluation
                foreach (Piece piece in list)
                    // Leave out multiplier for white and black since it's worked in already in the GetSquareBonus method
                    score += GetSquareBonus(piece.PieceType, piece.IsWhite, piece.Square.File, piece.Square.Rank);
            }
            return board.IsWhiteToMove ? score : -score;
        }

        //
        // Transposition table
        //

        // 0x400000 represents the rough number of entries it would take to fill 256mb
        // Very lowballed to make sure I don't go over
        private readonly TTEntry[] transpositionTable = new TTEntry[0x400000];

        private TTEntry TTRetrieve()
            => transpositionTable[board.ZobristKey & 0x3FFFFF];

        private void TTInsert(Move bestMove, int score, int depth, sbyte flag)
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
        private record struct TTEntry(ulong Hash, Move BestMove, int Score, int Depth, sbyte Flag);
    }

}