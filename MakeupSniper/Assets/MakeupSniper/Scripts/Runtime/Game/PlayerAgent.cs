using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Игрок в сети: человечек-«яйцо» в фартуке своего цвета. Хранит имя, номер, монеты.
    /// Всё, что игрок делает (выстрел, выбор образа, уворот), отправляется хосту, а хост решает.
    /// </summary>
    public class PlayerAgent : NetworkBehaviour
    {
        public static readonly List<PlayerAgent> All = new List<PlayerAgent>();
        public static PlayerAgent Local { get; private set; }
        public const string NamePref = "ms_name";

        [Header("Части тела")]
        public Transform cameraAnchor;
        public Transform weaponHolder;
        public GameObject[] weaponModels = new GameObject[0];
        [Tooltip("Красятся в цвет игрока (фартук)")]
        public Renderer[] tintRenderers = new Renderer[0];
        [Tooltip("Прячутся, когда игрок сидит в кресле Модели")]
        public Renderer[] bodyRenderers = new Renderer[0];
        public TextMesh nameLabel;
        [Tooltip("Розовая клякса на лице, когда игрока забрызгали")]
        public GameObject splatMark;

        readonly SyncVar<byte> _slot = new SyncVar<byte>();
        readonly SyncVar<string> _name = new SyncVar<string>("");
        readonly SyncVar<int> _coins = new SyncVar<int>();
        readonly SyncVar<byte> _weapon = new SyncVar<byte>();
        readonly SyncVar<byte> _charges = new SyncVar<byte>();
        readonly SyncVar<uint> _splatUntil = new SyncVar<uint>();

        public byte Slot { get { return _slot.Value; } }
        public string DisplayName { get { return string.IsNullOrEmpty(_name.Value) ? "Игрок " + Slot : _name.Value; } }
        public int Coins { get { return _coins.Value; } }
        public byte Weapon { get { return _weapon.Value; } }
        public int Charges { get { return _charges.Value; } }
        public bool IsHostPlayer { get { return Owner != null && Owner.IsLocalClient && IsServerInitialized; } }

        public float SplatSecondsLeft
        {
            get
            {
                if (TimeManager == null) return 0f;
                uint now = TimeManager.Tick, end = _splatUntil.Value;
                return end > now ? (float)((end - now) * TimeManager.TickDelta) : 0f;
            }
        }

        /// <summary>Игрок сейчас сидит в кресле Модели.</summary>
        public bool IsModelNow
        {
            get
            {
                var m = Match.Instance;
                if (m == null || m.ModelSlot == 0 || m.ModelSlot != Slot) return false;
                return m.State == MatchState.Pick || m.State == MatchState.Countdown || m.State == MatchState.Shoot || m.State == MatchState.Reveal;
            }
        }

        CharacterController cc;
        bool announced;
        Color lastTint = Color.clear;
        bool lastHidden;

        /// <summary>Положение руки со стволом в покое (для отдачи).</summary>
        public Vector3 WeaponRest { get; private set; }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (weaponHolder != null) WeaponRest = weaponHolder.localPosition;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!All.Contains(this)) All.Add(this);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            All.Remove(this);
            if (Local == this) Local = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (Match.Instance != null) Match.Instance.Server_PlayerJoined(this);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (Match.Instance != null) Match.Instance.Server_PlayerLeft(this);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (cc != null) cc.enabled = IsOwner;
            if (IsOwner)
            {
                Local = this;
                string n = PlayerPrefs.GetString(NamePref, "");
                CmdSetName(string.IsNullOrEmpty(n) ? "" : n);
            }
        }

        void Update()
        {
            // цвет фартука и имя над головой
            Color tint = PlayerColors.Of(Slot);
            if (tint != lastTint)
            {
                lastTint = tint;
                foreach (var r in tintRenderers)
                    if (r != null && r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", tint);
            }
            if (nameLabel != null)
            {
                nameLabel.text = DisplayName;
                nameLabel.color = tint;
                Camera cam = Camera.main;
                if (cam != null) nameLabel.transform.rotation = Quaternion.LookRotation(nameLabel.transform.position - cam.transform.position);
                bool showName = !IsOwner;
                if (nameLabel.gameObject.activeSelf != showName) nameLabel.gameObject.SetActive(showName);
            }
            // сидящий в кресле игрок прячется (его изображает голова на кресле)
            bool hidden = IsModelNow;
            if (hidden != lastHidden)
            {
                lastHidden = hidden;
                foreach (var r in bodyRenderers) if (r != null) r.enabled = !hidden;
                if (nameLabel != null) nameLabel.GetComponent<Renderer>().enabled = !hidden;
                var col = GetComponent<Collider>();
                if (col != null && !IsOwner) col.enabled = !hidden;
            }
            for (int i = 0; i < weaponModels.Length; i++)
                if (weaponModels[i] != null && weaponModels[i].activeSelf != (i == Weapon && !hidden)) weaponModels[i].SetActive(i == Weapon && !hidden);
            if (splatMark != null && splatMark.activeSelf != (SplatSecondsLeft > 0f)) splatMark.SetActive(SplatSecondsLeft > 0f);
        }

        // ---------- команды от игрока хосту ----------

        [ServerRpc]
        void CmdSetName(string requested)
        {
            string n = (requested ?? "").Trim();
            if (n.Length > 16) n = n.Substring(0, 16);
            bool first = !announced;
            announced = true;
            _name.Value = n;
            if (first && Match.Instance != null) Match.Instance.Server_Announce(DisplayName + " заходит в лофт");
        }

        [ServerRpc]
        public void CmdSelect(byte weapon)
        {
            if (Match.Instance != null) Match.Instance.Server_Select(this, weapon);
        }

        [ServerRpc]
        public void CmdFire(byte weapon, Vector3 origin, Vector3 direction, float seenYaw, float seenPitch)
        {
            if (Match.Instance != null) Match.Instance.Server_Fire(this, weapon, origin, direction, seenYaw, seenPitch);
        }

        [ServerRpc]
        public void CmdTaboo()
        {
            if (Match.Instance != null) Match.Instance.Server_Taboo(this);
        }

        [ServerRpc]
        public void CmdPose(float yaw, float pitch)
        {
            if (Match.Instance != null) Match.Instance.Server_ModelPose(this, yaw, pitch);
        }

        [ServerRpc]
        public void CmdModelAction(byte action)
        {
            if (Match.Instance != null) Match.Instance.Server_ModelAction(this, (ModelAction)action);
        }

        [ServerRpc]
        public void CmdPick(byte option, byte bet)
        {
            if (Match.Instance != null) Match.Instance.Server_Pick(this, option, bet);
        }

        [ServerRpc]
        public void CmdTip(byte targetSlot)
        {
            if (Match.Instance != null) Match.Instance.Server_Tip(this, targetSlot);
        }

        [ServerRpc]
        public void CmdHost(byte command, byte arg)
        {
            if (Match.Instance != null) Match.Instance.Server_Host(this, (HostCommand)command, arg);
        }

        // ---------- изменения от хоста ----------

        [Server] public void Server_SetSlot(byte slot) { _slot.Value = slot; }
        [Server] public void Server_SetCoins(int coins) { _coins.Value = Mathf.Max(0, coins); }
        [Server] public void Server_SetWeapon(byte weapon) { _weapon.Value = weapon; }
        [Server] public void Server_SetCharges(byte charges) { _charges.Value = charges; }
        [Server] public void Server_SetSplattered(uint untilTick) { _splatUntil.Value = untilTick; }

        [Server]
        public void Server_ResetRound(bool isModel)
        {
            _splatUntil.Value = 0;
            _weapon.Value = 0;
            var w = World.Instance;
            byte charges = 0;
            if (w != null) foreach (var wd in w.weapons) if (wd.eraser && wd.charges > 0) charges = (byte)wd.charges;
            _charges.Value = charges;
        }

        [Server]
        public void Server_Teleport(Vector3 position, float yaw)
        {
            if (Owner == null || !Owner.IsActive) return;
            TargetTeleport(Owner, position, yaw);
        }

        [TargetRpc]
        void TargetTeleport(NetworkConnection conn, Vector3 position, float yaw)
        {
            if (cc != null) cc.enabled = false;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = IsOwner;
            if (LocalController.Instance != null) LocalController.Instance.OnTeleported(yaw);
        }
    }
}
