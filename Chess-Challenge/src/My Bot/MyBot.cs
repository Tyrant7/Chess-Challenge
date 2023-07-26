using Chess_Challenge.src.My_Bot;
using ChessChallenge.API;
using System;
using System.Linq;

// TODO: Passed pawn evaluation
// TODO: Full piece square tables
// TODO: Null move pruning
// TODO: King safety
// TODO: Check extensions

public class MyBot : IChessBot
{
    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private readonly int[] PieceValues = { 0, 100, 320, 320, 500, 900, 0 };

    private int searchMaxTime;
    private Timer searchTimer;
    private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime;

    Board currentBoard;

    //
    // Search
    //

    public Move Think(Board board, Timer timer)
    {
        // Cache the board to save precious tokens
        currentBoard = board;

        // 1/16th of our remaining time, split among all of the moves
        searchMaxTime = timer.MillisecondsRemaining / 16;
        searchTimer = timer;

        // Progressively increase search depth, starting from 2
        for (int depth = 2; ; depth++)
        {
            Console.WriteLine("hit depth: " + depth + " in " + searchTimer.MillisecondsElapsedThisTurn + "ms");

            PVS(depth, -9999999, 9999999);

            if (OutOfTime)
                return TTRetrieve().BestMove;
        }
    }

    private int PVS(int depth, int alpha, int beta)
    {
        // Evaluate the gamestate
        if (currentBoard.IsDraw())
            // Discourage draws slightly, unless losing
            return -15;
        if (currentBoard.IsInCheckmate())
            // Checkmate = 99999
            // SwiftCheckmateBonus = 5000
            return -(99999 + (depth * 5000));

        // Terminal node, calculate score
        if (depth <= 0)
            // Do a Quiescence Search with a depth of 3, which will return a score from white's perspective
            // Multiple that score by -1 for black
            return QuiescenceSearch(2, alpha, beta);

        // Transposition table lookup
        TTEntry entry = TTRetrieve();

        // No entry for this position
        if (entry.Hash != currentBoard.ZobristKey)
        {
            // Internal iterative deepening
            PVS(depth - 2, alpha, beta);

            // Retrieve the new best move found with internal iterative deepening
            entry = TTRetrieve();
        }
        // Found a valid entry for this position
        else if (entry.Depth >= depth)
        {
            switch (entry.Flag)
            {
                // Exact
                case 0:
                    return entry.Score;
                // Lowerbound
                case -1:
                    alpha = Math.Max(alpha, entry.Score);
                    break;
                // Default case for upperbound (1) to save a token
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
            currentBoard.MakeMove(move);
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

            currentBoard.UndoMove(move);

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

        if (bestEval <= alpha)
            TTInsert(bestMove, bestEval, depth, 1);
        else
            TTInsert(bestMove, bestEval, depth, 0);

        return alpha;
    }

    // Quiescence search with help from
    // https://stackoverflow.com/questions/48846642/is-there-something-wrong-with-my-quiescence-search
    private int QuiescenceSearch(int depth, int alpha, int beta)
    {
        // Evaluate the gamestate
        if (currentBoard.IsDraw())
            // Discourage draws slightly, unless losing
            return -15;
        if (currentBoard.IsInCheckmate())
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
        foreach (Move move in GetOrdererdMoves(Move.NullMove, !currentBoard.IsInCheck()))
        {
            currentBoard.MakeMove(move);
            int eval = -QuiescenceSearch(depth - 1, -beta, -alpha);
            currentBoard.UndoMove(move);

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
                return 10 * (int)victim + (5 - (int)attacker);
        }
    }

    // Scoring algorithm using MVVLVA
    // Taking into account the best move found from the previous search
    private Move[] GetOrdererdMoves(Move hashMove, bool onlyCaptures)
        => currentBoard.GetLegalMoves(onlyCaptures).OrderByDescending(move =>
        GetMVV_LVA(move.CapturePieceType, move.MovePieceType) +
        (move == hashMove ? 100 : 0)).ToArray();

    //
    // Evaluation
    //

    // Big table packed with data from premade piece square tables
    private readonly ulong[,] PackedEvaluationTables = {
        {0x31CDE1EBFFEBCE00, 0x31D7D7F5FFF5D800, 0x31E1D7F5FFF5E200, 0x31EBCDFAFFF5E200},
        {0x31E1E1F604F5D80A, 0x13EBD80009FFEC0A, 0x13F5D8000A000014, 0x13FFCE000A00001E},
        {0x31E1E1F5FAF5E232, 0x13F5D80000000032, 0x0013D80500050A32, 0x001DCE05000A0F32},
        {0x31E1E1FAFAF5E205, 0x13F5D80000050505, 0x001DD80500050F0A, 0xEC27CE05000A1419},
        {0x31E1EBFFFAF5E200, 0x13F5E20000000000, 0x001DE205000A0F00, 0xEC27D805000A1414},
        {0x31E1F5F5FAF5E205, 0x13F5EC05000A04FB, 0x0013EC05000A09F6, 0x001DEC05000A0F00},
        {0x31E213F5FAF5D805, 0x13E214000004EC0A, 0x140000050000000A, 0x14000000000004EC},
        {0x31CE13EBFFEBCE00, 0x31E21DF5FFF5D800, 0x31E209F5FFF5E200, 0x31E1FFFB04F5E200}
    };

    public int GetSquareBonus(PieceType type, bool isWhite, int file, int rank)
    {
        // Because arrays are only 4 squares wide, mirror across files
        if (file > 3)
            file = 7 - file;

        // Mirror vertically for white pieces, since piece arrays are flipped vertically
        if (isWhite)
            rank = 7 - rank;

        ulong bytemask = 0xFF;

        // First shift the mask to select the correct byte
        // Then bitwise-and it with the packed scores
        // Finally, un-shift the resulting data to properly convert back
        int unpackedData = (sbyte)((PackedEvaluationTables[rank, file] & (bytemask << (int)type)) >> (int)type);

        // Invert eval scores for black pieces
        return isWhite ? unpackedData : -unpackedData;
    }

    // => instead of return { }
    // because it saves one token
    private int Evaluate()
    {
        int score = 0;
        foreach (PieceList list in currentBoard.GetAllPieceLists())
        {
            // Material evaluation
            int multiplier = list.IsWhitePieceList ? 1 : -1;
            score += PieceValues[(int)list.TypeOfPieceInList] * list.Count * multiplier;

            // Placement evaluation
            foreach (Piece piece in list)
            {
                // Leave out multiplier since it's worked in already in the GetSquareBonus method
                score += GetSquareBonus(piece.PieceType, piece.IsWhite, piece.Square.File, piece.Square.Rank);
            }
        }
        return currentBoard.IsWhiteToMove ? score : -score;
    }

    //
    // Transposition table
    //

    // 340000 represents the rough number of entries it would take to fill 256mb
    // Very lowballed to make sure I don't go over
    private readonly TTEntry[] transpositionTable = new TTEntry[340000];

    private TTEntry TTRetrieve()
        => transpositionTable[currentBoard.ZobristKey % 340000];

    private void TTInsert(Move bestMove, int score, int depth, sbyte flag)
    {
        if (depth > 1)
            transpositionTable[currentBoard.ZobristKey % 340000] = new TTEntry(
                currentBoard.ZobristKey, 
                bestMove, 
                score, 
                depth, 
                flag);

    }

    // public enum Flag
    // {
    //     0 = Exact,
    //     1 = Lowerbound,
    //     2 = Upperbound
    // }
    public record struct TTEntry(ulong Hash, Move BestMove, int Score, int Depth, sbyte Flag)
    {
        public readonly bool IsValid => Depth > 0;
        public static TTEntry Invalid => new(0, Move.NullMove, 0, -1, 0);
    }
}
