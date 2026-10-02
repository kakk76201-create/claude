using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MakeupSniper.Net;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Автотест сетевой игры без человека: запускается только с ключом -ms-autotest host|join.
    /// Две копии игры на одном компьютере играют матч из двух раундов (каждый по разу в кресле),
    /// стреляют из всех стволов, Модель закрывается ладонью, уворачивается и размазывает краску.
    /// Обе копии пишут отчёт: отпечаток лица после каждого раунда должен совпасть.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string role, outPath, codeArg, shotsDir;
        StreamWriter log;
        readonly List<string> banners = new List<string>();
        Match bound;
        int lastLoggedReveal = -1;
        readonly HashSet<string> done = new HashSet<string>();

        public static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        void Awake()
        {
            role = Arg("-ms-autotest");
            if (string.IsNullOrEmpty(role)) { enabled = false; return; }
            outPath = Arg("-ms-out") ?? Path.Combine(Application.temporaryCachePath, "autotest-" + role + ".log");
            codeArg = Arg("-ms-code");
            shotsDir = Path.Combine(Path.GetDirectoryName(outPath), "shots-" + role);
            Directory.CreateDirectory(shotsDir);
            log = new StreamWriter(outPath, false, new UTF8Encoding(false)) { AutoFlush = true };
            Progression.OverridePath = Path.Combine(Path.GetDirectoryName(outPath), "progress-" + role + ".json");
            PolaroidSaver.FolderOverride = Path.Combine(Path.GetDirectoryName(outPath), "polaroids-" + role);
            PlayerPrefs.SetString(PlayerAgent.NamePref, role == "host" ? "Хост-бот" : "Гость-бот");
            Application.targetFrameRate = 60;
            Write("START role=" + role + " pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        }

        void Write(string line)
        {
            string s = DateTime.Now.ToString("HH:mm:ss.fff") + " " + line;
            if (log != null) log.WriteLine(s);
            Debug.Log("[AutoTest] " + s);
        }

        IEnumerator Start()
        {
            if (!enabled) yield break;
            float deadline = Time.realtimeSinceStartup + 240f;
            var net = NetSession.Instance;
            while (net == null) { yield return null; net = NetSession.Instance; }
            yield return new WaitForSeconds(0.5f);

            if (role == "host")
            {
                net.Host(false);
                while (!(net.State == NetSession.Phase.Online && PlayerAgent.Local != null && PlayerAgent.Local.Slot != 0 && Match.Instance != null))
                {
                    if (Time.realtimeSinceStartup > deadline || net.State == NetSession.Phase.Failed) { Fail("host did not start: " + net.Message); yield break; }
                    yield return null;
                }
                string code = JoinCode.EncodeDirect("127.0.0.1", net.Port);
                File.WriteAllText(outPath + ".code", code);
                Write("HOSTED port=" + net.Port + " code=" + code + " lanCode=" + net.DirectCode + " adapters=" + net.Adapters.Count);
                Capture("01-lobby-host");
                int expected = 2;
                int.TryParse(Arg("-ms-players") ?? "2", out expected);
                while (CountPlayers() < expected)
                {
                    if (Time.realtimeSinceStartup > deadline) { Fail("guest did not join"); yield break; }
                    yield return null;
                }
                Write("GUEST JOINED players=" + CountPlayers());
                yield return new WaitForSeconds(1.5f);
                Capture("02-lobby-two");
                PlayerAgent.Local.CmdHost((byte)HostCommand.Start, 0);
            }
            else
            {
                if (string.IsNullOrEmpty(codeArg)) { Fail("no -ms-code"); yield break; }
                net.Join(codeArg);
                while (!(net.State == NetSession.Phase.Online && PlayerAgent.Local != null && PlayerAgent.Local.Slot != 0 && Match.Instance != null))
                {
                    if (Time.realtimeSinceStartup > deadline || net.State == NetSession.Phase.Failed) { Fail("join failed: " + net.Message); yield break; }
                    yield return null;
                }
                Write("JOINED slot=" + PlayerAgent.Local.Slot);
            }

            // основной цикл матча
            while (Time.realtimeSinceStartup < deadline)
            {
                var m = Match.Instance;
                var me = PlayerAgent.Local;
                if (m == null || me == null || net.State != NetSession.Phase.Online)
                {
                    if (role == "join" && done.Contains("summary")) break;
                    Fail("lost connection state=" + net.State + " msg=" + net.Message);
                    yield break;
                }
                Bind(m);
                switch (m.State)
                {
                    case MatchState.Pick:
                        if (me.IsModelNow && m.PickOptions.Length > 0 && Once("pick" + m.Round))
                        {
                            yield return new WaitForSeconds(0.8f);
                            Capture("r" + m.Round + "-pick");
                            me.CmdPick(0, 50);
                            Write("PICK round=" + m.Round);
                        }
                        break;
                    case MatchState.Shoot:
                        if (Once("shoot" + m.Round)) StartCoroutine(me.IsModelNow ? ModelScript(m.Round) : (Arg("-ms-splat") != null ? SplatScript(m.Round) : ShooterScript(m.Round)));
                        if (role == "host" && m.RoundTime > 17f && Once("skip" + m.Round)) me.CmdHost((byte)HostCommand.Skip, 0);
                        break;
                    case MatchState.Reveal:
                        if (m.HasReveal && lastLoggedReveal != m.Round)
                        {
                            lastLoggedReveal = m.Round;
                            yield return new WaitForSeconds(2.2f);
                            LogReveal(m);
                            Capture("r" + m.Round + "-reveal");
                            if (role == "host") { yield return new WaitForSeconds(1.0f); me.CmdHost((byte)HostCommand.Skip, 0); }
                        }
                        break;
                    case MatchState.Summary:
                        if (m.HasSummary && Once("summary"))
                        {
                            yield return new WaitForSeconds(1.0f);
                            var s = m.LastSummary;
                            Write("SUMMARY best=" + s.bestArtist + " | face=" + s.bestFace + " | rounds=" + (s.rounds != null ? s.rounds.Length : 0));
                            Capture("summary");
                            if (role == "host")
                            {
                                yield return new WaitForSeconds(3f);
                                Write("BANNERS " + string.Join(" / ", banners));
                                Finish();
                                yield break;
                            }
                        }
                        break;
                }
                yield return null;
            }
            if (role == "join" && done.Contains("summary"))
            {
                Write("BANNERS " + string.Join(" / ", banners));
                Finish();
            }
            else Fail("timeout");
        }

        bool Once(string key) { return done.Add(key); }

        void Bind(Match m)
        {
            if (bound == m) return;
            bound = m;
            m.BannerShown += (text, style, s) => banners.Add(text);
            m.VisionGranted += (seconds, option) => Write("VISION seconds=" + seconds + " option=" + option);
        }

        static int CountPlayers()
        {
            int n = 0;
            foreach (var a in PlayerAgent.All) if (a != null && a.Slot != 0) n++;
            return n;
        }

        IEnumerator WaitRoundTime(float t)
        {
            var m = Match.Instance;
            while (m != null && m.State == MatchState.Shoot && m.RoundTime < t) yield return null;
        }

        /// <summary>Модель: поворот головы, ладонь, уворот, размазать.</summary>
        IEnumerator ModelScript(int round)
        {
            var me = PlayerAgent.Local;
            Write("MODEL round=" + round);
            yield return WaitRoundTime(0.8f);
            var head = World.Instance.Head(0);
            head.SetTarget(18f, -6f);
            me.CmdPose(18f, -6f);
            yield return WaitRoundTime(2.0f);
            Capture("r" + round + "-model-view");
            yield return WaitRoundTime(4.6f);
            me.CmdModelAction((byte)ModelAction.Palm);
            Write("MODEL palm t=" + Match.Instance.RoundTime.ToString("0.00"));
            yield return WaitRoundTime(7.6f);
            me.CmdModelAction((byte)ModelAction.Dodge);
            Write("MODEL dodge t=" + Match.Instance.RoundTime.ToString("0.00"));
            yield return WaitRoundTime(13.5f);
            me.CmdModelAction((byte)ModelAction.Smear);
            Write("MODEL smear t=" + Match.Instance.RoundTime.ToString("0.00"));
        }

        /// <summary>Стрелок-хулиган: забрызгивает другого стрелка из базуки (тот должен увидеть «видение»).</summary>
        IEnumerator SplatScript(int round)
        {
            var me = PlayerAgent.Local;
            Write("SPLATTER round=" + round);
            yield return WaitRoundTime(2.0f);
            me.CmdSelect(2);
            yield return WaitRoundTime(3.5f);
            PlayerAgent victim = null;
            foreach (var a in PlayerAgent.All)
                if (a != null && a != me && a.Slot != 0 && !a.IsModelNow) victim = a;
            if (victim == null) { Write("SPLAT no victim"); yield break; }
            Vector3 origin = me.cameraAnchor.position;
            Vector3 target = victim.transform.position + Vector3.up * 1.0f;
            float t = Vector3.Distance(origin, target) / 24f;
            target += Vector3.up * 0.5f * 9.81f * t * t;
            var head = World.Instance.Head(0);
            me.CmdFire(2, origin, (target - origin).normalized, head.Yaw, head.Pitch);
            Write("SPLAT fire at slot " + victim.Slot);
            yield return WaitRoundTime(10.0f);
            Fire(me, 2, new Vector2(0.31f, 0.43f), 24f);
        }

        /// <summary>Стрелок: помада в нос, выстрел в ладонь, в уворот, тушь, румяна, ластик.</summary>
        IEnumerator ShooterScript(int round)
        {
            var me = PlayerAgent.Local;
            Write("SHOOTER round=" + round);
            yield return WaitRoundTime(2.5f);
            Fire(me, 0, new Vector2(0.50f, 0.47f), 0f);            // помада в нос
            yield return WaitRoundTime(4.2f);
            Fire(me, 0, new Vector2(0.44f, 0.35f), 0f);            // помада в губы
            yield return WaitRoundTime(5.8f);
            Fire(me, 0, new Vector2(0.50f, 0.42f), 0f);            // в поднятую ладонь (перезарядка помады 1,5 с)
            yield return WaitRoundTime(7.75f);
            Fire(me, 0, new Vector2(0.56f, 0.33f), 0f);            // во время уворота — в ухо
            yield return WaitRoundTime(9.0f);
            me.CmdSelect(2);
            yield return WaitRoundTime(10.0f);
            Fire(me, 2, new Vector2(0.69f, 0.43f), 24f);           // румяна-базука в левую щёку
            Capture("r" + round + "-shooter-view");
            yield return WaitRoundTime(11.0f);
            me.CmdSelect(1);
            yield return WaitRoundTime(12.0f);
            Fire(me, 1, new Vector2(0.39f, 0.60f), 45f);           // тушь в правый глаз
            yield return WaitRoundTime(14.5f);
            me.CmdSelect(3);
            yield return WaitRoundTime(15.5f);
            Fire(me, 3, new Vector2(0.50f, 0.47f), 30f, 0.6f);     // тональник по носу
        }

        void Fire(PlayerAgent me, int weapon, Vector2 uv, float speed, float gravityScale = 1f)
        {
            var w = World.Instance;
            PaintSurface face = w.Face(0);
            HeadRig head = w.Head(0);
            Vector3 origin = me.cameraAnchor.position;
            float lx = -(uv.x - 0.5f) * face.faceSize, ly = (uv.y - 0.5f) * face.faceSize;
            float lz = Mathf.Sqrt(Mathf.Max(0.0004f, face.headRadius * face.headRadius - lx * lx - ly * ly)) * 0.97f;
            Vector3 target = face.transform.TransformPoint(new Vector3(lx, ly, lz));
            Vector3 aim = target;
            if (speed > 0f)
            {
                float d = Vector3.Distance(origin, target);
                float t = d / speed;
                aim += Vector3.up * 0.5f * 9.81f * gravityScale * t * t;
            }
            Vector3 dir = (aim - origin).normalized;
            // снаряд вылетает на 0,6 м впереди глаз — как в Match
            me.CmdFire((byte)weapon, origin, dir, head.Yaw, head.Pitch);
            Write("FIRE w=" + weapon + " uv=" + uv + " t=" + Match.Instance.RoundTime.ToString("0.00"));
        }

        void LogReveal(Match m)
        {
            var p = m.LastReveal;
            var w = World.Instance;
            var sb = new StringBuilder();
            sb.Append("REVEAL round=").Append(m.Round).Append(" match=").Append(p.match).Append(" stars=").Append(p.stars)
              .Append(" hash=").Append(Match.FacesHash()).Append(" stamps0=").Append(w.Face(0).StampCount)
              .Append(" painted0=").Append(w.Face(0).Grid.PaintedCount).Append(" model=").Append(p.modelSlot);
            if (p.players != null) foreach (var pl in p.players) sb.Append(" | ").Append(pl.slot).Append(':').Append(pl.points).Append(" (").Append(pl.note).Append(')');
            Write(sb.ToString());
        }

        void Capture(string name)
        {
            try
            {
                var w = World.Instance;
                var ui = GameUI.Instance;
                if (w == null || w.mainCamera == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
                Camera cam = w.mainCamera;
                var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Canvas canvas = ui != null ? ui.Canvas : null;
                if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 0.2f; }
                Canvas.ForceUpdateCanvases();
                cam.targetTexture = rt;
                cam.Render();
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                File.WriteAllBytes(Path.Combine(shotsDir, name + ".png"), tex.EncodeToPNG());
                Destroy(tex);
                rt.Release();
            }
            catch (Exception e)
            {
                Write("CAPTURE FAILED " + name + ": " + e.Message);
            }
        }

        void Fail(string reason)
        {
            Write("FAIL " + reason);
            StartCoroutine(QuitSoon(2));
        }

        void Finish()
        {
            // что должно было случиться за матч (оба раунда)
            var missing = new List<string>();
            var expected = new List<string> { "В ЛАДОНЬ", "УВОРОТ", "В ГЛАЗ", "размазала" };
            if (Arg("-ms-expect-splat") != null) expected.Add("ЗАБРЫЗГАН");
            foreach (string expect in expected)
                if (!banners.Exists(b => b.Contains(expect))) missing.Add(expect);
            Write(missing.Count == 0 ? "CHECK banners ok" : "CHECK banners missing: " + string.Join(", ", missing));
            Write("DONE");
            StartCoroutine(QuitSoon(0));
        }

        IEnumerator QuitSoon(int code)
        {
            yield return new WaitForSeconds(1f);
            if (NetSession.Instance != null) NetSession.Instance.Leave();
            yield return new WaitForSeconds(1f);
            if (log != null) { log.Flush(); log.Close(); log = null; }
            Application.Quit(code);
        }
    }
}
