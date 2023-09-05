using System;
using System.Diagnostics;
using System.Linq;

public class VbrPieceTableGenerator : DecimalPieceTableGenerator
{
    private const long shifts8 = 0x212819110800;
    private const long masks10 = 0b0001111111_0011111111_0011111111_0011111111_0111111111_0011111111;

    protected override ReadOnlySpan<short> GetBaseValues(int[][] table, ReadOnlySpan<short> pieceValues)
    {
        short[] baseValues = new short[12];
        for (int type = 0; type < 6; ++type)
        {
            SetBaseValue(table, baseValues, type);
        }
        return baseValues;
    }

    protected override byte[] PackSquares(int[][] table, ReadOnlySpan<short> baseValues, int square)
    {
        return GetBytes(table, baseValues, square, 0).Concat(GetBytes(table, baseValues, square, 6)).ToArray();
    }

    protected override int[] UnpackSquares(decimal packedTable, ReadOnlySpan<short> pieceValues)
    {
        int[] unpackedData = new int[12];
        var bytes = new byte[14];
        new System.Numerics.BigInteger(packedTable).TryWriteBytes(bytes, out _);
        long midgame = BitConverter.ToInt64(bytes);
        long endgame = BitConverter.ToInt64(bytes, 6);
        for (int type = 0; type < 6; ++type)
        {
            byte shift = (byte)(shifts8 >> type * 8);
            long mask = (masks10 >> type * 10) & 0b0111111111;
            unpackedData[type] = (int)(midgame >> shift & mask);
            unpackedData[type + 6] = (int)(endgame >> shift & mask);
        }
        return unpackedData;
    }

    private static byte[] GetBytes(int[][] tables, ReadOnlySpan<short> baseValues, int square, int offset)
    {
        long midgame = 0;
        for (int type = 0; type < 6; ++type)
        {
            midgame += GetBits(type, tables[type + offset][square] + baseValues[type + offset]);
        }
        return BitConverter.GetBytes(midgame)[..6];
    }

    private static long GetBits(int type, long value)
    {
        byte shift = (byte)(shifts8 >> type * 8);
        long mask = (masks10 >> type * 10) & 0b0111111111;
        Debug.Assert(value >= 0 && value <= mask);
        return (value & mask) << shift;
    }
}
