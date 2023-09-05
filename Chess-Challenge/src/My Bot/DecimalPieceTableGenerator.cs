using System;

public abstract class DecimalPieceTableGenerator : PieceTableGenerator<decimal>
{
    protected abstract byte[] PackSquares(int[][] tablesToPack, ReadOnlySpan<short> baseValues, int square);

    protected abstract int[] UnpackSquares(decimal packedTable, ReadOnlySpan<short> pieceValues);

    protected override ReadOnlySpan<short> GetBaseValues(int[][] table, ReadOnlySpan<short> pieceValues)
    {
        short[] baseValues = new short[12];
        for (int type = 0; type < 6; ++type)
        {
            SetBaseValue(table, baseValues, type);
        }
        return baseValues;
    }

    // Packs data in the following form
    // Square data in the first 12 bytes of each decimal (1 byte per piece type, 6 per gamephase)
    protected override decimal[] PackData(int[][] tablesToPack, ReadOnlySpan<short> pieceValues)
    {
        decimal[] packedData = new decimal[tableSize];
        var baseValues = GetBaseValues(tablesToPack, pieceValues);

        for (int square = 0; square < tableSize; square++)
        {
            // Pack all sets for this square into a byte array
            byte[] packedSquares = PackSquares(tablesToPack, baseValues, square);

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
    protected override int[][] UnpackData(decimal[] tablesToUnpack, ReadOnlySpan<short> pieceValues)
    {
        int[][] unpackedData = new int[tableSize][];

        for (int square = 0; square < tableSize; square++)
        {
            unpackedData[square] = UnpackSquares(tablesToUnpack[square], pieceValues);
        }

        return unpackedData;
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
