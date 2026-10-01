using UnityEngine;
using UnityEngine.InputSystem;

namespace MakeupSniper
{
    public enum RoundState { Pick, Countdown, Shoot, Reveal }
    public enum Role { Shooter, Model }

    /// <summary>
    /// Ход раунда недели 1: выбор референса → отсчёт → 60 секунд стрельбы → раскрытие.
    /// Tab переключает роль (стрелок ↔ Модель), чтобы тестировать в одиночку.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        public ReferenceData[] references = new ReferenceData[0];
        public PaintSurface face;
        public HeadRig head;
        public ShooterController shooter;
        public WeaponSystem weapons;
        public Camera shooterCamera, modelCamera, revealCamera;
        public GameUI ui;
        public Sfx sfx;
        public float roundSeconds = 60f;
        public float countdownSeconds = 3f;
        public float revealDelay = 2f;

        public RoundState State { get; private set; }
        public Role CurrentRole { get; private set; }
        public ReferenceData Current { get; private set; }
        public float TimeLeft { get; private set; }
        public ScoreResult LastScore { get; private set; }
        public int EyeHits { get; private set; }
        public bool PolaroidShown { get; private set; }

        Texture2D[] previews;
        float countdownLeft, revealLeft;
        int currentIndex, lastTick = -1;

        void Start()
        {
            previews = new Texture2D[references.Length];
            for (int i = 0; i < references.Length; i++)
                previews[i] = FaceArt.CreateReferencePreview(references[i], 512, face.FaceRadiusUv);
            weapons.ShotResolved += OnShot;
            weapons.Fired += OnFired;
            EnterPick();
        }

        void OnDestroy()
        {
            if (weapons != null) { weapons.ShotResolved -= OnShot; weapons.Fired -= OnFired; }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---------- состояния ----------

        public void EnterPick()
        {
            State = RoundState.Pick;
            PolaroidShown = false;
            face.Clear();
            weapons.ResetForRound();
            head.ResetPose();
            CurrentRole = Role.Model;
            ui.HideReveal();
            ui.ShowCountdown(null);
            var names = new string[Mathf.Min(3, references.Length)];
            var pics = new Texture[names.Length];
            for (int i = 0; i < names.Length; i++) { names[i] = references[i].displayName; pics[i] = previews[i]; }
            ui.ShowPick(pics, names);
            Apply();
        }

        /// <summary>Выбрать референс. asModel = игрок видел картинку и начинает Моделью.</summary>
        public void ChooseReference(int index, bool asModel)
        {
            if (index < 0 || index >= references.Length) return;
            currentIndex = index;
            Current = references[index];
            EyeHits = 0;
            shooter.SetLine(0);
            CurrentRole = asModel ? Role.Model : Role.Shooter;
            State = RoundState.Countdown;
            countdownLeft = countdownSeconds;
            ui.HidePick();
            Apply();
        }

        public void BeginShooting()
        {
            State = RoundState.Shoot;
            TimeLeft = roundSeconds;
            lastTick = -1;
            ui.ShowCountdown(null);
            ui.Banner("КРАСЬ!", 0.8f);
            Apply();
        }

        public void EndRound()
        {
            if (State != RoundState.Shoot) return;
            State = RoundState.Reveal;
            revealLeft = revealDelay;
            PolaroidShown = false;
            LastScore = Scorer.Score(face.Grid, Current, face.FaceRadiusUv);
            head.ResetPose();
            Apply();
        }

        void ShowPolaroid()
        {
            PolaroidShown = true;
            if (sfx != null) sfx.Play(SfxKind.Tada);
            string title = "Референс: " + Current.displayName + " · выстрелов: " + weapons.ShotsFired + ", в лицо: " + weapons.FaceHits
                + (EyeHits > 0 ? " · в глаз: " + EyeHits : "");
            ui.ShowReveal(previews[currentIndex], face.Texture, LastScore, title);
        }

        public void SetRole(Role role)
        {
            CurrentRole = role;
            Apply();
        }

        /// <summary>Для автотестов: сразу начать стрельбу с этим референсом, без отсчёта.</summary>
        public void DebugStart(int index)
        {
            ChooseReference(index, false);
            BeginShooting();
        }

        // Привести камеры, управление и интерфейс в соответствие с состоянием и ролью.
        void Apply()
        {
            bool shooting = State == RoundState.Shoot;
            bool reveal = State == RoundState.Reveal;
            bool isShooter = CurrentRole == Role.Shooter;

            if (shooterCamera != null) shooterCamera.enabled = !reveal && isShooter;
            if (modelCamera != null) modelCamera.enabled = !reveal && !isShooter;
            if (revealCamera != null) revealCamera.enabled = reveal;

            shooter.InputEnabled = shooting && isShooter;
            head.ModelControlled = shooting && !isShooter;
            head.SwayActive = shooting;

            Cursor.lockState = shooting ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !shooting;

            bool hud = State == RoundState.Countdown || shooting;
            ui.ShowHud(hud);
            ui.SetCrosshair(shooting && isShooter);
            ui.ShowPhone(hud && !isShooter && Current != null, Current != null ? previews[currentIndex] : null, Current != null ? Current.displayName : "");
            ui.SetRole(isShooter ? "Роль: СТРЕЛОК (Tab — стать Моделью)" : "Роль: МОДЕЛЬ (Tab — стать стрелком)");
            ui.SetHint(isShooter
                ? "ЛКМ — выстрел · ПКМ (держать) — зум · 1 — помада, 2 — базука · B — оттенок помады (красный/чёрный)\nF — линия (3 / 8 / 15 м) · H — голова качается вкл/выкл · Tab — роль · Enter — закончить раунд"
                : "Ты в кресле и не видишь своего лица. Мышь — поворот головы (±35° / ±20°).\nTab — стать стрелком · Enter — закончить раунд");
        }

        // ---------- каждый кадр ----------

        void Update()
        {
            var kb = Keyboard.current;
            switch (State)
            {
                case RoundState.Pick:
                    if (kb != null)
                    {
                        if (kb.digit1Key.wasPressedThisFrame) ChooseReference(0, true);
                        else if (kb.digit2Key.wasPressedThisFrame) ChooseReference(1, true);
                        else if (kb.digit3Key.wasPressedThisFrame) ChooseReference(2, true);
                        else if (kb.spaceKey.wasPressedThisFrame && references.Length > 0) ChooseReference(Random.Range(0, references.Length), false);
                    }
                    break;

                case RoundState.Countdown:
                    countdownLeft -= Time.deltaTime;
                    ui.SetTimer(Mathf.CeilToInt(roundSeconds), false);
                    ui.ShowCountdown(Mathf.Max(1, Mathf.CeilToInt(countdownLeft)).ToString());
                    UpdateInfo();
                    if (countdownLeft <= 0f) BeginShooting();
                    break;

                case RoundState.Shoot:
                    TimeLeft -= Time.deltaTime;
                    int sec = Mathf.Max(0, Mathf.CeilToInt(TimeLeft));
                    ui.SetTimer(sec, sec <= 10);
                    if (sec <= 10 && sec != lastTick) { lastTick = sec; if (sfx != null) sfx.Play(SfxKind.Tick); }
                    UpdateInfo();
                    if (kb != null)
                    {
                        if (kb.tabKey.wasPressedThisFrame) SetRole(CurrentRole == Role.Shooter ? Role.Model : Role.Shooter);
                        if (kb.hKey.wasPressedThisFrame) { head.idleSway = !head.idleSway; ui.Banner(head.idleSway ? "Голова качается" : "Голова замерла", 0.9f); }
                        if (kb.enterKey.wasPressedThisFrame) { EndRound(); break; }
                    }
                    if (TimeLeft <= 0f) EndRound();
                    break;

                case RoundState.Reveal:
                    if (!PolaroidShown)
                    {
                        revealLeft -= Time.deltaTime;
                        if (revealLeft <= 0f) ShowPolaroid();
                    }
                    else if (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    {
                        EnterPick();
                    }
                    break;
            }
        }

        void UpdateInfo()
        {
            WeaponDef w = weapons.Current;
            string reload = weapons.ReloadLeft > 0f ? "перезарядка " + weapons.ReloadLeft.ToString("0.0") + " с" : "готово";
            ui.SetInfo("Линия " + shooter.LineDistance.ToString("0") + " м · множитель ×" + shooter.LineMultiplier
                + "\n" + w.displayName + " · " + PaintColors.RussianName(weapons.CurrentColor)
                + "\n" + reload);
        }

        // ---------- события выстрелов ----------

        void OnFired(WeaponDef w)
        {
            if (sfx != null) sfx.Play(w.kind == WeaponKind.Projectile ? SfxKind.Pop : SfxKind.Kiss);
        }

        void OnShot(ShotResult r)
        {
            if (r.backOfHead) { ui.Banner("Затылок! Мимо лица", 0.9f); if (sfx != null) sfx.Play(SfxKind.Miss); return; }
            if (!r.hitFace) { if (sfx != null) sfx.Play(SfxKind.Miss); return; }
            if (Current == null) return;

            // «В ГЛАЗ»: краска попала в глаз, а референс этого цвета там не просил
            foreach (var eye in new[] { FaceArt.EyeLeftPos, FaceArt.EyeRightPos })
            {
                float dx = (r.uv.x - eye.x) / FaceArt.EyeRadius.x, dy = (r.uv.y - eye.y) / FaceArt.EyeRadius.y;
                if (dx * dx + dy * dy > 1f) continue;
                bool wanted = false;
                foreach (var z in Current.zones) if (z.Contains(eye) && z.color == r.color) wanted = true;
                if (!wanted) { EyeHits++; ui.Banner("В ГЛАЗ!", 1.1f); }
            }
        }
    }
}
