using NUnit.Framework;
using UnityEngine;

namespace MakeupSniper.Tests
{
    /// <summary>Проверки «чистой» логики: сетка 64×64 и подсчёт совпадения.</summary>
    public class ScorerTests
    {
        const float FaceRadius = 0.3f / 0.7f;

        static ReferenceData Clown()
        {
            var r = ScriptableObject.CreateInstance<ReferenceData>();
            r.displayName = "Клоун";
            r.zones = new[]
            {
                new ZoneTarget { name = "Нос", center = new Vector2(0.5f, 0.47f), radius = new Vector2(0.055f, 0.05f), color = PaintColor.Red },
                new ZoneTarget { name = "Левая щека", center = new Vector2(0.69f, 0.43f), radius = new Vector2(0.08f, 0.07f), color = PaintColor.Pink }
            };
            return r;
        }

        static void FillZone(FaceGrid grid, ZoneTarget z, PaintColor color)
        {
            for (int i = 0; i < FaceGrid.Size * FaceGrid.Size; i++)
                if (z.Contains(FaceGrid.CellCenter(i))) grid.Colors[i] = color;
        }

        [Test]
        public void EmptyFace_ScoresZero()
        {
            var s = Scorer.Score(new FaceGrid(), Clown(), FaceRadius);
            Assert.AreEqual(0, s.matchPercent);
            Assert.AreEqual(0, s.overshootCells);
            foreach (var z in s.zones) Assert.IsFalse(z.ok);
        }

        [Test]
        public void PerfectPaint_ScoresHundred()
        {
            var r = Clown(); var g = new FaceGrid();
            foreach (var z in r.zones) FillZone(g, z, z.color);
            var s = Scorer.Score(g, r, FaceRadius);
            Assert.AreEqual(100, s.matchPercent);
            foreach (var z in s.zones) Assert.IsTrue(z.ok);
        }

        [Test]
        public void WrongColor_DoesNotCount()
        {
            var r = Clown(); var g = new FaceGrid();
            foreach (var z in r.zones) FillZone(g, z, PaintColor.Black);
            var s = Scorer.Score(g, r, FaceRadius);
            Assert.AreEqual(0, s.matchPercent);
        }

        [Test]
        public void Overshoot_IsPenalisedButCapped()
        {
            var r = Clown(); var g = new FaceGrid();
            for (int i = 0; i < g.Colors.Length; i++) g.Colors[i] = PaintColor.Red; // залили всё лицо красным
            var s = Scorer.Score(g, r, FaceRadius);
            Assert.AreEqual(Scorer.PenaltyMax, s.penaltyPercent, 0.001f);
            Assert.AreEqual(20, s.matchPercent); // нос 100%, щека 0% → 50% минус 30%
        }

        [Test]
        public void SeventyPercentCoverage_IsTheThreshold()
        {
            var r = Clown(); var g = new FaceGrid();
            var nose = r.zones[0];
            int total = 0;
            for (int i = 0; i < g.Colors.Length; i++) if (nose.Contains(FaceGrid.CellCenter(i))) total++;
            int need = Mathf.CeilToInt(total * 0.7f), done = 0;
            for (int i = 0; i < g.Colors.Length && done < need; i++)
                if (nose.Contains(FaceGrid.CellCenter(i))) { g.Colors[i] = PaintColor.Red; done++; }
            Assert.IsTrue(Scorer.Score(g, r, FaceRadius).zones[0].ok);
            // снимаем одну ячейку — зона больше не засчитана
            for (int i = 0; i < g.Colors.Length; i++) if (g.Colors[i] == PaintColor.Red) { g.Colors[i] = PaintColor.None; break; }
            Assert.IsFalse(Scorer.Score(g, r, FaceRadius).zones[0].ok);
        }

        [Test]
        public void StampDisc_PaintsRoundSpotWithOwner()
        {
            var g = new FaceGrid();
            int touched = g.StampDisc(new Vector2(0.5f, 0.5f), 0.05f, PaintColor.Pink, 3);
            Assert.Greater(touched, 20);  // диск радиусом 3,2 ячейки ≈ 32 ячейки
            Assert.Less(touched, 45);
            Assert.AreEqual(touched, g.PaintedCount);
            int centre = 32 * FaceGrid.Size + 32;
            Assert.AreEqual(PaintColor.Pink, g.Colors[centre]);
            Assert.AreEqual(3, g.Owners[centre]);
            Assert.AreEqual(PaintColor.None, g.Colors[0]);
        }

        [Test]
        public void HeadMesh_FrontUvIsFrontalProjection()
        {
            var mesh = HeadMeshFactory.CreateVisual(0.3f, 0.7f);
            var v = mesh.vertices; var uv = mesh.uv;
            int front = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i].z <= 0.001f) continue;
                front++;
                Assert.AreEqual(-v[i].x / 0.7f + 0.5f, uv[i].x, 1e-4f);
                Assert.AreEqual(v[i].y / 0.7f + 0.5f, uv[i].y, 1e-4f);
            }
            Assert.Greater(front, 100);
            Assert.Less(HeadMeshFactory.CreateCollider(0.3f).triangles.Length / 3, 255); // лимит выпуклого коллайдера
        }
    }
}
