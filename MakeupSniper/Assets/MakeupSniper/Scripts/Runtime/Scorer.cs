using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    public struct ZoneScore
    {
        public string name;
        public PaintColor color;
        public float coverage;   // 0..1, доля ячеек зоны нужного цвета
        public float required;   // порог, обычно 0.7
        public bool ok;          // покрытие >= порога
        public byte blame;       // чьего неправильного цвета в зоне больше всего (0 — ничьего)
    }

    public sealed class ScoreResult
    {
        public ZoneScore[] zones;
        public int overshootCells;     // цветные ячейки на лице вне зон
        public float penaltyPercent;   // штраф за залёт, 0..30
        public int matchPercent;       // итог 0..100
        /// <summary>Очки за закрытые зоны по владельцам (с учётом множителей дистанции).</summary>
        public readonly Dictionary<byte, float> zonePoints = new Dictionary<byte, float>();
        /// <summary>Ячейки залёта по владельцам.</summary>
        public readonly Dictionary<byte, int> overshootByOwner = new Dictionary<byte, int>();
    }

    /// <summary>Подсчёт совпадения и очков по зонам. Работает только с логической сеткой.</summary>
    public static class Scorer
    {
        public const float CoverOk = 0.7f;
        public const float PenaltyMax = 30f;
        public const float ZonePoints = 20f;
        public const int MultCap = 6;

        public static ScoreResult Score(FaceGrid grid, ReferenceData reference, float faceRadiusUv)
        {
            int n = FaceGrid.Size * FaceGrid.Size;
            var inAnyZone = new bool[n];
            var zones = new List<ZoneScore>();
            var result = new ScoreResult();
            float sum = 0f;
            var correctByOwner = new Dictionary<byte, int>();
            var multByOwner = new Dictionary<byte, int>();
            var wrongByOwner = new Dictionary<byte, int>();

            foreach (var z in reference.zones)
            {
                int total = 0, hit = 0;
                correctByOwner.Clear(); multByOwner.Clear(); wrongByOwner.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (!z.Contains(FaceGrid.CellCenter(i))) continue;
                    inAnyZone[i] = true;
                    total++;
                    PaintColor c = grid.Colors[i];
                    byte o = grid.Owners[i];
                    if (c == z.color)
                    {
                        hit++;
                        Inc(correctByOwner, o, 1);
                        Inc(multByOwner, o, Mathf.Clamp(grid.Mults[i], 1, MultCap));
                    }
                    else if (c != PaintColor.None)
                    {
                        Inc(wrongByOwner, o, 1);
                    }
                }
                float cov = total > 0 ? (float)hit / total : 0f;
                float need = z.required > 0f ? z.required : CoverOk;
                bool ok = cov >= need;
                // покрытие сверх порога не даёт больше 100% за зону
                sum += Mathf.Min(1f, cov / need);

                byte blame = 0; int worst = 0;
                foreach (var kv in wrongByOwner)
                    if (kv.Value > worst) { worst = kv.Value; blame = kv.Key; }

                if (ok && hit > 0)
                {
                    foreach (var kv in correctByOwner)
                    {
                        if (kv.Key == 0 || kv.Key == FaceGrid.ModelOwner) continue;
                        float share = (float)kv.Value / hit;
                        float avgMult = (float)multByOwner[kv.Key] / kv.Value;
                        float pts;
                        result.zonePoints.TryGetValue(kv.Key, out pts);
                        result.zonePoints[kv.Key] = pts + ZonePoints * share * avgMult;
                    }
                }

                zones.Add(new ZoneScore { name = z.name, color = z.color, coverage = cov, required = need, ok = ok, blame = blame });
            }

            int faceCells = 0, over = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = FaceGrid.CellCenter(i);
                float dx = c.x - 0.5f, dy = c.y - 0.5f;
                if (dx * dx + dy * dy > faceRadiusUv * faceRadiusUv) continue;
                faceCells++;
                if (grid.Colors[i] != PaintColor.None && !inAnyZone[i])
                {
                    over++;
                    int prev;
                    result.overshootByOwner.TryGetValue(grid.Owners[i], out prev);
                    result.overshootByOwner[grid.Owners[i]] = prev + 1;
                }
            }

            float penalty = faceCells > 0 ? Mathf.Min(PenaltyMax, 300f * over / faceCells) : 0f;
            float avg = zones.Count > 0 ? sum / zones.Count : 0f;
            result.zones = zones.ToArray();
            result.overshootCells = over;
            result.penaltyPercent = penalty;
            result.matchPercent = Mathf.Clamp(Mathf.RoundToInt(avg * 100f - penalty), 0, 100);
            return result;
        }

        static void Inc(Dictionary<byte, int> d, byte key, int by)
        {
            int v;
            d.TryGetValue(key, out v);
            d[key] = v + by;
        }

        /// <summary>Сколько ячеек зоны закрасил бы правильным цветом один штамп (для бонуса «одним выстрелом»).</summary>
        public static float ZoneShareOfStamp(ZoneTarget zone, FaceGrid before, FaceGrid after)
        {
            int n = FaceGrid.Size * FaceGrid.Size, total = 0, gained = 0;
            for (int i = 0; i < n; i++)
            {
                if (!zone.Contains(FaceGrid.CellCenter(i))) continue;
                total++;
                if (after.Colors[i] == zone.color && before.Colors[i] != zone.color) gained++;
            }
            return total > 0 ? (float)gained / total : 0f;
        }

        /// <summary>Звёзды за раунд: 40% — одна, 60% — две, 80% — три.</summary>
        public static int Stars(int matchPercent)
        {
            if (matchPercent >= 80) return 3;
            if (matchPercent >= 60) return 2;
            if (matchPercent >= 40) return 1;
            return 0;
        }
    }
}
