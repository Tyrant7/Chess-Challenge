using System;
using System.Linq;

public class LongPieceTableGenerator : PieceTableGenerator<long>
{
    protected override long[] PackData(int[][] table, ReadOnlySpan<short> _pieceValues)
    {
        short[] pieceValues = new short[12];
        _pieceValues.CopyTo(pieceValues);
        pieceValues[(int)ScoreType.KingMG] = pieceValues[(int)ScoreType.KingEG] =
            (short)-Math.Min(table[(int)ScoreType.KingMG].Min(), table[(int)ScoreType.KingEG].Min());
        long[] packedData = new long[128];
        for (int type = 0; type < 6; ++type)
        {
            for (int square = 0; square < 64; ++square)
            {
                packedData[square] |= (long)(pieceValues[type] + table[type][square]) << type * 11;
                packedData[square + 64] |= (long)(pieceValues[type + 6] + table[type + 6][square]) << type % 6 * 11;
            }
        }
        return packedData;
    }

    protected override int[][] UnpackData(long[] packedData, ReadOnlySpan<short> _)
    {
        int[][] unpackedData = new int[64][];
        for (int square = 0; square < 64; ++square)
            unpackedData[square] = new int[12];
        for (int type = 0; type < 6; ++type)
        {
            for (int square = 0; square < 64; ++square)
            {
                unpackedData[square][type] = (int)((packedData[square] >> type * 11) & 0b11111111111);
                unpackedData[square][type + 6] = (int)((packedData[square + 64] >> type % 6 * 11) & 0b11111111111);
            }
        }
        return unpackedData;
    }

    protected override void PrintPackedData(long[] packedData)
    {
        Console.WriteLine('{');
        for (int square = 0; square < 128; square++)
        {
            Console.Write($"0x{packedData[square]:X16},");
            Console.Write((square + 1) % 4 == 0 ? '\n' : ' ');
        }
        Console.WriteLine("};");
    }
}
