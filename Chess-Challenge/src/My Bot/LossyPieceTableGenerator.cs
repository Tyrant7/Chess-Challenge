using System;

public class LossyPieceTableGenerator : DecimalPieceTableGenerator
{
    protected override byte[] PackSquares(int[][] tablesToPack, int square)
    {
        byte[] packedSquares = new byte[tableCount];

        for (int set = 0; set < tableCount; set++)
        {
            int[] setToPack = tablesToPack[set];
            sbyte valueToPack = (sbyte)Math.Round(setToPack[square] / 1.461);
            packedSquares[set] = (byte)(valueToPack & 0xFF);
        }

        return packedSquares;
    }

    protected override int[] UnpackSquares(decimal packedTable, ReadOnlySpan<short> pieceValues)
    {
        int[] unpackedSquares = new int[tableCount];
        byte[] bytes = new System.Numerics.BigInteger(packedTable).ToByteArray();

        for (int set = 0; set < tableCount; set++)
        {
            unpackedSquares[set] = (int)((sbyte)bytes[set] * 1.461) + pieceValues[set];
        }

        return unpackedSquares;
    }
}
