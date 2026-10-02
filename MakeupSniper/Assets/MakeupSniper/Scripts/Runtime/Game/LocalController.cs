using UnityEngine;
using UnityEngine.InputSystem;

namespace MakeupSniper
{
    /// <summary>
    /// Управление на этом компьютере. Стрелок: WASD, мышь, ЛКМ — выстрел, ПКМ — зум, 1–4 — ствол, T — «запретное слово».
    /// Модель: мышь — голова, ПКМ — ладонь, Пробел — уворот, E — размазать. Камера тоже здесь.
    /// </summary>
    public class LocalController : MonoBehaviour
    {
        public enum Mode { Menu, Lobby, Shooter, Model, Reveal, Summary }

        public static LocalController Instance { get; private set; }
        public const string SensPref = "ms_sens";

        public float walkSpeed = 3.6f;
        public float runSpeed = 6f;
        public float jumpSpeed = 4.2f;
        public float gravity = 16f;
        public float lookPerPixel = 0.08f;
        public float modelLookPerPixel = 0.1f;
        public float normalFov = 60f;

        public Mode CurrentMode { get; private set; }
        public bool Zoomed { get; private set; }
        public int WeaponIndex { get; private set; }
        public float SwitchLeft { get { return Mathf.Max(0f, switchReady - Time.time); } }
        public float LineZ { get { var me = PlayerAgent.Local; return me != null ? me.transform.position.z : 0f; } }

        float yaw = 180f, pitch;
        float verticalVelocity;
        float switchReady;
        readonly float[] nextFire = new float[8];
        float lastPoseSend;
        float sentYaw = 999f, sentPitch = 999f;
        float orbit;
        float kick;
        bool ownVisibleSet = true;

        public static float Sensitivity
        {
            get { return Mathf.Clamp(PlayerPrefs.GetFloat(SensPref, 1f), 0.2f, 4f); }
            set { PlayerPrefs.SetFloat(SensPref, Mathf.Clamp(value, 0.2f, 4f)); }
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public float ReloadLeft(int weapon)
        {
            return weapon >= 0 && weapon < nextFire.Length ? Mathf.Max(0f, nextFire[weapon] - Time.time) : 0f;
        }

        public void OnTeleported(float newYaw)
        {
            yaw = newYaw;
            pitch = 0f;
            verticalVelocity = 0f;
        }

        Mode Decide(Match m, PlayerAgent me)
        {
            var net = Net.NetSession.Instance;
            if (net == null || net.State != Net.NetSession.Phase.Online || m == null || me == null) return Mode.Menu;
            switch (m.State)
            {
                case MatchState.Lobby: return Mode.Lobby;
                case MatchState.Summary: return Mode.Summary;
                case MatchState.Reveal: return Mode.Reveal;
                default: return me.IsModelNow ? Mode.Model : Mode.Shooter;
            }
        }

        void Update()
        {
            var m = Match.Instance;
            var me = PlayerAgent.Local;
            var w = World.Instance;
            CurrentMode = Decide(m, me);
            var ui = GameUI.Instance;
            bool blocked = ui != null && ui.BlocksGameplay;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            bool wantLock = !blocked && (CurrentMode == Mode.Shooter || (CurrentMode == Mode.Model && m != null && m.State != MatchState.Pick));
            Cursor.lockState = wantLock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !wantLock;

            Zoomed = false;
            if (CurrentMode == Mode.Shooter && me != null) ShooterUpdate(m, me, w, kb, mouse, blocked);
            else if (CurrentMode == Mode.Model && me != null) ModelUpdate(m, me, w, kb, mouse, blocked);

            SetOwnBodyVisible(me, CurrentMode != Mode.Shooter);
            kick = Mathf.Lerp(kick, 0f, Mathf.Min(1f, Time.deltaTime * 10f));
        }

        void ShooterUpdate(Match m, PlayerAgent me, World w, Keyboard kb, Mouse mouse, bool blocked)
        {
            var cc = me.GetComponent<CharacterController>();
            float dt = Time.deltaTime;
            Vector2 move = Vector2.zero;
            bool run = false, jump = false;
            if (!blocked && kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                run = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            // зум считаем до поворота камеры, чтобы в прицеле мышь двигалась медленнее
            Zoomed = !blocked && mouse != null && mouse.rightButton.isPressed && w != null
                     && WeaponIndex < w.weapons.Length && w.weapons[WeaponIndex].canZoom;
            if (!blocked && mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue() * lookPerPixel * Sensitivity * (Zoomed ? 0.3f : 1f);
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
            }
            me.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null && cc.enabled)
            {
                Vector3 dir = me.transform.right * move.x + me.transform.forward * move.y;
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                Vector3 velocity = dir * (run ? runSpeed : walkSpeed);
                if (cc.isGrounded)
                {
                    verticalVelocity = -1f;
                    if (jump) verticalVelocity = jumpSpeed;
                }
                else verticalVelocity -= gravity * dt;
                velocity.y = verticalVelocity;
                Vector3 step = velocity * dt;
                // ближе линии 3 м подходить нельзя, за стены — тоже
                if (w != null)
                {
                    Vector3 next = me.transform.position + step;
                    next.z = Mathf.Clamp(next.z, w.closestLine, w.farthestLine);
                    next.x = Mathf.Clamp(next.x, -w.roomHalfWidth, w.roomHalfWidth);
                    step.x = next.x - me.transform.position.x;
                    step.z = next.z - me.transform.position.z;
                }
                cc.Move(step);
            }

            if (w == null || w.weapons.Length == 0 || blocked) return;
            // выбор ствола
            int want = WeaponIndex;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) want = 0;
                if (kb.digit2Key.wasPressedThisFrame) want = 1;
                if (kb.digit3Key.wasPressedThisFrame) want = 2;
                if (kb.digit4Key.wasPressedThisFrame) want = 3;
            }
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) want = (WeaponIndex + w.weapons.Length - 1) % w.weapons.Length;
                else if (scroll < -0.1f) want = (WeaponIndex + 1) % w.weapons.Length;
            }
            want = Mathf.Clamp(want, 0, w.weapons.Length - 1);
            if (want != WeaponIndex)
            {
                WeaponIndex = want;
                switchReady = Time.time + Match.SwitchSeconds;
                me.CmdSelect((byte)want);
            }
            WeaponDef wd = w.weapons[WeaponIndex];
            Zoomed = wd.canZoom && mouse != null && mouse.rightButton.isPressed;

            if (kb != null && kb.tKey.wasPressedThisFrame && m != null && m.State == MatchState.Shoot) me.CmdTaboo();

            bool canShoot = m != null && m.State == MatchState.Shoot && me.SplatSecondsLeft <= 0f && Time.time >= switchReady;
            if (wd.eraser && wd.charges > 0 && me.Charges <= 0) canShoot = false;
            if (canShoot && mouse != null && mouse.leftButton.wasPressedThisFrame && Time.time >= nextFire[WeaponIndex])
                Fire(me, w, wd);
        }

        void Fire(PlayerAgent me, World w, WeaponDef wd)
        {
            Camera cam = w.mainCamera;
            if (cam == null) return;
            nextFire[WeaponIndex] = Time.time + wd.reload;
            float spread = Zoomed ? wd.spreadZoomDeg : wd.spreadDeg;
            Vector2 off = Random.insideUnitCircle * spread;
            Vector3 dir = cam.transform.rotation * Quaternion.Euler(off.y, off.x, 0f) * Vector3.forward;
            HeadRig head = w.Head(0);
            me.CmdFire((byte)WeaponIndex, cam.transform.position, dir, head != null ? head.Yaw : 0f, head != null ? head.Pitch : 0f);
            kick = 0.08f;
        }

        void ModelUpdate(Match m, PlayerAgent me, World w, Keyboard kb, Mouse mouse, bool blocked)
        {
            HeadRig head = w != null ? w.Head(0) : null;
            if (head == null || m == null) return;
            bool active = m.State == MatchState.Countdown || m.State == MatchState.Shoot;
            if (active && !blocked && mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue() * modelLookPerPixel * Sensitivity;
                head.AddLook(d);
            }
            if (Time.time - lastPoseSend > 0.066f && (Mathf.Abs(head.TargetYaw - sentYaw) > 0.2f || Mathf.Abs(head.TargetPitch - sentPitch) > 0.2f))
            {
                lastPoseSend = Time.time;
                sentYaw = head.TargetYaw;
                sentPitch = head.TargetPitch;
                me.CmdPose(sentYaw, sentPitch);
            }
            if (m.State != MatchState.Shoot || blocked) return;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) me.CmdModelAction((byte)ModelAction.Palm);
            if (kb != null && kb.spaceKey.wasPressedThisFrame) me.CmdModelAction((byte)ModelAction.Dodge);
            if (kb != null && kb.eKey.wasPressedThisFrame) me.CmdModelAction((byte)ModelAction.Smear);
        }

        /// <summary>Стрелки, которым Модель может дать чаевые (по порядку номеров).</summary>
        public static System.Collections.Generic.List<PlayerAgent> ShootersForTips(Match m)
        {
            var list = new System.Collections.Generic.List<PlayerAgent>();
            foreach (var a in PlayerAgent.All) if (a != null && a.Slot != 0 && a.Slot != m.ModelSlot) list.Add(a);
            list.Sort((x, y) => x.Slot.CompareTo(y.Slot));
            return list;
        }

        void SetOwnBodyVisible(PlayerAgent me, bool visible)
        {
            if (me == null || ownVisibleSet == visible) return;
            ownVisibleSet = visible;
            foreach (var r in me.bodyRenderers)
                if (r != null) r.shadowCastingMode = visible ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        void LateUpdate()
        {
            var w = World.Instance;
            if (w == null || w.mainCamera == null) return;
            Camera cam = w.mainCamera;
            var me = PlayerAgent.Local;
            var m = Match.Instance;
            float targetFov = normalFov;
            switch (CurrentMode)
            {
                case Mode.Shooter:
                    if (me != null && me.cameraAnchor != null)
                    {
                        cam.transform.position = me.cameraAnchor.position;
                        cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                        if (me.weaponHolder != null) me.weaponHolder.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                        if (me.weaponHolder != null) me.weaponHolder.localPosition = me.WeaponRest + new Vector3(0f, 0f, -kick);
                        if (Zoomed && w.weapons.Length > WeaponIndex) targetFov = w.weapons[WeaponIndex].zoomFov;
                    }
                    break;
                case Mode.Model:
                    if (w.modelEye != null)
                    {
                        cam.transform.position = w.modelEye.position;
                        cam.transform.rotation = w.modelEye.rotation;
                    }
                    targetFov = 70f;
                    break;
                case Mode.Reveal:
                {
                    bool wide = m != null && m.Location != null && m.Location.gimmick == Gimmick.Wedding;
                    Transform pose = wide && w.revealPoseWide != null ? w.revealPoseWide : w.revealPose;
                    if (pose != null)
                    {
                        cam.transform.position = Vector3.Lerp(cam.transform.position, pose.position, Mathf.Min(1f, Time.deltaTime * 3f));
                        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, pose.rotation, Mathf.Min(1f, Time.deltaTime * 3f));
                    }
                    targetFov = 40f;
                    break;
                }
                default:
                {
                    orbit += Time.deltaTime * 6f;
                    Vector3 pivot = w.menuPivot != null ? w.menuPivot.position : new Vector3(0f, 1.3f, 0f);
                    Vector3 offset = Quaternion.Euler(0f, 180f + 25f * Mathf.Sin(orbit * Mathf.Deg2Rad * 3f), 0f) * new Vector3(0f, 1.1f, -6.5f);
                    cam.transform.position = pivot + offset;
                    cam.transform.rotation = Quaternion.LookRotation(pivot - cam.transform.position);
                    break;
                }
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Mathf.Min(1f, Time.deltaTime * 12f));
        }
    }
}
