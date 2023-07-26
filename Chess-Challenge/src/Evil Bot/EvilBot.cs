using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        ulong[] historyHeuristic;
        int nodes = 0;
        Board board;

        public Move Think(Board startBoard, Timer timer)
        {
            historyHeuristic = new ulong[4096];
            board = startBoard;
            nodes = 0;

            for (int i = 2; i < 30; i++)
            {
                if (AlphaBeta(-10000, 10000, i) >= 9000 || timer.MillisecondsElapsedThisTurn > 300) break;
            }

            Entry entry = TableGet();
            Console.WriteLine("Depth: {0}, Eval: {1}, Time: {2} Nodes: {3}\n", entry.depth, entry.score, timer.MillisecondsElapsedThisTurn, nodes);
            nodes = 0;

            return entry.bestMove;
        }

        int AlphaBeta(int alpha, int beta, int depth)
        {
            if (board.IsRepeatedPosition()) return -30;

            if (depth <= 0)
                return QSearch(alpha, beta);

            Entry entry = TableGet();
            if (entry.hash != board.ZobristKey)
            {
                // Internal iterative deepening
                AlphaBeta(alpha, beta, depth - 2);
                entry = TableGet();
            }

            else if (entry.depth >= depth)
            {
                if (entry.boundType == 0)
                    return entry.score; // Exact 
                else if (entry.boundType == -1)
                    alpha = Math.Max(alpha, entry.score); // Lowerbound TODO: change to non negative number for 
                else
                    beta = Math.Min(beta, entry.score); // Upperbound
                if (alpha >= beta) return entry.score;
            }

            var moves = GetOrderedMoves(false, entry.bestMove); // Pass table move in, maybe we can just TableGet() twice?

            bool inCheck = board.IsInCheck();
            if (moves.Length == 0)
            {
                if (inCheck)
                    return -(9000 + board.PlyCount);
                else
                    return 0;
            }

            int bestScore = -10000;
            Move bestMove = moves[0];

            int i = 0;
            foreach (Move move in moves)
            {
                int R = 1;
                if (i > 4 && !move.IsCapture && depth > 2 && !inCheck) R = 2 + i / 12;

                board.MakeMove(move);
                int score = -AlphaBeta(-beta, -alpha, depth - R);
                board.UndoMove(move);

                if (score > bestScore)
                {
                    if (score >= beta)
                    {
                        TableSet(move, score, -1, depth);
                        if (!move.IsCapture)
                            historyHeuristic[move.StartSquare.Index + (64 * move.TargetSquare.Index)] += (ulong)1 << depth;
                        return score;
                    }

                    bestMove = move;
                    bestScore = score;
                    alpha = Math.Max(alpha, score);
                }
                i++;
            }

            if (bestScore <= alpha)
                TableSet(bestMove, bestScore, 1, depth);
            else
                TableSet(bestMove, bestScore, 0, depth);

            return bestScore;
        }


        int QSearch(int alpha, int beta)
        {
            bool inCheck = board.IsInCheck();

            // When we are in check we allow it to search non captures so we don't reach an illegal position
            var moves = GetOrderedMoves(!inCheck, Move.NullMove);

            if (moves.Length == 0 && inCheck)
                if (inCheck)
                    return -9000;
            // We don't do stalemate check because otherwise itll say its a draw everytime no capture is possible

            // We get an evaluation of the current "standing pattern" as its possible that all captures in a position are bad, and none should be played
            // We obviously can't just stand around when in check either

            if (!inCheck)
            {
                int standPat = evaluation();
                if (standPat >= beta)
                    return beta;
                alpha = Math.Max(alpha, standPat);
            }

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int score = -QSearch(-beta, -alpha);
                board.UndoMove(move);

                if (score >= beta)
                    return score;
                alpha = Math.Max(alpha, score);
            }

            return alpha;
        }

        // Essentially this performs the very simple Selection Sort algorithm, to order moves based on a numerical priority
        // Ideally you would sort this as each move is being played, as a beta cutoff may occur in the first few moves
        // Thus you can save some time by not sorting the remaining moves 

        // TODO: See if we can get Array.Sort(keys, values) to work as this could in theory provide a saving of approx 100 tokens

        Move[] GetOrderedMoves(bool onlyCaptures, Move hashMove)
        {
            var moves = board.GetLegalMoves(onlyCaptures); //TODO: investigate if GetLegalMovesNonAlloc() is better
            var priority = new int[moves.Length];

            int i = 0;
            foreach (Move move in moves)
            {
                if (move == hashMove)
                    priority[i] = 100;
                else if (move.IsCapture)
                    priority[i] = 10 * (int)move.CapturePieceType - (int)move.MovePieceType;
                else
                    priority[i] = 64 - BitOperations.LeadingZeroCount(historyHeuristic[move.StartSquare.Index + (64 * move.TargetSquare.Index)]);
                i++;
            }

            // TODO:  PLEASE REWRITE THIS DUMBASS SORT
            // Selection Sort
            for (i = 0; i < moves.Length; i++)
            {
                // Loop through all unsorted moves to find max priority
                int chosenIndex = i;
                int highestPriority = -1000000;
                for (int j = i; j < moves.Length; j++)
                {
                    if (priority[j] > highestPriority)
                    {
                        chosenIndex = j;
                        highestPriority = priority[j];
                    }
                }
                // Then put the highest priority move at the front
                // Repeat until no unsorted moves remain

                (priority[i], priority[chosenIndex]) = (priority[chosenIndex], priority[i]);
                (moves[i], moves[chosenIndex]) = (moves[chosenIndex], moves[i]);

            }

            return moves;
        }


        int evaluation()
        {
            nodes += 1;
            var pieceWeights = new int[] { 100, 280, 320, 500, 900 };
            var pieceTypes = new PieceType[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen };
            ulong bordermagic = 18411139144890810879;

            int score = 0;

            for (int i = 0; i < 5; i++)
            {
                ulong whitePieces = board.GetPieceBitboard(pieceTypes[i], true);
                ulong blackPieces = board.GetPieceBitboard(pieceTypes[i], false);
                score += pieceWeights[i] * (BitOperations.PopCount(whitePieces) - BitOperations.PopCount(blackPieces));
                score -= 15 * (BitOperations.PopCount(whitePieces & bordermagic) - BitOperations.PopCount(blackPieces & bordermagic));

            }

            if (!board.IsWhiteToMove)
                score = -score;

            return score;
        }


        Entry[] transpositionTable = new Entry[1000000];

        Entry TableGet()
        {
            return transpositionTable[board.ZobristKey % 1000000];
        }

        struct Entry
        {
            public ulong hash;
            public int depth, score;
            public Move bestMove;
            public sbyte boundType;
        }

        void TableSet(Move bestMove, int score, sbyte boundType, int depth)
        {
            if (depth > 1)
                transpositionTable[board.ZobristKey % 1000000] = new Entry
                {
                    hash = board.ZobristKey,
                    depth = depth,
                    boundType = boundType,
                    bestMove = bestMove,
                    score = score
                };
        }
    }
}