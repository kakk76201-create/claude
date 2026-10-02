using System.Collections;
using System.IO;
using MakeupSniper.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MakeupSniper.Tests
{
    /// <summary>
    /// Сквозная проверка в настоящем Play Mode: тренировка одному (хост + бот-Модель).
    /// Меню → лобби → раунд со всеми стволами → раскрытие → итоги. Скриншоты — в Logs/Shots.
    /// </summary>
    public class SoloMatchTest
    {
        static string ShotDir { get { return Path.Combine(Application.dataPath, "..", "Logs", "Shots"); } }

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
            var ui = GameUI.Instance;
            Camera cam = w.mainCamera;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Canvas canvas = ui.Canvas;
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

        static void FireAt(PlayerAgent me, int weapon, Vector2 uv, float speed, float gravityScale)
        {
            var w = World.Instance;
            PaintSurface face = w.Face(0);
            HeadRig head = w.Head(0);
            Vector3 origin = me.cameraAnchor.position;
            float lx = -(uv.x - 0.5f) * face.faceSize, ly = (uv.y - 0.5f) * face.faceSize;
            float lz = Mathf.Sqrt(Mathf.Max(0.0004f, face.headRadius * face.headRadius - lx * lx - ly * ly)) * 0.97f;
            Vector3 target = face.transform.TransformPoint(new Vector3(lx, ly, lz));
            if (speed > 0f)
            {
                float t = Vector3.Distance(origin, target) / speed;
                target += Vector3.up * 0.5f * 9.81f * gravityScale * t * t;
            }
            me.CmdFire((byte)weapon, origin, (target - origin).normalized, head.Yaw, head.Pitch);
        }

        [UnityTest]
        public IEnumerator SoloPractice_FullMatch()
        {
            Progression.OverridePath = Path.Combine(Application.temporaryCachePath, "progress-playmode-test.json");
            PolaroidSaver.FolderOverride = Path.Combine(Application.temporaryCachePath, "polaroids-playmode-test");
            if (Directory.Exists(PolaroidSaver.FolderOverride)) Directory.Delete(PolaroidSaver.FolderOverride, true);
            if (File.Exists(Progression.OverridePath)) File.Delete(Progression.OverridePath);

            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;
            var net = NetSession.Instance;
            Assert.IsNotNull(net, "В сцене нет NetSession");
            Assert.IsNotNull(World.Instance, "В сцене нет World");
            Assert.AreEqual(4, World.Instance.weapons.Length);
            Assert.AreEqual(4, World.Instance.locations.Length);
            yield return Capture("10-menu");

            net.Host(true);
            yield return Until(() => net.State == NetSession.Phase.Online && Match.Instance != null && PlayerAgent.Local != null && PlayerAgent.Local.Slot != 0, 20f, "хост запустился");
            var me = PlayerAgent.Local;
            var m = Match.Instance;
            Assert.AreEqual(1, me.Slot);
            Assert.IsTrue(me.IsHostPlayer, "Свой игрок хоста должен считаться хостом");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("11-lobby");

            me.CmdHost((byte)HostCommand.Start, 0);
            yield return Until(() => m.State == MatchState.Shoot, 15f, "начало стрельбы");
            Assert.IsTrue(m.BotModel, "В тренировке Модель — бот");
            Assert.GreaterOrEqual(m.ServerOptionForTests, 0, "Бот выбрал образ");
            var w = World.Instance;
            HeadRig head = w.Head(0);
            head.idleSway = false;
            yield return new WaitForSeconds(1.2f); // голова встаёт прямо, игрок стоит на линии 3 м

            int before = w.Face(0).StampCount;
            FireAt(me, 0, new Vector2(0.50f, 0.47f), 0f, 1f);   // помада в нос
            yield return Until(() => w.Face(0).StampCount > before, 3f, "помада попала в лицо");
            int noseCell = Mathf.FloorToInt(0.47f * FaceGrid.Size) * FaceGrid.Size + FaceGrid.Size / 2;
            Assert.AreEqual(PaintColor.Red, w.Face(0).Grid.Colors[noseCell], "Помада в носу");
            Assert.AreEqual(1, w.Face(0).Grid.Owners[noseCell], "Краска записана на игрока 1");

            me.CmdSelect(2);
            yield return new WaitForSeconds(0.8f);
            before = w.Face(0).StampCount;
            FireAt(me, 2, new Vector2(0.69f, 0.43f), 24f, 1f);  // румяна-базука в щёку
            yield return Until(() => w.Face(0).StampCount > before, 3f, "базука долетела");
            yield return Capture("12-shooter");

            me.CmdSelect(1);
            yield return new WaitForSeconds(0.8f);
            before = w.Face(0).StampCount;
            FireAt(me, 1, new Vector2(0.39f, 0.60f), 45f, 1f);  // тушь в глаз
            yield return Until(() => w.Face(0).StampCount > before, 3f, "тушь долетела");

            // ластик стирает нос
            me.CmdSelect(3);
            yield return new WaitForSeconds(0.8f);
            before = w.Face(0).StampCount;
            FireAt(me, 3, new Vector2(0.50f, 0.47f), 30f, 0.6f);
            yield return Until(() => w.Face(0).StampCount > before, 3f, "тональник долетел");
            Assert.AreEqual(PaintColor.None, w.Face(0).Grid.Colors[noseCell], "Тональник стёр нос");
            Texture2D face = w.Face(0).ReadBack();
            Color nosePixel = face.GetPixel(face.width / 2, Mathf.RoundToInt(0.45f * face.height));
            Object.Destroy(face);
            Assert.Less(Mathf.Abs(nosePixel.r - nosePixel.g), 0.2f, "На картинке нос снова цвета кожи: " + nosePixel);
            var baseTex = (Texture2D)w.Face(0).BaseTexture;
            Color basePixel = baseTex.GetPixel(baseTex.width / 2, Mathf.RoundToInt(0.45f * baseTex.height));
            Debug.Log("[MakeupSniper] после ластика: " + nosePixel + " чистое лицо: " + basePixel);
            Assert.AreEqual(basePixel.r, nosePixel.r, 0.035f, "Ластик возвращает тот же цвет кожи (без светлого круга)");
            Assert.AreEqual(basePixel.b, nosePixel.b, 0.035f, "Ластик возвращает тот же цвет кожи (без светлого круга)");

            // конец раунда — раскрытие
            me.CmdHost((byte)HostCommand.Skip, 0);
            yield return Until(() => m.State == MatchState.Reveal && m.HasReveal, 5f, "раскрытие");
            var p = m.LastReveal;
            Assert.IsNotNull(p.players);
            Assert.AreEqual(1, p.players.Length);
            Assert.IsNotNull(p.zones);
            Assert.Greater(p.zones.Length, 0);
            Debug.Log("[MakeupSniper] Раунд 1: " + p.match + "%, очки " + p.players[0].points + " (" + p.players[0].note + ")");
            yield return new WaitForSeconds(2.2f);
            yield return Capture("13-reveal");
            string[] saved = Directory.Exists(PolaroidSaver.FolderOverride) ? Directory.GetFiles(PolaroidSaver.FolderOverride, "*.png") : new string[0];
            Assert.AreEqual(1, saved.Length, "Полароид раунда сохранён картинкой");
            File.Copy(saved[0], Path.Combine(ShotDir, "16-polaroid-file.png"), true);

            // оставшиеся раунды пролистываем
            int guard = 0;
            while (m.State != MatchState.Summary && guard++ < 2000)
            {
                if (m.State == MatchState.Reveal || m.State == MatchState.Shoot) me.CmdHost((byte)HostCommand.Skip, 0);
                yield return new WaitForSeconds(0.25f);
            }
            yield return Until(() => m.State == MatchState.Summary && m.HasSummary, 30f, "итоги вечера");
            Assert.AreEqual(Match.SoloRounds, m.LastSummary.rounds.Length);
            yield return new WaitForSeconds(0.3f);
            yield return Capture("14-summary");

            me.CmdHost((byte)HostCommand.ToLobby, 0);
            yield return Until(() => m.State == MatchState.Lobby, 5f, "возврат в лобби");
            net.Leave();
            yield return Until(() => net.State == NetSession.Phase.Offline, 5f, "выход в меню");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("15-menu-after");
            Progression.OverridePath = null;
            PolaroidSaver.FolderOverride = null;
        }
    }
}
