using System;

public abstract class DecimalBitsPieceTableGenerator : DecimalPieceTableGenerator
{
    protected override byte[] PackSquares(int[][] tablesToPack, ReadOnlySpan<short> baseValues, int square)
    {
        byte[] packedSquares = new byte[tableCount];

        for (int set = 0; set < tableCount; set++)
        {
            packedSquares[set] = PackSquare(tablesToPack, baseValues, square, set);
        }

        return packedSquares;
    }

    protected override int[] UnpackSquares(decimal packedTable, ReadOnlySpan<short> baseValues)
    {
        int[] unpackedSquares = new int[tableCount];
        int[] bits = decimal.GetBits(packedTable);

        for (int set = 0; set < tableCount; set++)
        {
            unpackedSquares[set] = UnpackSquare(bits[set >> 2] >> (set % 4 << 3), baseValues, set);
        }

        return unpackedSquares;
    }

    protected abstract byte PackSquare(int[][] tablesToPack, ReadOnlySpan<short> baseValues, int square, int set);

    protected abstract int UnpackSquare(int valueToUnpack, ReadOnlySpan<short> baseValues, int set);
}
