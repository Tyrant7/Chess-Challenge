using System;
using System.Linq;

public class DecimalPieceTableGenerator : PieceTableGenerator<decimal>
{
    // Packs data in the following form
    // Square data in the first 12 bytes of each decimal (1 byte per piece type, 6 per gamephase)
    protected override decimal[] PackData(int[][] tablesToPack, short[] _)
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

        return packedData;
    }

    // Unpacks a packed square table to be accessed with
    // pestoUnpacked[square][pieceType]
    protected override int[][] UnpackData(decimal[] tablesToUnpack, short[] pieceValues)
    {
        return tablesToUnpack.Select(packedTable =>
        {
            int pieceType = 0;
            return new System.Numerics.BigInteger(packedTable).ToByteArray().Take(12)
                    .Select(square => (int)((sbyte)square * 1.461) + pieceValues[pieceType++])
                .ToArray();
        }).ToArray();
    }

    protected override void PrintPackedData(decimal[] packedData)
    {
        Console.Write("{ ");
        for (int square = 0; square < tableSize; square++)
        {
            if (square % 8 == 0)
                Console.WriteLine();
            Console.Write($"{packedData[square],29}m, ");
        }
        Console.WriteLine("\n};");
    }
}
