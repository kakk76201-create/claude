using System.Collections;
using System.Collections.Generic;
using System.IO;
using MakeupSniper.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MakeupSniper.Tests
{
    /// <summary>
    /// Экскурсия по местам: детский праздник (попадание в ребёнка), свидание (девушка оборачивается,
    /// подозрение растёт), свадьба (два лица, жених красится отдельно). Скриншоты — в Logs/Shots.
    /// </summary>
    public class LocationsTourTest
    {
        static string ShotDir { get { return Path.Combine(Application.dataPath, "..", "Logs", "Shots"); } }
        readonly List<string> banners = new List<string>();

        static IEnumerator Until(System.Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Не дождались: " + what);
                yield return null;
            }
        }

        static IEnumerator Capture(string name)
        {
            yield return null;
            var w = World.Instance;
            Camera cam = w.mainCamera;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Canvas canvas = GameUI.Instance.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.2f;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Directory.CreateDirectory(ShotDir);
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        static void FireAtPoint(PlayerAgent me, int weapon, Vector3 target)
        {
            HeadRig head = World.Instance.Head(0);
            Vector3 origin = me.cameraAnchor.position;
            me.CmdFire((byte)weapon, origin, (target - origin).normalized, head.Yaw, head.Pitch);
        }

        static Vector3 FacePoint(PaintSurface face, Vector2 uv)
        {
            float lx = -(uv.x - 0.5f) * face.faceSize, ly = (uv.y - 0.5f) * face.faceSize;
            float lz = Mathf.Sqrt(Mathf.Max(0.0004f, face.headRadius * face.headRadius - lx * lx - ly * ly)) * 0.97f;
            return face.transform.TransformPoint(new Vector3(lx, ly, lz));
        }

        IEnumerator StartAt(PlayerAgent me, Match m, int location)
        {
            me.CmdHost((byte)HostCommand.ToLobby, 0);
            yield return Until(() => m.State == MatchState.Lobby, 5f, "лобби");
            me.CmdHost((byte)HostCommand.SetLocation, (byte)location);
            yield return Until(() => m.LocationIndex == location, 5f, "смена места " + location);
            me.CmdHost((byte)HostCommand.Start, 0);
            yield return Until(() => m.State == MatchState.Shoot, 15f, "стрельба в месте " + location);
            World.Instance.Head(0).idleSway = false;
            yield return new WaitForSeconds(1.0f);
        }

        [UnityTest]
        public IEnumerator Tour_PartyDateWedding()
        {
            // все места открыты: подкладываем свой файл прогресса
            Progression.OverridePath = Path.Combine(Application.temporaryCachePath, "progress-tour-test.json");
            new Progression { stars = new[] { 9, 9, 9, 0 } }.Save(Progression.OverridePath);

            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            var net = NetSession.Instance;
            net.Host(true);
            yield return Until(() => net.State == NetSession.Phase.Online && Match.Instance != null && PlayerAgent.Local != null && PlayerAgent.Local.Slot != 0, 20f, "хост");
            var me = PlayerAgent.Local;
            var m = Match.Instance;
            var w = World.Instance;
            m.BannerShown += (t, s, d) => banners.Add(t);
            yield return Until(() => m.IsUnlocked(3), 5f, "все места открыты");

            // ---------- детский праздник ----------
            me.CmdHost((byte)HostCommand.SetLocation, 1);
            yield return Until(() => m.LocationIndex == 1, 5f, "праздник в лобби");
            yield return new WaitForSeconds(0.3f);
            yield return Capture("20-lobby-party");
            yield return StartAt(me, m, 1);
            Assert.IsTrue(w.kids.gameObject.activeInHierarchy, "Дети на празднике");
            yield return Capture("21-party-shooter");
            float before = m.SecondsLeft;
            Transform kid = w.kids.kids[0];
            FireAtPoint(me, 0, kid.position + Vector3.up * 0.55f);
            yield return Until(() => banners.Exists(b => b.Contains("РЕБЁНКА")), 3f, "попадание в ребёнка");
            Assert.Less(m.SecondsLeft, before - 4f, "Попадание в ребёнка отнимает 5 секунд");

            // ---------- свидание ----------
            yield return StartAt(me, m, 2);
            Assert.IsNotNull(w.Witnesses, "Девушка на свидании");
            Assert.IsTrue(w.dateWitnesses.gameObject.activeInHierarchy);
            Assert.IsFalse(w.kids.gameObject.activeInHierarchy, "Детей на свидании нет");
            // дождаться, когда она обернётся, и выстрелить
            yield return Until(() => w.Witnesses.AnyLooking(m.Seed, m.RoundSeconds, m.RoundTime), 20f, "девушка обернулась");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("22-date-looking");
            FireAtPoint(me, 0, FacePoint(w.Face(0), new Vector2(0.5f, 0.47f)));
            yield return Until(() => m.Suspicion > 10f, 3f, "подозрение выросло");
            // попасть в саму девушку — подозрение сильно растёт
            float s0 = m.Suspicion;
            yield return new WaitForSeconds(1.6f);
            Transform girl = w.dateWitnesses.roots[0].transform;
            FireAtPoint(me, 0, girl.position + Vector3.up * 0.9f);
            yield return Until(() => m.Suspicion > s0 + 30f || m.State == MatchState.Reveal, 3f, "выстрел в девушку замечен");

            // ---------- свадьба ----------
            yield return StartAt(me, m, 3);
            Assert.IsTrue(w.groomRoot.activeInHierarchy, "Жених на свадьбе");
            Assert.AreEqual(2, w.weddingWitnesses.roots.Length);
            yield return Capture("23-wedding-shooter");
            int groomStamps = w.Face(1).StampCount;
            yield return new WaitForSeconds(0.2f);
            // дождаться, когда гости не смотрят, чтобы не сорвать раунд
            yield return Until(() => !w.Witnesses.AnyLooking(m.Seed, m.RoundSeconds, m.RoundTime), 10f, "гости отвернулись");
            FireAtPoint(me, 0, FacePoint(w.Face(1), new Vector2(0.5f, 0.33f)));
            yield return Until(() => w.Face(1).StampCount > groomStamps, 3f, "помада попала жениху");
            me.CmdHost((byte)HostCommand.Skip, 0);
            yield return Until(() => m.State == MatchState.Reveal && m.HasReveal, 5f, "раскрытие свадьбы");
            Assert.AreEqual(2, m.LastReveal.faceMatch.Length, "На свадьбе два лица");
            yield return new WaitForSeconds(2.2f);
            yield return Capture("24-wedding-reveal");

            me.CmdHost((byte)HostCommand.ToLobby, 0);
            yield return Until(() => m.State == MatchState.Lobby, 5f, "лобби");
            net.Leave();
            yield return Until(() => net.State == NetSession.Phase.Offline, 5f, "выход");
            Progression.OverridePath = null;
        }
    }
}
