using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MakeupSniper.Tests
{
    /// <summary>
    /// Сквозная проверка недели 1 в настоящем Play Mode: сцена грузится, выстрел красит лицо там,
    /// куда попал, базука долетает по параболе, раунд заканчивается раскрытием со счётом.
    /// Попутно сохраняет скриншоты в Logs/Shots.
    /// </summary>
    public class Week1SmokeTest
    {
        static string ShotDir { get { return Path.Combine(Application.dataPath, "..", "Logs", "Shots"); } }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        // Снимок экрана через RenderTexture: интерфейс временно привязываем к камере, чтобы он попал в кадр.
        static IEnumerator Capture(RoundManager rm, Camera cam, string name)
        {
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Canvas canvas = rm.ui.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.2f;
            cam.targetTexture = rt;
            yield return null;
            yield return null;
            cam.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(ShotDir);
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(tex);
            rt.Release();
            yield return null;
        }

        static bool IsReddish(Color c) { return c.r > 0.6f && c.g < 0.45f && c.b < 0.5f; }
        static bool IsSkin(Color c) { return c.r > 0.9f && c.g > 0.85f && c.b > 0.85f; }

        [UnityTest]
        public IEnumerator FullRound_PaintsScoresAndReveals()
        {
            yield return SceneManager.LoadSceneAsync("Week1", LoadSceneMode.Single);
            yield return Frames(3);

            var rm = Object.FindAnyObjectByType<RoundManager>();
            Assert.IsNotNull(rm, "В сцене нет RoundManager");
            Assert.AreEqual(RoundState.Pick, rm.State);
            Assert.AreEqual(3, rm.references.Length);
            yield return Capture(rm, rm.modelCamera, "01-pick");

            // Клоун, сразу стрельба
            rm.DebugStart(0);
            rm.head.idleSway = false;
            yield return Frames(40); // голова успевает встать прямо
            Assert.AreEqual(RoundState.Shoot, rm.State);
            Assert.AreEqual(Role.Shooter, rm.CurrentRole);
            Assert.AreEqual(0, rm.face.Grid.PaintedCount);

            PaintSurface face = rm.face;
            Camera cam = rm.shooterCamera;

            // 1) Выстрел помадой в нос с линии 3 м: луч → локальная точка → uv
            Vector3 noseLocal = new Vector3(0f, (0.47f - 0.5f) * face.faceSize, face.headRadius);
            Vector3 noseWorld = face.transform.TransformPoint(noseLocal);
            rm.weapons.FireRay(new Ray(cam.transform.position, (noseWorld - cam.transform.position).normalized));
            yield return null;
            Assert.AreEqual(1, rm.weapons.FaceHits, "Выстрел в нос не попал в лицо");
            Assert.Greater(face.Grid.PaintedCount, 0, "Сетка 64×64 не изменилась");
            int noseCell = Mathf.FloorToInt(0.47f * FaceGrid.Size) * FaceGrid.Size + FaceGrid.Size / 2;
            Assert.AreEqual(PaintColor.Red, face.Grid.Colors[noseCell], "В сетке на месте носа нет красного");

            // 2) Картинка: штамп стоит там же, где uv, и не перевёрнут
            face.StampUv(new Vector2(0.30f, 0.75f), 0.03f, PaintColor.Red, BrushKind.Smudge, 1, 0f);
            yield return null;
            Texture2D read = face.ReadBack();
            Directory.CreateDirectory(ShotDir);
            File.WriteAllBytes(Path.Combine(ShotDir, "00-face-texture.png"), read.EncodeToPNG());
            int S = face.textureSize;
            Assert.IsTrue(IsReddish(read.GetPixel((int)(0.30f * S), (int)(0.75f * S))), "Штамп не там, где его uv: " + read.GetPixel((int)(0.30f * S), (int)(0.75f * S)));
            Assert.IsTrue(IsSkin(read.GetPixel((int)(0.30f * S), (int)(0.25f * S))), "Штамп отзеркален по вертикали");
            Assert.IsTrue(IsSkin(read.GetPixel((int)(0.70f * S), (int)(0.75f * S))), "Штамп отзеркален по горизонтали");
            Object.Destroy(read);

            // 3) Базука: снаряд летит по параболе и долетает с 3 м
            rm.weapons.Select(1);
            int hitsBefore = rm.weapons.FaceHits;
            Vector3 cheekWorld = face.transform.TransformPoint(new Vector3((0.69f - 0.5f) * face.faceSize, (0.43f - 0.5f) * face.faceSize, 0.2f));
            Vector3 aim = cheekWorld + Vector3.up * 0.08f; // поправка на падение за 0,125 с полёта
            rm.weapons.FireRay(new Ray(cam.transform.position, (aim - cam.transform.position).normalized));
            float wait = 0f;
            while (rm.weapons.FaceHits == hitsBefore && wait < 2f) { wait += Time.deltaTime; yield return null; }
            Assert.AreEqual(hitsBefore + 1, rm.weapons.FaceHits, "Снаряд базуки не попал в лицо");
            int pink = 0;
            foreach (var c in face.Grid.Colors) if (c == PaintColor.Pink) pink++;
            Assert.Greater(pink, 20, "Розового пятна базуки нет в сетке");

            // 4) Докрашиваем клоуна напрямую и снимаем вид стрелка
            face.StampUv(new Vector2(0.50f, 0.47f), 0.036f, PaintColor.Red, BrushKind.Kiss, 1, 0f);
            foreach (float x in new[] { 0.44f, 0.50f, 0.56f }) face.StampUv(new Vector2(x, 0.33f), 0.03f, PaintColor.Red, BrushKind.Kiss, 1, 0f);
            face.StampUv(new Vector2(0.69f, 0.43f), 0.055f, PaintColor.Pink, BrushKind.Blush, 1, 0f);
            face.StampUv(new Vector2(0.31f, 0.43f), 0.055f, PaintColor.Pink, BrushKind.Blush, 1, 0f);
            yield return Capture(rm, cam, "02-shooter");

            // 5) Роль Модели: камера Модели включается, голова слушается «мыши»
            rm.SetRole(Role.Model);
            yield return null;
            Assert.IsTrue(rm.modelCamera.enabled);
            Assert.IsFalse(rm.shooterCamera.enabled);
            Assert.IsTrue(rm.head.ModelControlled);
            rm.head.AddLook(new Vector2(10000f, 0f));
            float turn = 0f;
            while (turn < 1.5f) { turn += Time.deltaTime; yield return null; }
            Assert.AreEqual(rm.head.yawLimit, rm.head.Yaw, 1.5f, "Голова не повернулась до ограничителя 35°");
            yield return Capture(rm, rm.modelCamera, "03-model");
            rm.SetRole(Role.Shooter);

            // 6) Раскрытие
            rm.EndRound();
            Assert.AreEqual(RoundState.Reveal, rm.State);
            float t = 0f;
            while (!rm.PolaroidShown && t < 6f) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(rm.PolaroidShown, "Полароид не показался");
            Assert.IsNotNull(rm.LastScore);
            Assert.AreEqual(4, rm.LastScore.zones.Length);
            Assert.GreaterOrEqual(rm.LastScore.matchPercent, 60, "Накрашенный клоун набрал слишком мало");
            Debug.Log("[MakeupSniper] Итог теста: " + rm.LastScore.matchPercent + "%, залёт " + rm.LastScore.overshootCells + " ячеек");
            yield return Capture(rm, rm.revealCamera, "04-reveal");

            // 7) Новый раунд начинается с чистого лица
            rm.EnterPick();
            yield return null;
            Assert.AreEqual(RoundState.Pick, rm.State);
            Assert.AreEqual(0, rm.face.Grid.PaintedCount);
        }
    }
}
