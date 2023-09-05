using System;

public class PseudoPieceTableGenerator : DecimalBitsPieceTableGenerator
{
    protected override decimal[] PackData(int[][] table, ReadOnlySpan<short> pieceValues)
    {
        //Adjust KnightMG on a8 so it fits within a byte
        table[1][0] = -126;

        return base.PackData(table, pieceValues);
    }

    protected override byte PackSquare(int[][] table, ReadOnlySpan<short> baseValues, int square, int type)
    {
        return (byte)(table[type][square] + baseValues[type]);
    }

    protected override int UnpackSquare(int bit, ReadOnlySpan<short> baseValues, int type)
    {
        return (byte)bit - baseValues[type];
    }

    protected override void PrintBaseValues(ReadOnlySpan<short> baseValues)
    {
        long value = 0;
        for (int type = 0; type < 6; ++type)
        {
            value |= (long)baseValues[type] << type * 10;
        }
        Console.WriteLine($"0x{value:X16}");
    }
}
