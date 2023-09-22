using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;

public class PieceTableGenerator
{
    public enum ScoreType
    {
        PawnMG, KnightMG, BishopMG, RookMG, QueenMG, KingMG,
        PawnEG, KnightEG, BishopEG, RookEG, QueenEG, KingEG
    }

    private static int[] mg_pawn_table =  {
0, 0, 0, 0, 0, 0, 0, 0,
66, 83, 66, 91, 83, 72, 10, -26,
-14, 5, 38, 40, 46, 74, 48, 3,
-28, -1, 4, 6, 27, 22, 22, -6,
-41, -10, -10, 6, 6, 0, 7, -25,
-41, -14, -11, -12, 3, -6, 25, -13,
-42, -12, -18, -27, -7, 11, 34, -18,
0, 0, 0, 0, 0, 0, 0, 0,
 };

    private static int[] eg_pawn_table = {
0, 0, 0, 0, 0, 0, 0, 0,
178, 172, 167, 122, 115, 129, 176, 185,
114, 121, 90, 68, 58, 48, 91, 89,
43, 34, 16, 5, -3, 3, 19, 17,
18, 16, 0, -7, -8, -3, 5, -1,
11, 14, 0, 8, 3, 3, 3, -7,
16, 17, 11, 11, 14, 7, 2, -7,
0, 0, 0, 0, 0, 0, 0, 0,
    };

    private static int[] mg_knight_table = {
-164, -104, -43, -19, 28, -60, -96, -98,
-25, 0, 45, 44, 45, 95, 7, 22,
2, 37, 52, 69, 106, 110, 65, 30,
2, 14, 37, 60, 39, 66, 21, 35,
-13, 2, 17, 18, 27, 24, 22, -6,
-31, -9, 7, 9, 20, 10, 14, -17,
-43, -33, -15, -4, -3, 0, -13, -16,
-87, -32, -49, -33, -29, -17, -31, -62,
    };

    private static int[] eg_knight_table = {
-68, -29, -13, -19, -22, -34, -24, -103,
-31, -11, -10, -4, -17, -29, -20, -49,
-17, -3, 16, 15, -4, -7, -16, -27,
-5, 16, 26, 29, 30, 22, 15, -15,
-4, 6, 29, 29, 32, 21, 5, -10,
-18, 0, 7, 23, 22, 6, -6, -18,
-28, -13, -3, 1, 0, -5, -22, -20,
-36, -47, -17, -13, -13, -22, -42, -41,
    };

    private static int[] mg_bishop_table = {
-13, -34, -24, -65, -67, -40, -13, -51,
-5, 22, 9, 0, 26, 34, 16, 17,
6, 30, 39, 52, 45, 71, 52, 39,
-4, 9, 34, 44, 40, 36, 13, 0,
-8, 7, 13, 33, 31, 13, 8, -2,
6, 12, 11, 15, 15, 12, 11, 17,
7, 10, 18, -2, 5, 17, 26, 10,
-17, 5, -11, -19, -18, -19, 7, -8,
    };

    private static int[] eg_bishop_table = {
-5, 3, 0, 14, 12, 2, -3, -4,
-17, -1, 5, 6, -1, -3, 5, -23,
9, 5, 13, 6, 9, 11, 3, 1,
6, 23, 16, 30, 22, 19, 16, 3,
1, 18, 27, 22, 23, 22, 13, -8,
0, 11, 18, 20, 23, 18, 2, -10,
-4, -6, -5, 8, 11, -1, 0, -24,
-22, -4, -24, -2, -5, -5, -19, -30,
    };

    private static int[] mg_rook_table = {
17, 13, 23, 28, 46, 69, 48, 58, 
-3, -6, 21, 43, 25, 59, 49, 73, 
-18, 7, 6, 12, 37, 49, 87, 58, 
-31, -13, -9, -3, 1, 10, 22, 18, 
-45, -41, -34, -21, -20, -30, -4, -17, 
-52, -40, -32, -32, -26, -27, 9, -15, 
-55, -40, -28, -29, -25, -21, -2, -38, 
-35, -32, -23, -18, -13, -23, -9, -29, 
    };

    private static int[] eg_rook_table = {
31, 34, 39, 35, 28, 19, 22, 20,
30, 41, 40, 30, 31, 20, 17, 5,
30, 29, 30, 25, 15, 11, 3, 0,
30, 26, 33, 28, 17, 14, 10, 5,
23, 26, 28, 25, 22, 22, 9, 4,
18, 18, 16, 19, 16, 11, -9, -8,
14, 17, 18, 18, 11, 9, -1, 5,
14, 21, 26, 23, 16, 16, 12, -1,
    };

    private static int[] mg_queen_table = {
-30, -24, 8, 43, 45, 53, 53, -1,
0, -20, -15, -26, -20, 30, 7, 60,
0, -2, 3, 18, 24, 65, 70, 65,
-15, -11, -5, -7, -4, 9, 8, 16,
-15, -13, -14, -9, -6, -6, 5, 5,
-15, -5, -12, -10, -8, -1, 12, 4,
-17, -9, 0, 1, 0, 9, 15, 21,
-17, -27, -19, -3, -14, -29, -11, -13,
    };

    private static int[] eg_queen_table = {
34, 51, 64, 52, 47, 40, 4, 42,
8, 43, 79, 102, 115, 75, 59, 19,
13, 33, 67, 70, 90, 63, 27, 13,
24, 49, 59, 85, 99, 84, 70, 45,
27, 48, 58, 83, 75, 65, 47, 35,
8, 21, 51, 44, 50, 41, 21, 7,
9, 11, 6, 15, 18, -11, -37, -58,
5, 9, 10, -7, 8, 5, -19, -23,
    };

    private static int[] mg_king_table = {
16, 16, 43, -41, -9, 13, 41, 61,
-56, -6, -45, 26, -4, 13, 1, -23,
-73, 31, -50, -54, -37, 35, 19, -32,
-55, -68, -75, -119, -116, -84, -75, -106,
-51, -60, -92, -127, -126, -98, -94, -124,
-22, -6, -66, -81, -78, -72, -28, -46,
59, 18, 1, -37, -38, -17, 32, 43,
50, 77, 50, -54, 13, -27, 57, 59,
    };

    private static int[] eg_king_table = {
-87, -43, -36, -2, -11, -4, -10, -77,
-11, 16, 24, 13, 27, 38, 38, 10,
3, 20, 39, 45, 47, 43, 44, 19,
-7, 27, 43, 55, 56, 52, 43, 22,
-18, 14, 38, 53, 54, 43, 30, 15,
-24, -1, 22, 35, 35, 28, 9, -4,
-42, -15, -1, 12, 15, 6, -14, -32,
-73, -57, -38, -17, -43, -19, -48, -75,
    };

    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private static readonly short[] PieceValues ={
78, 306, 314, 434, 893, 0,
113, 334, 340, 592, 1120, 0, };

    public static void Generate()
    {
        /*
        Important note!
        Due to certain specifics which I don't fully understand, 
        king PST values (or any PST values where the piece value is also zero, for that matter)
        will throw an exception when unpacking! 
        Please sanitize your inputted king square tables by changing all zeroes to either 1 or -1 to minimize error without raising any exceptions
        */

        List<int[]> table = new()
        {
            mg_pawn_table,
            mg_knight_table,
            mg_bishop_table,
            mg_rook_table,
            mg_queen_table,
            mg_king_table,

            eg_pawn_table,
            eg_knight_table,
            eg_bishop_table,
            eg_rook_table,
            eg_queen_table,
            eg_king_table
        };

        Console.WriteLine("Packed table:\n");
        decimal[] packedData = PackData(table);

        Console.Write(packedData);

        Console.WriteLine("Unpacked table:\n");
        int[][] unpackedData = UnpackData(packedData);

        PrintUnpackedData(unpackedData);
    }

    private const int tableSize = 64;
    private const int tableCount = 12;

    // Packs data in the following form
    // Square data in the first 12 bytes of each decimal (1 byte per piece type, 6 per gamephase)
    private static decimal[] PackData(List<int[]> tablesToPack)
    {
        decimal[] packedData = new decimal[tableSize];

        for (int square = 0; square < tableSize; square++)
        {
            // Pack all sets for this square into a byte array
            byte[] packedSquares = new byte[tableCount];
            for (int set = 0; set < tableCount; set++)
            {
                int[] setToPack = tablesToPack[set];
                sbyte valueToPack = (sbyte)Math.Round(setToPack[square] / 1.461);
                packedSquares[set] = (byte)(valueToPack & 0xFF);
            }

            // Create a new decimal based on the packed values for this square
            int[] thirds = new int[4];
            for (int i = 0; i < 3; i++)
            {
                thirds[i] = BitConverter.ToInt32(packedSquares, i * 4);
            }
            packedData[square] = new(thirds);
        }

        // Print the newly created table
        Console.Write("{ ");
        for (int square = 0; square < tableSize; square++)
        {
            if (square % 8 == 0)
                Console.WriteLine();
            Console.Write(packedData[square] + "m, ");
        }
        Console.WriteLine("\n};");

        return packedData;
    }

    // Unpacks a packed square table to be accessed with
    // pestoUnpacked[square][pieceType]
    private static int[][] UnpackData(decimal[] tablesToUnpack)
    {
        var pestoUnpacked = tablesToUnpack.Select(packedTable =>
        {
            int pieceType = 0;
            return new System.Numerics.BigInteger(packedTable).ToByteArray().Take(12)
                    .Select(square => (int)((sbyte)square * 1.461) + PieceValues[pieceType++])
                .ToArray();
        }).ToArray();

        return pestoUnpacked;
    }

    private static void PrintUnpackedData(int[][] unpackedData)
    {
        // Print all of the unpacked values
        for (int type = 0; type < tableCount; type++)
        {
            Console.WriteLine("\n\nTable for type: " + (ScoreType)type);
            for (int square = 0; square < tableSize; square++)
            {
                if (square % 8 == 0)
                    Console.WriteLine();

                Console.Write(unpackedData[square][type] + ", ");
            }
            Console.WriteLine();
        }
    }
}