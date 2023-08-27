//#define DEBUG

using ChessChallenge.API;
using System;
using System.Linq;

public class CellBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int bestScore = -9999999;
        Move bestMove = default;

        foreach (Move move in board.GetLegalMoves())
        {
            board.MakeMove(move);
            int score = Evaluate();
            board.UndoMove(move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }


#if DEBUG
        Console.WriteLine("Eval: {0}",
            bestScore);
#endif

        return bestMove;

        int Evaluate()
        {
            // Generate out array of cells
            bool[,] cells = new bool[64, 12];

            for (int i = 0; i < 12; i++)
            {
                // Get bitboard for piece type
                ulong pieces = board.GetPieceBitboard((PieceType)(i / 2 % 5) + 1, i % 2 == 0);
                while (pieces != 0)
                {
                    int cellIndex = BitboardHelper.ClearAndGetIndexOfLSB(ref pieces);
                    cells[cellIndex, i] = true;
                }
            }

            // Create an order of updates based on current turn
            // WPawn, BPawn, WKnight, BKnight, ...
            // If white: 11, 10, 9, 8, 7, 6, 5, ...
            // Else:    10, 11, 8, 9, 6, 7, 4, 5 ...
            bool whiteToMove = board.IsWhiteToMove;
            int[] order = Enumerable.Range(0, 11).Reverse().ToArray();
            if (!whiteToMove)
                for (int i = 0; i < 10; i += 2)
                {
                    // Swap even and odd indices
                    int temp = order[i + 1];
                    order[i] = order[i + 1];
                    order[i + 1] = temp;
                }


            ulong blockers = board.AllPiecesBitboard;

            // TODO: End iterations if no king squares where updated, meaning no king activity

            // Run iterations
            const int Generations = 2;
            for (int generation = 0; generation < Generations; generation++)
                foreach (int type in order)
                {
                    for (int index = 0; index < 64; index++)
                    {
                        // Determine whether our cell is active
                        bool cell = cells[index, type];
                        if (cell)
                        {
                            ulong attacks = GetAttacksForCell(index, type);
                            while (attacks != 0)
                            {
                                int attackIndex = BitboardHelper.ClearAndGetIndexOfLSB(ref attacks);

                                bool canSet = true;
                                for (int i = 0; i < 12; i++)
                                    if (cells[attackIndex, i])
                                    {
                                        canSet = false;
                                        if (type % 2 != i % 2)
                                        {
                                            // Clear whatever was there before
                                            cells[attackIndex, i] = false;
                                            canSet = true;
                                        }
                                    }
                                cells[attackIndex, type] = canSet;
                            }
                        }
                    }
                }


            // Count population for each side
            int wScore = 0, bScore = 0;
            for (int index = 0; index < 64; index++)
            {
                for (int type = 0; type < 12; type++)
                {
                    if (index % 2 == 0)
                        wScore += cells[index, type] ? 1 : 0;
                    else
                        bScore += cells[index, type] ? 1 : 0;
                }
            }

            return (wScore - bScore) * (whiteToMove ? 1 : -1);

            ulong GetAttacksForCell(int index, int type)
                => BitboardHelper.GetPieceAttacks(
                    (PieceType)type + 1, new Square(index), blockers, type % 2 == 0);
        }
    }
}
