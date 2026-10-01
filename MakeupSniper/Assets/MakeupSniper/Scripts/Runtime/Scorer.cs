using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    public struct ZoneScore
    {
        public string name;
        public PaintColor color;
        public float coverage;   // 0..1, доля ячеек зоны нужного цвета
        public bool ok;          // покрытие >= 70%
    }

    public sealed class ScoreResult
    {
        public ZoneScore[] zones;
        public int overshootCells;     // цветные ячейки на лице вне зон
        public float penaltyPercent;   // штраф за залёт, 0..30
        public int matchPercent;       // итог 0..100
    }

    /// <summary>Подсчёт совпадения по зонам. Работает только с логической сеткой.</summary>
    public static class Scorer
    {
        public const float CoverOk = 0.7f;
        public const float PenaltyMax = 30f;

        public static ScoreResult Score(FaceGrid grid, ReferenceData reference, float faceRadiusUv)
        {
            int n = FaceGrid.Size * FaceGrid.Size;
            var inAnyZone = new bool[n];
            var zones = new List<ZoneScore>();
            float sum = 0f;

            foreach (var z in reference.zones)
            {
                int total = 0, hit = 0;
                for (int i = 0; i < n; i++)
                {
                    if (!z.Contains(FaceGrid.CellCenter(i))) continue;
                    inAnyZone[i] = true;
                    total++;
                    if (grid.Colors[i] == z.color) hit++;
                }
                float cov = total > 0 ? (float)hit / total : 0f;
                sum += cov;
                zones.Add(new ZoneScore { name = z.name, color = z.color, coverage = cov, ok = cov >= CoverOk });
            }

            int faceCells = 0, over = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = FaceGrid.CellCenter(i);
                float dx = c.x - 0.5f, dy = c.y - 0.5f;
                if (dx * dx + dy * dy > faceRadiusUv * faceRadiusUv) continue;
                faceCells++;
                if (grid.Colors[i] != PaintColor.None && !inAnyZone[i]) over++;
            }

            float penalty = faceCells > 0 ? Mathf.Min(PenaltyMax, 300f * over / faceCells) : 0f;
            float avg = zones.Count > 0 ? sum / zones.Count : 0f;
            int match = Mathf.Clamp(Mathf.RoundToInt(avg * 100f - penalty), 0, 100);

            return new ScoreResult { zones = zones.ToArray(), overshootCells = over, penaltyPercent = penalty, matchPercent = match };
        }
    }
}
