using NUnit.Framework;
using UnityEngine;

namespace MakeupSniper.Tests
{
    /// <summary>Правила игры, которые не зависят от сети: очки, «стереть», полосы туши, звёзды, расписания помех.</summary>
    public class GameRulesTests
    {
        const float FaceRadius = 0.3f / 0.7f;

        static ReferenceData Make(params ZoneTarget[] zones)
        {
            var r = ScriptableObject.CreateInstance<ReferenceData>();
            r.zones = zones;
            return r;
        }

        static void Fill(FaceGrid g, ZoneTarget z, PaintColor c, byte owner, byte mult)
        {
            for (int i = 0; i < FaceGrid.Size * FaceGrid.Size; i++)
                if (z.Contains(FaceGrid.CellCenter(i))) { g.Colors[i] = c; g.Owners[i] = owner; g.Mults[i] = mult; }
        }

        [Test]
        public void ZonePoints_SplitByOwnerAndMultiplier()
        {
            var nose = new ZoneTarget { name = "Нос", center = new Vector2(0.5f, 0.47f), radius = new Vector2(0.055f, 0.05f), color = PaintColor.Red };
            var g = new FaceGrid();
            Fill(g, nose, PaintColor.Red, 1, 1);
            // половину носа перекрасил игрок 2 с линии 15 м (×3)
            int n = 0;
            for (int i = 0; i < g.Colors.Length; i++)
                if (nose.Contains(FaceGrid.CellCenter(i)) && (n++ % 2 == 0)) { g.Owners[i] = 2; g.Mults[i] = 3; }
            var s = Scorer.Score(g, Make(nose), FaceRadius);
            Assert.IsTrue(s.zones[0].ok);
            Assert.AreEqual(100, s.matchPercent);
            float p1 = s.zonePoints[1], p2 = s.zonePoints[2];
            Assert.AreEqual(Scorer.ZonePoints * 0.5f, p1, 1.5f);
            Assert.AreEqual(Scorer.ZonePoints * 0.5f * 3f, p2, 4.5f);
        }

        [Test]
        public void FailedZone_BlamesWrongColorOwner()
        {
            var lips = new ZoneTarget { name = "Губы", center = new Vector2(0.5f, 0.33f), radius = new Vector2(0.10f, 0.045f), color = PaintColor.Red };
            var g = new FaceGrid();
            Fill(g, lips, PaintColor.Black, 3, 1);
            var s = Scorer.Score(g, Make(lips), FaceRadius);
            Assert.IsFalse(s.zones[0].ok);
            Assert.AreEqual(3, s.zones[0].blame);
        }

        [Test]
        public void CleanZone_NeedsErasingDirt()
        {
            var zone = new ZoneTarget { name = "Засос — стереть", center = new Vector2(0.64f, 0.21f), radius = new Vector2(0.075f, 0.065f), color = PaintColor.None };
            var g = new FaceGrid();
            // грязь в начале раунда (как ApplyStartDirt: диск радиусом 0.045 по uv)
            g.StampDisc(new Vector2(0.64f, 0.21f), 0.045f, PaintColor.Red, 0);
            var before = Scorer.Score(g, Make(zone), FaceRadius);
            Assert.IsFalse(before.zones[0].ok, "Грязь должна закрывать больше 30% зоны, иначе стирать не нужно");
            Assert.AreEqual(0, before.overshootCells, "Грязь не должна вылезать за зону");
            // стёрли тональником (игрок 2)
            g.StampDisc(new Vector2(0.64f, 0.21f), 0.1f / 0.7f, PaintColor.None, 2);
            var after = Scorer.Score(g, Make(zone), FaceRadius);
            Assert.IsTrue(after.zones[0].ok);
            Assert.AreEqual(100, after.matchPercent);
            Assert.IsTrue(after.zonePoints.ContainsKey(2), "Очки за стирание достаются тому, кто стёр");
        }

        [Test]
        public void Stripe_FollowsAngle()
        {
            var g = new FaceGrid();
            int n = g.StampStripe(new Vector2(0.5f, 0.5f), 0.3f, 0.01f, 0f, PaintColor.Black, 1);
            Assert.Greater(n, 10);
            // горизонтальная полоса: краска слева и справа от центра, но не сверху
            Assert.AreEqual(PaintColor.Black, g.Colors[32 * 64 + 40]);
            Assert.AreEqual(PaintColor.Black, g.Colors[32 * 64 + 24]);
            Assert.AreEqual(PaintColor.None, g.Colors[40 * 64 + 32]);
            var v = new FaceGrid();
            v.StampStripe(new Vector2(0.5f, 0.5f), 0.3f, 0.01f, 90f, PaintColor.Black, 1);
            Assert.AreEqual(PaintColor.Black, v.Colors[40 * 64 + 32]);
            Assert.AreEqual(PaintColor.None, v.Colors[32 * 64 + 40]);
        }

        [Test]
        public void ThresholdIsPerZone()
        {
            var freckles = new ZoneTarget { name = "Веснушки", center = new Vector2(0.67f, 0.46f), radius = new Vector2(0.10f, 0.08f), color = PaintColor.Black, required = 0.2f };
            var g = new FaceGrid();
            int total = 0, painted = 0;
            for (int i = 0; i < g.Colors.Length; i++) if (freckles.Contains(FaceGrid.CellCenter(i))) total++;
            for (int i = 0; i < g.Colors.Length && painted < Mathf.CeilToInt(total * 0.25f); i++)
                if (freckles.Contains(FaceGrid.CellCenter(i)) && i % 3 == 0) { g.Colors[i] = PaintColor.Black; g.Owners[i] = 1; g.Mults[i] = 1; painted++; }
            var s = Scorer.Score(g, Make(freckles), FaceRadius);
            Assert.IsTrue(s.zones[0].ok, "Для веснушек достаточно 20% покрытия");
        }

        [Test]
        public void Stars_Thresholds()
        {
            Assert.AreEqual(0, Scorer.Stars(39));
            Assert.AreEqual(1, Scorer.Stars(40));
            Assert.AreEqual(2, Scorer.Stars(60));
            Assert.AreEqual(3, Scorer.Stars(80));
        }

        [Test]
        public void Progression_UnlocksAfterFourStars()
        {
            var p = new Progression();
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(1));
            Assert.AreEqual(-1, p.AddStars(0, 3, 4));
            Assert.AreEqual(1, p.AddStars(0, 1, 4));
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.IsFalse(p.IsUnlocked(2));
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "ms_progress_test.json");
            p.Save(path);
            var loaded = Progression.Load(path);
            Assert.AreEqual(4, loaded.StarsAt(0));
        }

        [Test]
        public void Gimmicks_AreDeterministic()
        {
            var a = GimmickMath.WitnessWindows(42, 60f, 0);
            var b = GimmickMath.WitnessWindows(42, 60f, 0);
            Assert.Greater(a.Count, 2);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].start, b[i].start);
            foreach (var w in a) { Assert.GreaterOrEqual(w.end - w.start, 0f); Assert.LessOrEqual(w.end - w.start, 3.61f); }
            Assert.AreEqual(GimmickMath.KidPosition(7, 1, 12.5f), GimmickMath.KidPosition(7, 1, 12.5f));
            // дети бегают между креслом и линией 3 м
            for (float t = 0f; t < 60f; t += 0.37f)
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = GimmickMath.KidPosition(3, i, t);
                    Assert.LessOrEqual(Mathf.Abs(p.x), 3.5f);
                    Assert.Greater(p.z, 0.8f);
                    Assert.Less(p.z, 2.6f);
                }
        }

        [Test]
        public void Multiplier_ByLine()
        {
            Assert.AreEqual(1, World.LineMultiplier(3.2f));
            Assert.AreEqual(2, World.LineMultiplier(8.1f));
            Assert.AreEqual(3, World.LineMultiplier(15.5f));
        }
    }
}
