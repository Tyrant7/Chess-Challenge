using System;

public class LossyPieceTableGenerator : DecimalBitsPieceTableGenerator
{
    protected override ReadOnlySpan<short> GetBaseValues(int[][] _, ReadOnlySpan<short> pieceValues)
    {
        return pieceValues;
    }

    protected override byte PackSquare(int[][] tablesToPack, ReadOnlySpan<short> _, int square, int set)
    {
        int[] setToPack = tablesToPack[set];
        sbyte valueToPack = (sbyte)Math.Round(setToPack[square] / 1.461);
        return (byte)valueToPack;
    }

    protected override int UnpackSquare(int valueToUnpack, ReadOnlySpan<short> pieceValues, int set)
    {
        return (int)((sbyte)valueToUnpack * 1.461 + pieceValues[set]);
    }
}
