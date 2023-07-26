using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Challenge.src.My_Bot
{
    internal class PieceTableGenerator
    {
        // Piece square tables taken from
        // https://www.chessprogramming.org/Simplified_Evaluation_Function

        private sbyte[,] pawnScores = {
            { 0, 0, 0, 0},
            { 50, 50, 50, 50},
            { 10, 10, 20, 30},
            { 5, 5, 10, 25},
            { 0, 0, 0, 20 },
            { 5, -5, -10, 0},
            { 5, 10, 10, -20},
            { 0, 0, 0, 0}
        };

        private sbyte[,] knightScores =
        {
            { -50, -40, -30, -30 },
            { -40, -20, 0, 0},
            { -30, 0, 10, 15},
            { -30, 5, 15, 20},
            { -30, 0, 15, 20},
            { -30, 5, 10, 15},
            { -40, -20, 0, 5},
            { -50, -40, -30, -30}
        };

        private sbyte[,] bishopScores =
        {
            { -20, -10, -10, -10},
            { -10, 0, 0, 0},
            { -10, 0, 5, 10},
            { -10, 5, 5, 10},
            { -10, 0, 10, 10},
            { -10, 10, 10, 10},
            { -10, 5, 0, 0},
            { -20, -10, -10, -10}
        };

        private sbyte[,] rookScores =
        {
            { 0, 0, 0, 0},
            { 5, 10, 10, 10},
            { -5, 0, 0, 0},
            { -5, 0, 0, 0},
            { -5, 0, 0, 0},
            { -5, 0, 0, 0},
            { -5, 0, 0, 0},
            { 0, 0, 0, 0 }
        };

        private sbyte[,] queenScores =
        {
            { -20, -10, -10, -5},
            { -10, 0, 0, 0},
            { -10, 0, 5, 5},
            { -5, 0, 5, 5},
            { 0, 0, 5, 5 },
            { -10, 5, 5, 5 },
            { -10, 0, 5, 0},
            { -20, -10, -10, -5}
        };

        private sbyte[,] kingMiddlegameScores =
        {
            { -30, -40, -40, -50},
            { -30, -40, -40, -50},
            { -30, -40, -40, -50},
            { -30, -40, -40, -50},
            { -20, -30, -30, -40},
            { -10, -20, -20, -20},
            { 20, 20, 0, 0},
            { 20, 30, 10, 0}
        };

        private sbyte[,] kingEndgameScores =
        {
            { -50, -40, -30, -20},
            { -30, -20, -10, 0},
            { -30, -10, 20, 30},
            { -30, -10, 30, 40},
            { -30, -10, 30, 40},
            { -30, -10, 20, 30},
            { -30, -30, 0, 0},
            { -50, -30, -30, -30}
        };

        private sbyte[,] kingHuntScores =
{
            { 0, 0, 0, 0},
            { 0, 0, 0, 0},
            { 0, 0, 0, 0},
            { 0, 0, 0, 0},
            { 0, 0, 0, 0},
            { 0, 0, 0, 0},
            { 0, 0, 0, 0},
            { 0, 0, 0, 0}
        };

        // Script provided by Selenaut on Discord
        // Use to print the packed array to the console, then clean up and paste directly into your code.
        public PieceTableGenerator()
        {
            // Add boards from "index" 0 upwards. Here, the pawn board is "index" 0.
            // That means it will occupy the least significant byte in the packed data.
            List<sbyte[,]> allScores = new List<sbyte[,]>
            {
                pawnScores,
                knightScores,
                bishopScores,
                rookScores,
                queenScores,
                kingMiddlegameScores,
                kingEndgameScores,
                kingHuntScores
            };

            long[,] packedData = new long[8, 4];

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 4; file++)
                {
                    for (int set = 0; set < 8; set++)
                    {
                        sbyte[,] thisSet = allScores[set];
                        packedData[rank, file] |= ((long)thisSet[rank, file]) << (8 * set);
                    }
                }
                Console.WriteLine("{{0x{0,16:X}, 0x{1,16:X}, 0x{2,16:X}, 0x{3,16:X}}},", packedData[rank, 0], packedData[rank, 1], packedData[rank, 2], packedData[rank, 3]);
            }
        }
    }
}
