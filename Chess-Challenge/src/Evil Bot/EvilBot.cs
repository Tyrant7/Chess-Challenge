using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
    {
        Move globalbestmove = Move.NullMove;

        // https://www.chessprogramming.org/PeSTO%27s_Evaluation_Function
        int[] pieceVal = { 0, 100, 310, 330, 500, 1000, 10000 };
        int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };
        ulong[] psts = { 657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086, 364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588, 421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452, 162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453, 347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514, 329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460, 257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958, 384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824, 365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484, 329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047, 347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452, 384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716, 366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428, 329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844, 329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863, 419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224, 366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995, 365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612, 401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596, 67159620133902 };
        Move[,] killerMoves = new Move[200, 2];
        int[,] historyHeuristicScores = new int[64, 64];

        struct TTEntry
        {
            public ulong key;
            public Move move;
            public int depth, score, bound;
            public TTEntry(ulong _key, Move _move, int _depth, int _score, int _bound)
            {
                key = _key; move = _move; depth = _depth; score = _score; bound = _bound;
            }
        }

        const int entries = (1 << 20);
        TTEntry[] tt = new TTEntry[entries];

        public int getPstVal(int psq)
        {
            return (int)(((psts[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
        }

        public int Evaluate(Board board)
        {
            int mg = 0, eg = 0, phase = 0;

            foreach (bool stm in new[] { true, false })
            {
                for (var p = PieceType.Pawn; p <= PieceType.King; p++)
                {
                    int piece = (int)p, ind;
                    ulong mask = board.GetPieceBitboard(p, stm);
                    while (mask != 0)
                    {
                        phase += piecePhase[piece];
                        ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                        mg += getPstVal(ind) + pieceVal[piece];
                        eg += getPstVal(ind + 64) + pieceVal[piece];
                    }
                }

                mg = -mg;
                eg = -eg;
            }

            return (mg * phase + eg * (24 - phase)) / 24 * (board.IsWhiteToMove ? 1 : -1);
        }

        public int Search(Board board, Timer timer, int alpha, int beta, int depth, int ply)
        {
            bool qsearch = depth <= 0;
            int best = -30000;

            if (ply > 0 && board.IsRepeatedPosition()) return 0;

            ulong key = board.ZobristKey;
            TTEntry entry = tt[key % entries];

            if (ply > 0 && entry.key == key && entry.depth >= depth && (
                entry.bound == 3 || entry.bound == 2 && entry.score >= beta || entry.bound == 1 & entry.score <= alpha
            )) return entry.score;

            int eval = Evaluate(board);

            if (qsearch)
            {
                best = eval;
                if (best >= beta) return best;
                alpha = Math.Max(alpha, best);
            }

            // no more tokens :( gotta wait for Selenaut rng compression to work
            //if (depth > 3 && !board.IsInCheck())
            //{
            //    board.MakeMove(Move.NullMove);
            //    int nullscore = -Search(board, timer, -beta, -beta + 1, depth - 3, ply + 1);
            //    board.UndoMove(Move.NullMove);

            //    if (nullscore >= beta) return beta;
            //}

            var moves = board.GetLegalMoves(qsearch);
            var scores = new int[moves.Length];

            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];
                if (move == entry.move) scores[i] += 1000000;
                if (move.IsCapture) scores[i] += 100 * (int)move.CapturePieceType - (int)move.MovePieceType;
                if (move == globalbestmove) scores[i] += 100000;
                if (move.Equals(killerMoves[ply, 0])) scores[i] += 2000;
                if (move.Equals(killerMoves[ply, 1])) scores[i] += 1000;
                scores[i] += historyHeuristicScores[move.StartSquare.Index, move.TargetSquare.Index];
            }

            Move bestMove = Move.NullMove;
            int origAlpha = alpha;

            for (int i = 0; i < moves.Length; i++)
            {
                if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) return 30000;

                for (int j = i + 1; j < moves.Length; j++)
                {
                    if (scores[j] > scores[i])
                        (scores[i], scores[j], moves[i], moves[j]) = (scores[j], scores[i], moves[j], moves[i]);
                }

                Move move = moves[i];
                board.MakeMove(move);
                int score;
                if (i < 5) score = -Search(board, timer, -beta, -alpha, depth - 1, ply + 1);
                else
                {
                    score = -Search(board, timer, -beta, -alpha, depth - 2, ply + 1);
                    if (score > alpha && score < beta)
                    {
                        score = -Search(board, timer, -beta, -alpha, depth - 1, ply + 1);
                    }
                }

                if (!move.IsCapture) historyHeuristicScores[move.StartSquare.Index, move.TargetSquare.Index] += depth * depth;

                if (score >= beta) UpdateKillerMove(ply, move);

                board.UndoMove(move);

                if (score > best)
                {
                    best = score;
                    bestMove = move;
                    if (ply == 0) globalbestmove = move;
                    alpha = Math.Max(alpha, score);
                    if (alpha >= beta) break;

                }
            }

            if (!qsearch && moves.Length == 0) return board.IsInCheck() ? -30000 + ply : 0;

            int bound = best >= beta ? 2 : best > origAlpha ? 3 : 1;

            tt[key % entries] = new TTEntry(key, bestMove, depth, best, bound);

            return best;
        }

        public void UpdateKillerMove(int ply, Move move)
        {
            killerMoves[ply, 1] = killerMoves[ply, 0];
            killerMoves[ply, 0] = move;
        }

        public Move Think(Board board, Timer timer)
        {
            globalbestmove = Move.NullMove;
            for (int depth = 1; depth <= 100; depth++)
            {
                Search(board, timer, -30000, 30000, depth, 0);

                if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                    break;
            }
            return globalbestmove.IsNull ? board.GetLegalMoves()[0] : globalbestmove;
        }
    }
}