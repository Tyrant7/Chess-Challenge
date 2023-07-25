using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;

// TODO: MOST IMPORTANT: Killer moves
// TODO: MOST IMPORTANT: Null move pruning
// TODO: MOST IMPORTANT: Quiescence search
// TODO: King safety
// TODO: Check extensions
// TODO: Can probably optimize MVV_LVA with a simple mathematical function

public class MyBot : IChessBot
{
    TranspositionTable transpositionTable = new();

    // 2 is the number of killer moves
    // 12 as a placeholder max ply value
    Move[,] killerMoves = new Move[2, 12];

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

    private int initialSearchPly;

    private int searchMaxTime;
    private Timer searchTimer;
    private bool OutOfTime => searchTimer.MillisecondsElapsedThisTurn > searchMaxTime;

    public Move Think(Board board, Timer timer)
    {
        initialSearchPly = board.PlyCount;
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
    {
        if (move.CapturePieceType != PieceType.None)
            // MVV_LVA_Offset = int.MaxValue - 256
            // Or 2147483391
            return 2147483391 + MVV_LVA[(int)move.CapturePieceType - 1, (int)move.MovePieceType - 1];
        else
            for (int n = 0; n < 2; n++)
                if (move == killerMoves[n, board.PlyCount - initialSearchPly])
                    // If killer move matches at spot 0, we'll end up with
                    // MVV_LVA_Offset - 10, the second would be MVV_LVA_Offset - 20
                    // This will always place killer moves just below captures
                    return 2147483391 - ((n + 1) * 10);
        return 0;
    }

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
            {
                if (move.CapturePieceType == PieceType.None)
                    StoreKillerMove(move, board.PlyCount - initialSearchPly);
                break;
            }
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

    private void StoreKillerMove(Move move, int searchPly)
    {
        Move firstKiller = killerMoves[0, searchPly];

        // Don't store the same killer moves
        if (firstKiller != move)
        {
            // 2: Hardcoded max number of killer moves
            // Shift all moves one index upwards
            for (int i = 0; i < 2; i++)
            {
                Move previous = killerMoves[i - 1, searchPly];
                killerMoves[i, searchPly] = previous;
            }

            // Add the new killer move in the first spot
            killerMoves[0, searchPly] = move;
        }
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

public static class PieceSquareTable
{
    private static int[] DistFromCentre =
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
        int centreDist = DistFromCentre[square.Index] - (type == PieceType.Pawn ? 0 : 1);

        switch (type)
        {
            // Use some simple equations to determine generally good squares without using a table
            case PieceType.Pawn:
                // Pawn gets bonuses for being further forward
                // but also get a bonus for being close to the centre
                return rank * 5 + (centreDist == 1 ? 10 : 0) + (centreDist == 0 ? 15 : 0);
            case PieceType.Knight:
                // Get a bonus for being in the centre, and a penalty for being further away
                return -centreDist * 15;
            case PieceType.Bishop:
                // Same here, but less
                return -centreDist * 10;
            case PieceType.Rook:
                // Bonus for sitting on second or seventh rank, depending on side
                return (square.Rank == (isWhite ? 6 : 1)) ? 10 : 0;
            case PieceType.Queen:
                // Bonus for being in centre, just like knights, but less
                return -centreDist * 5;
            case PieceType.King:
                // King gets a base +10 bonus for being on back rank, then -10 for every step forward
                return (-rank * 10) + 10;
        }
        return 0;
    }
}

//
// Transposition table
//

public class TranspositionTable
{
    Dictionary<ulong, PositionInfo> table = new();
    Queue<ulong> addedPositions = new();

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
    public bool IsValid => depthChecked > 0;
    public static PositionInfo Invalid => new PositionInfo(int.MinValue, -1, Flag.Exact);
}
