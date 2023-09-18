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
57, 77, 57, 83, 72, 66, 3, -36,
-23, -3, 28, 30, 38, 64, 39, -8,
-35, -8, -4, -1, 18, 13, 14, -16,
-48, -18, -18, -1, -1, -8, 0, -33,
-48, -21, -18, -20, -5, -12, 17, -21,
-49, -19, -26, -35, -16, 4, 26, -26,
0, 0, 0, 0, 0, 0, 0, 0,
 };

    private static int[] eg_pawn_table = {
0, 0, 0, 0, 0, 0, 0, 0, 
183, 177, 170, 127, 122, 134, 179, 191, 
115, 122, 93, 71, 61, 52, 94, 92, 
45, 37, 20, 9, 1, 7, 22, 21, 
21, 19, 4, -3, -4, 1, 9, 3, 
14, 17, 3, 12, 7, 6, 6, -3, 
20, 20, 15, 17, 19, 10, 5, -3, 
0, 0, 0, 0, 0, 0, 0, 0, 
    };

    private static int[] mg_knight_table = {
-163, -98, -41, -18, 35, -62, -85, -97,
-26, 0, 49, 43, 44, 93, 9, 21,
2, 41, 52, 70, 106, 112, 67, 33,
3, 16, 37, 60, 40, 68, 23, 36,
-10, 4, 19, 20, 29, 25, 23, -4,
-27, -6, 10, 11, 21, 12, 16, -16,
-39, -30, -12, -1, 0, 3, -11, -13,
-85, -28, -46, -30, -26, -15, -26, -57,
    };

    private static int[] eg_knight_table = {
-57, -23, -5, -15, -17, -25, -22, -93,
-23, -4, -5, 3, -9, -23, -13, -42,
-12, 2, 22, 21, 2, 0, -9, -23,
0, 21, 32, 35, 36, 27, 21, -9,
1, 11, 34, 35, 37, 27, 12, -4,
-13, 6, 13, 28, 27, 11, -1, -10,
-22, -8, 3, 8, 6, 0, -15, -15,
-28, -42, -11, -7, -7, -16, -38, -36,
    };

    private static int[] mg_bishop_table = {
-11, -26, -30, -61, -60, -36, -8, -44,
-4, 25, 10, 1, 29, 38, 20, 15,
7, 33, 42, 55, 47, 73, 54, 41,
-1, 12, 36, 48, 43, 40, 16, 3,
-3, 11, 16, 35, 35, 17, 12, 2,
10, 16, 15, 19, 19, 16, 15, 20,
11, 14, 22, 2, 8, 21, 30, 13,
-13, 8, -7, -15, -14, -14, 8, -6,
    };

    private static int[] eg_bishop_table = {
1, 8, 8, 20, 18, 9, 3, 2,
-8, 7, 13, 12, 7, 4, 12, -14,
16, 11, 19, 12, 15, 17, 10, 9,
13, 29, 24, 35, 28, 25, 22, 11,
8, 24, 33, 30, 29, 28, 19, -1,
6, 17, 25, 27, 30, 24, 9, -2,
2, 0, 2, 15, 18, 6, 6, -16,
-15, 3, -17, 6, 3, 1, -9, -19,
    };

    private static int[] mg_rook_table = {
23, 18, 24, 32, 49, 59, 40, 57,
2, -1, 22, 43, 29, 59, 46, 71,
-19, 3, 3, 8, 33, 41, 77, 49,
-37, -21, -16, -8, -6, 0, 10, 6,
-55, -50, -42, -29, -27, -40, -15, -29,
-63, -49, -41, -42, -36, -38, -4, -29,
-66, -49, -38, -39, -35, -32, -14, -56,
-45, -42, -32, -27, -23, -33, -24, -40,
    };

    private static int[] eg_rook_table = {
27, 32, 38, 35, 26, 14, 18, 15,
35, 38, 45, 41, 33, 23, 26, 23,
34, 45, 46, 38, 36, 23, 21, 10,
35, 36, 37, 34, 22, 16, 8, 6,
36, 33, 42, 36, 26, 20, 15, 12,
31, 34, 37, 34, 29, 27, 15, 12,
26, 26, 24, 28, 24, 17, 0, 0,
22, 24, 26, 27, 19, 16, 6, 16,
17, 27, 33, 32, 24, 19, 20, 3,
    };

    private static int[] mg_queen_table = {
-30, -24, 8, 37, 48, 53, 54, 2,
-2, -22, -16, -25, -20, 30, 9, 58,
-1, -2, 3, 16, 23, 64, 68, 64,
-17, -13, -6, -8, -5, 9, 7, 13,
-15, -14, -13, -10, -6, -6, 5, 4,
-15, -4, -11, -9, -8, -1, 11, 3,
-18, -8, 1, 1, 0, 9, 13, 18,
-16, -26, -18, -1, -14, -29, -13, -15,
    };

    private static int[] eg_queen_table = {
47, 68, 78, 67, 60, 53, 20, 58,
24, 60, 93, 115, 128, 87, 73, 34,
28, 47, 78, 86, 103, 77, 43, 29,
41, 65, 73, 98, 113, 98, 87, 65,
40, 64, 72, 98, 89, 80, 63, 51,
26, 34, 65, 58, 64, 56, 37, 25,
23, 25, 20, 30, 32, 6, -19, -36,
18, 21, 24, 3, 23, 20, -1, -9,
    };

    private static int[] mg_king_table = {
34, 27, 54, -33, -19, 6, 42, 89,
-42, -1, -39, 28, -7, 11, -10, -39,
-61, 31, -39, -50, -35, 41, 29, -32,
-53, -64, -68, -113, -109, -80, -69, -104,
-56, -57, -90, -127, -126, -96, -92, -119,
-23, -10, -66, -82, -79, -73, -30, -47,
54, 15, -4, -43, -42, -21, 28, 38,
43, 72, 45, -58, 10, -31, 52, 54,
    };

    private static int[] eg_king_table = {
-90, -45, -37, -6, -10, -1, -10, -71,
-12, 15, 22, 11, 25, 37, 37, 13,
3, 19, 36, 40, 42, 40, 41, 19,
-7, 26, 40, 52, 52, 50, 41, 21,
-17, 12, 37, 51, 52, 42, 29, 12,
-24, 1, 22, 34, 35, 28, 9, -4,
-42, -15, 1, 13, 16, 6, -13, -31,
-72, -56, -37, -15, -42, -18, -46, -73,
    };

    // None, Pawn, Knight, Bishop, Rook, Queen, King 
    private static readonly short[] PieceValues = { 85, 302, 308, 443, 891, 0, // Middlegame
                                                    106, 317, 322, 572, 1066,  0 }; // Endgame

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