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
75, 84, 71, 102, 89, 76, 5, -25,
-6, 3, 39, 41, 49, 80, 44, 6,
-19, -3, 3, 6, 27, 29, 20, -3,
-32, -13, -13, 3, 5, 8, 6, -22,
-32, -17, -14, -16, 1, 2, 23, -10,
-34, -16, -22, -30, -10, 19, 32, -16,
0, 0, 0, 0, 0, 0, 0, 0,
 };

    private static int[] eg_pawn_table = {
0, 0, 0, 0, 0, 0, 0, 0,
181, 175, 170, 123, 116, 130, 177, 187,
114, 122, 91, 70, 59, 47, 93, 88,
42, 35, 17, 8, -2, 2, 19, 16,
16, 17, 1, -5, -7, -4, 6, -2,
10, 15, 2, 11, 3, 2, 3, -8,
15, 19, 12, 13, 14, 5, 2, -7,
0, 0, 0, 0, 0, 0, 0, 0,
    };

    private static int[] mg_knight_table = {
-161, -105, -47, -24, 25, -62, -99, -102,
-32, -3, 42, 41, 41, 86, 5, 18,
-3, 34, 51, 67, 102, 107, 60, 28,
0, 15, 36, 59, 39, 66, 21, 35,
-12, 2, 18, 20, 29, 25, 23, -4,
-30, -7, 8, 10, 21, 11, 16, -15,
-41, -30, -13, -2, -1, 2, -11, -13,
-81, -31, -45, -30, -27, -12, -29, -58,
    };

    private static int[] eg_knight_table = {
-68, -29, -13, -19, -22, -34, -23, -100,
-30, -11, -10, -4, -17, -29, -20, -49,
-17, -3, 15, 14, -4, -7, -15, -28,
-6, 15, 25, 28, 29, 21, 14, -16,
-4, 5, 28, 28, 31, 20, 5, -11,
-18, 0, 7, 22, 21, 6, -6, -18,
-28, -13, -2, 2, 0, -5, -21, -19,
-36, -46, -17, -13, -12, -22, -41, -40,
    };

    private static int[] mg_bishop_table = {
-11, -36, -26, -67, -70, -41, -17, -50,
-14, 21, 7, -1, 24, 27, 14, 14,
2, 28, 39, 49, 44, 68, 50, 35,
-7, 10, 31, 43, 39, 36, 14, 1,
-7, 7, 13, 34, 32, 14, 9, 0,
4, 13, 13, 16, 17, 13, 13, 18,
7, 11, 20, -1, 6, 19, 28, 11,
-14, 8, -9, -17, -16, -17, 9, -4,
    };

    private static int[] eg_bishop_table = {
-6, 3, 0, 14, 12, 2, -3, -4,
-16, -1, 5, 6, -1, -2, 5, -23,
9, 5, 12, 6, 8, 11, 4, 2,
6, 22, 16, 30, 21, 19, 15, 2,
1, 18, 27, 22, 23, 21, 13, -9,
0, 10, 18, 20, 23, 18, 2, -10,
-5, -6, -6, 8, 11, -1, -1, -23,
-22, -4, -22, -2, -5, -4, -19, -31,
    };

    private static int[] mg_rook_table = {
16, 12, 19, 23, 42, 65, 51, 58,
-4, -4, 16, 35, 20, 51, 48, 70,
-22, 0, -1, 3, 31, 37, 77, 51,
-33, -21, -19, -11, -10, 0, 14, 14,
-44, -47, -40, -29, -27, -35, -5, -15,
-45, -43, -38, -36, -29, -25, 13, -6,
-45, -40, -33, -33, -27, -18, -4, -32,
-24, -28, -26, -19, -13, -12, -9, -18,
    };

    private static int[] eg_rook_table = {
31, 33, 38, 33, 26, 18, 20, 21,
31, 40, 39, 28, 30, 22, 18, 8,
31, 30, 29, 25, 14, 12, 6, 3,
31, 26, 33, 27, 17, 14, 11, 7,
23, 26, 26, 24, 20, 21, 8, 4,
15, 17, 15, 17, 12, 8, -12, -11,
11, 14, 17, 16, 9, 5, -4, 2,
16, 17, 24, 19, 11, 13, 8, 0,
    };

    private static int[] mg_queen_table = {
-34, -28, 3, 37, 41, 42, 54, -4,
-12, -21, -18, -27, -23, 20, 4, 54,
-4, -5, 3, 16, 24, 61, 67, 62,
-18, -11, -5, -7, -4, 9, 9, 17,
-14, -11, -11, -7, -3, -4, 8, 7,
-14, -3, -9, -7, -6, 1, 13, 5,
-16, -6, 3, 3, 2, 12, 17, 20,
-16, -24, -16, -1, -12, -24, -6, -10,
    };

    private static int[] eg_queen_table = {
37, 54, 67, 55, 49, 45, 3, 44,
12, 42, 80, 103, 117, 77, 59, 20,
11, 33, 65, 70, 88, 64, 26, 13,
21, 47, 57, 83, 97, 83, 68, 43,
24, 46, 55, 80, 73, 63, 45, 34,
8, 20, 49, 42, 48, 41, 22, 7,
9, 10, 4, 14, 17, -10, -36, -53,
6, 7, 10, -5, 9, 4, -20, -23,
    };

    private static int[] mg_king_table = {
46, 14, 48, -51, -4, 20, 47, 104,
-63, -6, -45, 23, -8, 12, 0, -25,
-77, 29, -49, -56, -40, 30, 15, -37,
-56, -67, -75, -121, -117, -85, -78, -109,
-50, -61, -92, -128, -127, -97, -95, -125,
-21, -4, -65, -82, -77, -72, -28, -46,
62, 20, 2, -39, -40, -16, 30, 40,
49, 76, 48, -54, 10, -28, 54, 55,
    };

    private static int[] eg_king_table = {
-94, -43, -37, -2, -12, -6, -12, -84, 
-10, 15, 24, 13, 27, 38, 38, 11, 
3, 20, 38, 44, 47, 43, 44, 20, 
-7, 26, 42, 55, 55, 52, 44, 22, 
-18, 14, 37, 53, 54, 43, 31, 15, 
-24, 1, 22, 35, 35, 28, 10, -4, 
-42, -14, 1, 12, 16, 6, -13, -30, 
-72, -56, -38, -16, -41, -18, -47, -73, 
    };

    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private static readonly short[] PieceValues = { 85, 303, 311, 417, 884, 0,
                                                    112, 331, 337, 580, 1111, 0, };

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