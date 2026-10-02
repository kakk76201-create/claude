using System;
using System.Collections.Generic;
using System.Text;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Матч целиком. Решает всё хост (правило GDD «хост-авторитет»): кто Модель, когда стрелять,
    /// куда попал выстрел, какой счёт. Остальным он рассылает мазки краски и события, а они только показывают.
    /// </summary>
    public class Match : NetworkBehaviour
    {
        public static Match Instance { get; private set; }
        /// <summary>Подробный журнал выстрелов (в автотестах и при запуске без окна).</summary>
        public static bool Verbose;

        static void Trace(string text)
        {
            if (Verbose) Debug.Log("[Match] " + text);
        }

        public const float PickSeconds = 25f;
        public const float CountdownSeconds = 3f;
        public const float RevealSeconds = 16f;
        public const float TipsSeconds = 12f;
        public const float MirrorSeconds = 3f;
        public const float SplatSeconds = 6f;
        public const float PalmSeconds = 1.6f;
        public const float DodgeSeconds = 0.45f;
        public const float DodgeCooldown = 4f;
        public const float SwitchSeconds = 0.7f;
        public const int PalmCharges = 3;
        public const int SmearCharges = 3;
        public const int TipsPool = 100;
        public const int TipStep = 10;
        public const int OneShotBonus = 50;
        public const int EyePenalty = 10;
        public const int EarPenalty = 5;
        public const int KidPenalty = 5;
        public const int PerfectBonus = 30;
        public const int BetBonus = 20;
        public const float TabooPenaltySeconds = 5f;
        public const float KidPenaltySeconds = 5f;
        public const int SoloRounds = 3;

        // ---------- синхронизируемое состояние ----------
        readonly SyncVar<byte> _state = new SyncVar<byte>();
        readonly SyncVar<uint> _stateEndTick = new SyncVar<uint>();
        readonly SyncVar<uint> _roundStartTick = new SyncVar<uint>();
        readonly SyncVar<float> _roundSeconds = new SyncVar<float>(60f);
        readonly SyncVar<byte> _round = new SyncVar<byte>();
        readonly SyncVar<byte> _roundsTotal = new SyncVar<byte>();
        readonly SyncVar<byte> _roundsPerPlayer = new SyncVar<byte>(1);
        readonly SyncVar<byte> _location = new SyncVar<byte>();
        readonly SyncVar<int> _seed = new SyncVar<int>();
        readonly SyncVar<byte> _modelSlot = new SyncVar<byte>();
        readonly SyncVar<float> _headYaw = new SyncVar<float>(new SyncTypeSettings(0.05f));
        readonly SyncVar<float> _headPitch = new SyncVar<float>(new SyncTypeSettings(0.05f));
        readonly SyncVar<bool> _palmUp = new SyncVar<bool>();
        readonly SyncVar<byte> _palmColor = new SyncVar<byte>();
        readonly SyncVar<byte> _palmLeft = new SyncVar<byte>();
        readonly SyncVar<byte> _smearLeft = new SyncVar<byte>();
        readonly SyncVar<uint> _dodgeReadyTick = new SyncVar<uint>();
        readonly SyncVar<byte> _dodgeSerial = new SyncVar<byte>();
        readonly SyncVar<float> _suspicion = new SyncVar<float>(new SyncTypeSettings(0.1f));
        readonly SyncVar<bool> _mirror = new SyncVar<bool>();
        readonly SyncVar<int> _tipsLeft = new SyncVar<int>();
        readonly SyncVar<int> _bet = new SyncVar<int>(50);
        readonly SyncList<byte> _stars = new SyncList<byte>();

        public MatchState State { get { return (MatchState)_state.Value; } }
        public byte ModelSlot { get { return _modelSlot.Value; } }
        public bool BotModel { get { return _modelSlot.Value == 0; } }
        public int Round { get { return _round.Value; } }
        public int RoundsTotal { get { return _roundsTotal.Value; } }
        public int RoundsPerPlayer { get { return _roundsPerPlayer.Value; } }
        public int LocationIndex { get { return _location.Value; } }
        public int Seed { get { return _seed.Value; } }
        public float RoundSeconds { get { return _roundSeconds.Value; } }
        public bool PalmUp { get { return _palmUp.Value; } }
        public int PalmLeft { get { return _palmLeft.Value; } }
        public int SmearLeft { get { return _smearLeft.Value; } }
        public float Suspicion { get { return _suspicion.Value; } }
        public bool MirrorOpen { get { return _mirror.Value; } }
        public int TipsLeft { get { return _tipsLeft.Value; } }
        public int Bet { get { return _bet.Value; } }
        public int StarsAt(int location) { return location >= 0 && location < _stars.Count ? _stars[location] : 0; }
        public bool IsUnlocked(int location) { return location <= 0 || StarsAt(location - 1) >= Progression.UnlockStars; }
        public LocationDef Location { get { var w = World.Instance; return w != null && LocationIndex < w.locations.Length ? w.locations[LocationIndex] : null; } }

        public float SecondsLeft
        {
            get
            {
                if (TimeManager == null) return 0f;
                uint now = TimeManager.Tick, end = _stateEndTick.Value;
                return end > now ? (float)((end - now) * TimeManager.TickDelta) : 0f;
            }
        }

        public float RoundTime
        {
            get
            {
                if (TimeManager == null) return 0f;
                uint now = TimeManager.Tick, start = _roundStartTick.Value;
                return now > start ? (float)((now - start) * TimeManager.TickDelta) : 0f;
            }
        }

        public bool DodgeReady { get { return TimeManager != null && TimeManager.Tick >= _dodgeReadyTick.Value; } }

        // ---------- события для интерфейса (на клиентах) ----------
        public event Action<string, byte, float> BannerShown;
        public event Action<string> FeedAdded;
        public event Action<RevealPacket> Revealed;
        public event Action<SummaryPacket> Summarized;
        public event Action PickOffered;
        public event Action<float, byte> VisionGranted;

        /// <summary>Варианты образов для Модели (только у Модели).</summary>
        public byte[] PickOptions { get; private set; } = new byte[0];
        /// <summary>Выбранный образ: Модель знает с начала раунда, остальные — после раскрытия.</summary>
        public int KnownOption { get; private set; } = -1;
        public RevealPacket LastReveal { get; private set; }
        public bool HasReveal { get; private set; }
        public SummaryPacket LastSummary { get; private set; }
        public bool HasSummary { get; private set; }

        // ---------- только на сервере ----------
        class SlotStats
        {
            public float[] nextFire = new float[8];
            public float switchReady;
            public byte weapon;
            public int eraserLeft;
            public int eye, ear, kid, oneShot;
            public float splattedUntil;
            public int roundPoints;
            public int tipsGot;
            public int shooterTotal;
            public int bestModel = -1, worstModel = 101;
            public readonly HashSet<string> oneShotZones = new HashSet<string>();
        }

        class Proj
        {
            public int id;
            public Vector3 pos, vel;
            public WeaponDef weapon;
            public PaintColor color;
            public byte owner, mult;
            public float life;
            public PlayerAgent shooter;
        }

        struct PoseSample { public float time, yaw, pitch; }

        readonly Dictionary<byte, SlotStats> stats = new Dictionary<byte, SlotStats>();
        readonly List<byte> order = new List<byte>();
        readonly List<StampData> roundStamps = new List<StampData>();
        readonly List<Proj> projectiles = new List<Proj>();
        readonly List<PoseSample> poses = new List<PoseSample>();
        readonly List<RoundRecord> records = new List<RoundRecord>();
        readonly HashSet<int> usedOptions = new HashSet<int>(); // ключ: место * 100 + вариант
        readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        readonly System.Random rng = new System.Random();
        Progression progression;
        byte[] serverOptions = new byte[0];
        int serverOption = -1;
        int nextProjectileId = 1;
        bool mirrorDone;
        uint mirrorEndTick;
        uint palmDownTick;
        uint dodgeUntilTick;
        float lastTabooTime = -10f;
        float lastPoseSend;
        bool tipsClosed;
        bool roundEnded;

        // ================= жизненный цикл =================

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            Instance = this;
            if (Application.isBatchMode) Verbose = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (Instance == this) Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            progression = Progression.Load();
            SyncStars();
            _state.Value = (byte)MatchState.Lobby;
            _location.Value = 0;
            _roundsPerPlayer.Value = 1;
            foreach (var a in PlayerAgent.All) if (a.IsSpawned && a.Slot == 0) Server_PlayerJoined(a);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            var w = World.Instance;
            if (w != null) w.ShowLocation(_location.Value);
        }

        /// <summary>Новый игрок увидел матч: досылаем ему лицо текущего раунда.</summary>
        public override void OnSpawnServer(NetworkConnection connection)
        {
            base.OnSpawnServer(connection);
            if (connection == null || connection.IsLocalClient) return;
            if (State == MatchState.Lobby) return;
            TargetRoundState(connection, _location.Value, roundStamps.ToArray());
        }

        void SyncStars()
        {
            var w = World.Instance;
            int n = w != null ? w.locations.Length : 4;
            _stars.Clear();
            for (int i = 0; i < n; i++) _stars.Add((byte)Mathf.Min(255, progression.StarsAt(i)));
        }

        uint Now { get { return TimeManager.Tick; } }
        uint Ticks(float seconds) { return TimeManager.TimeToTicks(Mathf.Max(0f, seconds), TickRounding.RoundUp); }
        float ServerTime { get { return Time.time; } }

        SlotStats Stats(byte slot)
        {
            SlotStats s;
            if (!stats.TryGetValue(slot, out s)) { s = new SlotStats(); stats[slot] = s; }
            return s;
        }

        // ================= игроки =================

        [Server]
        public void Server_PlayerJoined(PlayerAgent agent)
        {
            if (agent.Slot != 0) return;
            var used = new HashSet<byte>();
            foreach (var a in PlayerAgent.All) if (a != agent) used.Add(a.Slot);
            byte slot = 1;
            while (used.Contains(slot) && slot < 8) slot++;
            agent.Server_SetSlot(slot);
            Stats(slot);
            if (State != MatchState.Lobby && !order.Contains(slot)) order.Add(slot);
        }

        [Server]
        public void Server_Announce(string text)
        {
            ServerFeed(text);
        }

        [Server]
        public void Server_PlayerLeft(PlayerAgent agent)
        {
            byte slot = agent.Slot;
            if (slot == 0) return;
            ServerFeed(agent.DisplayName + " уходит");
            order.Remove(slot);
            if (State == MatchState.Lobby || State == MatchState.Summary) return;
            if (slot == _modelSlot.Value && (State == MatchState.Pick || State == MatchState.Countdown || State == MatchState.Shoot))
            {
                ServerBanner("Модель сбежала! Раунд отменён", 1, 2.5f);
                NextRoundOrSummary();
            }
        }

        static List<PlayerAgent> Players()
        {
            var list = new List<PlayerAgent>();
            foreach (var a in PlayerAgent.All) if (a != null && a.IsSpawned && a.Slot != 0) list.Add(a);
            list.Sort((x, y) => x.Slot.CompareTo(y.Slot));
            return list;
        }

        public static PlayerAgent BySlot(byte slot)
        {
            foreach (var a in PlayerAgent.All) if (a != null && a.Slot == slot) return a;
            return null;
        }

        public static string NameOf(byte slot)
        {
            if (slot == 0) return "Бот-Модель";
            if (slot == FaceGrid.ModelOwner) return "Модель";
            var a = BySlot(slot);
            return a != null ? a.DisplayName : PlayerColors.NameOf(slot);
        }

        // ================= команды хоста =================

        [Server]
        public void Server_Host(PlayerAgent caller, HostCommand cmd, byte arg)
        {
            if (caller == null || caller.Owner == null || !caller.Owner.IsLocalClient) return; // только хост
            var w = World.Instance;
            switch (cmd)
            {
                case HostCommand.SetLocation:
                    if (State != MatchState.Lobby && State != MatchState.Summary) return;
                    if (w == null || arg >= w.locations.Length) return;
                    if (!IsUnlocked(arg)) { ServerBanner("Это место ещё закрыто: нужно " + Progression.UnlockStars + " ★ в предыдущем", 1, 2f); return; }
                    _location.Value = arg;
                    RpcShowLocation(arg);
                    break;
                case HostCommand.SetRounds:
                    if (State != MatchState.Lobby && State != MatchState.Summary) return;
                    _roundsPerPlayer.Value = (byte)Mathf.Clamp(arg, 1, 3);
                    break;
                case HostCommand.Start:
                    if (State != MatchState.Lobby && State != MatchState.Summary) return;
                    StartMatch();
                    break;
                case HostCommand.Skip:
                    if (State == MatchState.Reveal) { CloseTips(); NextRoundOrSummary(); }
                    else if (State == MatchState.Shoot) EndRound(false);
                    else if (State == MatchState.Pick) Server_Pick(BySlot(_modelSlot.Value), 0, (byte)_bet.Value, true);
                    break;
                case HostCommand.ToLobby:
                    ToLobby();
                    break;
            }
        }

        void ToLobby()
        {
            _state.Value = (byte)MatchState.Lobby;
            _mirror.Value = false;
            projectiles.Clear();
            RpcClearWorld(_location.Value);
            ClearWorldLocal(_location.Value);
            foreach (var p in Players()) p.Server_Teleport(SpawnFor(p.Slot), 180f);
        }

        void StartMatch()
        {
            var players = Players();
            if (players.Count == 0) return;
            order.Clear();
            foreach (var p in players) order.Add(p.Slot);
            records.Clear();
            usedOptions.Clear();
            foreach (var p in players) p.Server_SetCoins(0);
            stats.Clear();
            foreach (var p in players) Stats(p.Slot);
            bool solo = players.Count == 1;
            _roundsTotal.Value = (byte)(solo ? SoloRounds : Mathf.Clamp(players.Count * _roundsPerPlayer.Value, 1, 12));
            HasSummary = false;
            BeginRound(0);
        }

        // ================= раунд =================

        void BeginRound(int index)
        {
            var w = World.Instance;
            LocationDef loc = Location;
            if (w == null || loc == null || loc.pool.Length == 0) { Debug.LogError("[MakeupSniper] Нет референсов для места"); return; }

            _round.Value = (byte)index;
            var players = Players();
            bool solo = players.Count <= 1;
            byte model = 0;
            if (!solo && order.Count > 0) model = order[index % order.Count];
            _modelSlot.Value = model;
            _seed.Value = rng.Next(1, int.MaxValue);
            _roundSeconds.Value = loc.seconds;
            _suspicion.Value = 0f;
            _mirror.Value = false;
            _palmUp.Value = false;
            _palmColor.Value = 0;
            _palmLeft.Value = PalmCharges;
            _smearLeft.Value = SmearCharges;
            _dodgeReadyTick.Value = 0;
            _tipsLeft.Value = TipsPool;
            _bet.Value = 50;
            mirrorDone = false;
            roundEnded = false;
            tipsClosed = false;
            serverOption = -1;
            projectiles.Clear();
            poses.Clear();
            roundStamps.Clear();
            foreach (var kv in stats)
            {
                SlotStats s = kv.Value;
                Array.Clear(s.nextFire, 0, s.nextFire.Length);
                s.switchReady = 0f; s.weapon = 0;
                s.eye = s.ear = s.kid = s.oneShot = 0;
                s.splattedUntil = 0f; s.roundPoints = 0; s.tipsGot = 0;
                s.oneShotZones.Clear();
                s.eraserLeft = 0;
                foreach (var wd in w.weapons) if (wd.eraser && wd.charges > 0) s.eraserLeft = wd.charges;
            }

            // три варианта образа на выбор (или все, если меньше)
            var all = new List<byte>();
            for (byte i = 0; i < loc.pool.Length; i++) all.Add(i);
            for (int i = all.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); byte t = all[i]; all[i] = all[j]; all[j] = t; }
            // сначала образы, которых в этот вечер ещё не было
            int locKey = _location.Value * 100;
            var fresh = all.FindAll(o => !usedOptions.Contains(locKey + o));
            if (fresh.Count == 0) { foreach (var o in all) usedOptions.Remove(locKey + o); fresh = new List<byte>(all); }
            var stale = all.FindAll(o => !fresh.Contains(o));
            all = fresh; all.AddRange(stale);
            int count = Mathf.Min(3, all.Count);
            serverOptions = all.GetRange(0, count).ToArray();

            ClearWorldLocal(_location.Value);
            RpcClearWorld(_location.Value);
            RpcRoundBegin(_round.Value, model);

            foreach (var p in players)
            {
                p.Server_ResetRound(p.Slot == model);
                if (p.Slot != model) p.Server_Teleport(SpawnFor(p.Slot), 180f);
            }

            var head = w.Head(0);
            if (head != null) { head.ResetPose(); head.source = model == 0 ? HeadRig.Source.Bot : (IsOwnerLocal(model) ? HeadRig.Source.Local : HeadRig.Source.Remote); }

            _state.Value = (byte)MatchState.Pick;
            _stateEndTick.Value = Now + Ticks(PickSeconds);
            if (model == 0)
            {
                Server_Pick(null, (byte)rng.Next(count), 50, true);
            }
            else
            {
                var modelAgent = BySlot(model);
                if (modelAgent != null) TargetPick(modelAgent.Owner, serverOptions, _location.Value);
                ServerFeed(NameOf(model) + " садится в кресло");
            }
        }

        bool IsOwnerLocal(byte slot)
        {
            var a = BySlot(slot);
            return a != null && a.Owner != null && a.Owner.IsLocalClient;
        }

        Vector3 SpawnFor(byte slot)
        {
            var w = World.Instance;
            if (w == null || w.shooterSpawns.Length == 0) return new Vector3(0f, 0f, 3.2f);
            return w.shooterSpawns[(slot - 1 + w.shooterSpawns.Length) % w.shooterSpawns.Length].position;
        }

        [Server]
        public void Server_Pick(PlayerAgent agent, byte optionIndex, byte bet, bool force = false)
        {
            if (State != MatchState.Pick) return;
            if (!force && (agent == null || agent.Slot != _modelSlot.Value)) return;
            if (serverOptions.Length == 0) return;
            int idx = Mathf.Clamp(optionIndex, 0, serverOptions.Length - 1);
            serverOption = serverOptions[idx];
            usedOptions.Add(_location.Value * 100 + serverOption);
            _bet.Value = Mathf.Clamp(bet, 0, 100);
            var modelAgent = BySlot(_modelSlot.Value);
            if (modelAgent != null) TargetPicked(modelAgent.Owner, (byte)serverOption);
            LocationDef pickedLoc = Location;
            if (pickedLoc != null) ApplyStartDirt(pickedLoc.pool[serverOption]);
            _state.Value = (byte)MatchState.Countdown;
            _stateEndTick.Value = Now + Ticks(CountdownSeconds);
        }

        /// <summary>Грязь, которая уже есть на лице в начале раунда (вино на губах, засос). Её стирают тональником.</summary>
        void ApplyStartDirt(ReferenceSet set)
        {
            var w = World.Instance;
            if (w == null || set == null) return;
            for (int f = 0; f < set.faces.Length; f++)
            {
                ReferenceData r = set.faces[f];
                PaintSurface face = w.Face(f);
                if (r == null || face == null || r.startDirt == null) continue;
                foreach (var d in r.startDirt)
                {
                    BrushKind b = d.style == ZoneStyle.Kiss ? BrushKind.Kiss : (d.style == ZoneStyle.Soft ? BrushKind.Blush : BrushKind.Smudge);
                    float radius = Mathf.Min(d.radius.x, d.radius.y) * face.faceSize;
                    ApplyStamp(new StampData
                    {
                        face = (byte)f, u = d.center.x, v = d.center.y, radius = radius, color = (byte)d.color,
                        brush = (byte)b, angle = d.style == ZoneStyle.Kiss ? 20f : 0f, owner = 0, mult = 1
                    });
                }
            }
        }

        void StartShooting()
        {
            _state.Value = (byte)MatchState.Shoot;
            _roundStartTick.Value = Now;
            _stateEndTick.Value = Now + Ticks(_roundSeconds.Value);
            ServerBanner("КРАСЬ!", 0, 0.9f);
        }

        void EndRound(bool noticed)
        {
            if (roundEnded) return;
            roundEnded = true;
            _mirror.Value = false;
            _palmUp.Value = false;
            projectiles.Clear();
            var w = World.Instance;
            LocationDef loc = Location;
            ReferenceSet set = loc != null && serverOption >= 0 && serverOption < loc.pool.Length ? loc.pool[serverOption] : null;
            if (set == null) { NextRoundOrSummary(); return; }

            var packet = new RevealPacket();
            packet.location = _location.Value;
            packet.option = (byte)serverOption;
            packet.modelSlot = _modelSlot.Value;
            packet.modelName = NameOf(_modelSlot.Value);
            packet.noticed = noticed;
            packet.bet = _bet.Value;

            var zoneLines = new List<ZoneLine>();
            var faceMatch = new List<int>();
            var zonePoints = new Dictionary<byte, float>();
            var overshoot = new Dictionary<byte, int>();
            for (int f = 0; f < set.faces.Length; f++)
            {
                PaintSurface face = w.Face(f);
                if (face == null || set.faces[f] == null) continue;
                ScoreResult r = Scorer.Score(face.Grid, set.faces[f], face.FaceRadiusUv);
                faceMatch.Add(r.matchPercent);
                foreach (var z in r.zones)
                    zoneLines.Add(new ZoneLine { face = (byte)f, name = z.name, color = (byte)z.color, coverage = (byte)Mathf.RoundToInt(z.coverage * 100f), ok = z.ok, blame = z.blame });
                foreach (var kv in r.zonePoints) { float v; zonePoints.TryGetValue(kv.Key, out v); zonePoints[kv.Key] = v + kv.Value; }
                foreach (var kv in r.overshootByOwner) { int v; overshoot.TryGetValue(kv.Key, out v); overshoot[kv.Key] = v + kv.Value; }
            }
            int match = 0;
            foreach (int m in faceMatch) match += m;
            match = faceMatch.Count > 0 ? Mathf.RoundToInt((float)match / faceMatch.Count) : 0;
            if (noticed) match = Mathf.Max(0, match - 30);
            packet.match = match;
            packet.faceMatch = faceMatch.ToArray();
            packet.zones = zoneLines.ToArray();
            packet.perfect = match >= 80;
            packet.stars = (byte)Scorer.Stars(match);
            packet.betWon = _modelSlot.Value != 0 && Mathf.Abs(_bet.Value - match) <= 10;

            // очки стрелков
            var players = Players();
            var lines = new List<PlayerLine>();
            foreach (var p in players)
            {
                SlotStats s = Stats(p.Slot);
                var note = new StringBuilder();
                int pts = 0;
                if (p.Slot == _modelSlot.Value)
                {
                    pts = match + (packet.betWon ? BetBonus : 0) + (packet.perfect ? PerfectBonus : 0);
                    note.Append("Модель: ").Append(match).Append("%");
                    if (packet.betWon) note.Append(", ставка сыграла +").Append(BetBonus);
                    if (s.bestModel < match) s.bestModel = match;
                    if (s.worstModel > match) s.worstModel = match;
                    packet.modelCoins = pts;
                }
                else
                {
                    float zp; zonePoints.TryGetValue(p.Slot, out zp);
                    int over; overshoot.TryGetValue(p.Slot, out over);
                    pts = Mathf.RoundToInt(zp) + s.oneShot * OneShotBonus - s.eye * EyePenalty - s.ear * EarPenalty - s.kid * KidPenalty - over / 5;
                    if (packet.perfect) pts += PerfectBonus;
                    pts = Mathf.Max(0, pts);
                    note.Append("зоны ").Append(Mathf.RoundToInt(zp));
                    if (s.oneShot > 0) note.Append(", одним выстрелом +").Append(s.oneShot * OneShotBonus);
                    if (s.eye > 0) note.Append(", в глаз −").Append(s.eye * EyePenalty);
                    if (s.ear > 0) note.Append(", в ухо −").Append(s.ear * EarPenalty);
                    if (s.kid > 0) note.Append(", в детей −").Append(s.kid * KidPenalty);
                    if (over >= 5) note.Append(", залёт −").Append(over / 5);
                    if (packet.perfect) note.Append(", ИДЕАЛЬНО +").Append(PerfectBonus);
                    s.shooterTotal += pts;
                }
                s.roundPoints = pts;
                p.Server_SetCoins(p.Coins + pts);
                lines.Add(new PlayerLine { slot = p.Slot, name = p.DisplayName, points = pts, coinsTotal = p.Coins, note = note.ToString() });
            }
            packet.players = lines.ToArray();

            // заголовок
            string title = set.Title;
            if (noticed) packet.headline = "ЗАМЕТИЛА! Свидание сорвано";
            else if (packet.perfect) packet.headline = "ИДЕАЛЬНО! Все получают +" + PerfectBonus;
            else if (match < 15) packet.headline = "Это… смело. " + title + " не узнать";
            else if (match < 40) packet.headline = "Похоже на " + title.ToLowerInvariant() + ", если прищуриться";
            else packet.headline = title + ": " + match + "%";

            // прогресс мест (хранится у хоста)
            int unlocked = -1;
            if (progression != null && w != null)
            {
                unlocked = progression.AddStars(_location.Value, packet.stars, w.locations.Length);
                progression.Save();
                SyncStars();
            }
            packet.unlock = unlocked >= 0 && w != null ? "Открыто новое место: " + w.locations[unlocked].displayName + "!" : "";

            records.Add(new RoundRecord { modelSlot = _modelSlot.Value, modelName = packet.modelName, title = title, match = match, location = _location.Value, option = (byte)serverOption });

            _state.Value = (byte)MatchState.Reveal;
            _stateEndTick.Value = Now + Ticks(RevealSeconds);
            if (_modelSlot.Value == 0) { _tipsLeft.Value = 0; AutoTips(players); }
            RpcReveal(packet);
        }

        void AutoTips(List<PlayerAgent> players)
        {
            // бот-Модель раздаёт чаевые пропорционально очкам
            int total = 0;
            foreach (var p in players) total += Stats(p.Slot).roundPoints;
            if (total <= 0) return;
            foreach (var p in players)
            {
                int tip = Mathf.RoundToInt(TipsPool * (float)Stats(p.Slot).roundPoints / total);
                if (tip <= 0) continue;
                p.Server_SetCoins(p.Coins + tip);
                Stats(p.Slot).tipsGot += tip;
            }
            tipsClosed = true;
        }

        [Server]
        public void Server_Tip(PlayerAgent model, byte targetSlot)
        {
            if (State != MatchState.Reveal || tipsClosed || model == null || model.Slot != _modelSlot.Value) return;
            if (targetSlot == model.Slot || _tipsLeft.Value <= 0) return;
            var target = BySlot(targetSlot);
            if (target == null) return;
            int amount = Mathf.Min(TipStep, _tipsLeft.Value);
            _tipsLeft.Value -= amount;
            target.Server_SetCoins(target.Coins + amount);
            Stats(targetSlot).tipsGot += amount;
            RpcTip(targetSlot, amount, Stats(targetSlot).tipsGot);
        }

        void CloseTips()
        {
            if (tipsClosed) return;
            tipsClosed = true;
            var shooters = new List<PlayerAgent>();
            foreach (var p in Players()) if (p.Slot != _modelSlot.Value) shooters.Add(p);
            if (shooters.Count == 0 || _tipsLeft.Value <= 0) return;
            int each = _tipsLeft.Value / shooters.Count;
            foreach (var p in shooters) { p.Server_SetCoins(p.Coins + each); Stats(p.Slot).tipsGot += each; }
            _tipsLeft.Value = 0;
            if (each > 0) ServerFeed("Остаток чаевых поровну: по " + each);
        }

        void NextRoundOrSummary()
        {
            int next = _round.Value + 1;
            if (next < _roundsTotal.Value && Players().Count > 0) { BeginRound(next); return; }
            ShowSummary();
        }

        void ShowSummary()
        {
            _state.Value = (byte)MatchState.Summary;
            _mirror.Value = false;
            var players = Players();
            var lines = new List<PlayerLine>();
            string bestArtist = "—", bestFace = "—", worstFace = "—";
            int bestShooter = -1, best = -1, worst = 101;
            foreach (var p in players)
            {
                SlotStats s = Stats(p.Slot);
                lines.Add(new PlayerLine { slot = p.Slot, name = p.DisplayName, points = s.shooterTotal, coinsTotal = p.Coins, note = s.bestModel >= 0 ? "лучшее лицо " + s.bestModel + "%" : "" });
                if (s.shooterTotal > bestShooter) { bestShooter = s.shooterTotal; bestArtist = p.DisplayName + " (" + s.shooterTotal + " очков)"; }
            }
            foreach (var r in records)
            {
                if (r.match > best) { best = r.match; bestFace = r.modelName + ": " + r.title + " " + r.match + "%"; }
                if (r.match < worst) { worst = r.match; worstFace = r.modelName + ": " + r.title + " " + r.match + "%"; }
            }
            lines.Sort((a, b) => b.coinsTotal.CompareTo(a.coinsTotal));
            var packet = new SummaryPacket { players = lines.ToArray(), bestArtist = bestArtist, bestFace = bestFace, worstFace = worstFace, rounds = records.ToArray() };
            RpcSummary(packet);
        }

        // ================= каждый кадр =================

        void Update()
        {
            if (IsServerInitialized) ServerUpdate();
            if (IsClientInitialized) ClientUpdate();
        }

        void ServerUpdate()
        {
            var w = World.Instance;
            switch (State)
            {
                case MatchState.Pick:
                    if (Now >= _stateEndTick.Value) Server_Pick(null, 0, (byte)_bet.Value, true);
                    break;
                case MatchState.Countdown:
                    if (Now >= _stateEndTick.Value) StartShooting();
                    break;
                case MatchState.Shoot:
                    ServerShootUpdate(w);
                    break;
                case MatchState.Reveal:
                    if (!tipsClosed && SecondsLeft <= RevealSeconds - TipsSeconds) CloseTips();
                    if (Now >= _stateEndTick.Value) NextRoundOrSummary();
                    break;
            }
        }

        void ServerShootUpdate(World w)
        {
            float dt = Time.deltaTime;
            HeadRig head = w != null ? w.Head(0) : null;
            // голова бота: сервер сам качает и рассылает позу
            if (head != null)
            {
                if (_modelSlot.Value == 0)
                {
                    head.SwayActive = true;
                    if (Time.time - lastPoseSend > 0.066f)
                    {
                        lastPoseSend = Time.time;
                        _headYaw.Value = head.TargetYaw;
                        _headPitch.Value = head.TargetPitch;
                    }
                }
                poses.Add(new PoseSample { time = ServerTime, yaw = head.Yaw, pitch = head.Pitch });
                while (poses.Count > 0 && poses[0].time < ServerTime - 1f) poses.RemoveAt(0);
            }

            if (_palmUp.Value && Now >= palmDownTick) _palmUp.Value = false;

            // зеркальце на 35-й секунде из 60 (на 58% раунда)
            float elapsed = RoundTime;
            if (!mirrorDone && elapsed >= _roundSeconds.Value * 0.58f)
            {
                mirrorDone = true;
                _mirror.Value = true;
                mirrorEndTick = Now + Ticks(MirrorSeconds);
                if (_modelSlot.Value != 0) ServerBanner("Модель смотрит в зеркальце!", 2, 1.5f);
            }
            if (_mirror.Value && Now >= mirrorEndTick) _mirror.Value = false;

            // подозрение свидетелей медленно спадает
            if (_suspicion.Value > 0f && _suspicion.Value < 100f) _suspicion.Value = Mathf.Max(0f, _suspicion.Value - 5f * dt);

            SimulateProjectiles(dt);

            if (!roundEnded && Now >= _stateEndTick.Value) EndRound(false);
        }

        void ClientUpdate()
        {
            var w = World.Instance;
            if (w == null) return;
            bool running = State == MatchState.Shoot;
            float t = RoundTime;
            LocationDef loc = Location;
            if (loc != null)
            {
                if (loc.gimmick == Gimmick.Kids && w.kids != null) w.kids.Pose(_seed.Value, t, running);
                if ((loc.gimmick == Gimmick.Witness || loc.gimmick == Gimmick.Wedding) && w.Witnesses != null) w.Witnesses.Pose(_seed.Value, _roundSeconds.Value, t, running);
            }

            // голова Модели у всех, кроме самой Модели
            HeadRig head = w.Head(0);
            if (head != null)
            {
                bool localModel = PlayerAgent.Local != null && PlayerAgent.Local.Slot == _modelSlot.Value && _modelSlot.Value != 0
                                  && State != MatchState.Lobby && State != MatchState.Summary;
                if (localModel) head.source = HeadRig.Source.Local;
                else if (_modelSlot.Value == 0 && IsServerInitialized) head.source = HeadRig.Source.Bot;
                else
                {
                    head.source = HeadRig.Source.Remote;
                    head.SetTarget(_headYaw.Value, _headPitch.Value);
                }
                head.SwayActive = running;
                head.PalmUp = _palmUp.Value;
                head.SetPalmColor((PaintColor)_palmColor.Value);
                if (dodgeSeen != _dodgeSerial.Value) { dodgeSeen = _dodgeSerial.Value; if (_dodgeSerial.Value != 0) head.Dodge(DodgeSeconds); }
                if (State == MatchState.Reveal || State == MatchState.Summary || State == MatchState.Lobby) head.SetTarget(0f, 0f);
            }
            if (w.modelApron != null)
            {
                Color c = PlayerColors.Of(_modelSlot.Value);
                var m = w.modelApron.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            }
        }

        byte dodgeSeen;

        // ================= выстрелы =================

        [Server]
        public void Server_Select(PlayerAgent agent, byte weapon)
        {
            var w = World.Instance;
            if (agent == null || w == null || weapon >= w.weapons.Length) return;
            SlotStats s = Stats(agent.Slot);
            if (s.weapon == weapon) return;
            s.weapon = weapon;
            s.switchReady = ServerTime + SwitchSeconds - 0.15f;
            agent.Server_SetWeapon(weapon);
        }

        [Server]
        public void Server_Fire(PlayerAgent agent, byte weaponIndex, Vector3 origin, Vector3 dir, float seenYaw, float seenPitch)
        {
            var w = World.Instance;
            if (agent == null || w == null || State != MatchState.Shoot || roundEnded) { Trace("fire ignored: state " + State); return; }
            if (agent.Slot == _modelSlot.Value) { Trace("fire ignored: model"); return; }
            if (weaponIndex >= w.weapons.Length) return;
            SlotStats s = Stats(agent.Slot);
            float now = ServerTime;
            if (now < s.splattedUntil) { Trace("fire ignored: splattered"); return; }
            if (now < s.switchReady) { Trace("fire ignored: switching weapon"); return; }
            if (s.weapon != weaponIndex) { s.weapon = weaponIndex; agent.Server_SetWeapon(weaponIndex); }
            WeaponDef wd = w.weapons[weaponIndex];
            if (now < s.nextFire[weaponIndex] - 0.15f) { Trace("fire ignored: reload"); return; }
            if (wd.eraser && wd.charges > 0)
            {
                if (s.eraserLeft <= 0) return;
                s.eraserLeft--;
                agent.Server_SetCharges((byte)s.eraserLeft);
            }
            s.nextFire[weaponIndex] = now + wd.reload;

            // проверка разумности: выстрел из точки рядом с игроком
            Vector3 body = agent.transform.position + Vector3.up * 1.6f;
            if ((origin - body).sqrMagnitude > 9f) origin = body;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            byte mult = (byte)World.LineMultiplier(origin.z);

            // свидетели видят выстрел
            LocationDef loc = Location;
            if (loc != null && (loc.gimmick == Gimmick.Witness || loc.gimmick == Gimmick.Wedding) && w.Witnesses != null
                && w.Witnesses.AnyLooking(_seed.Value, _roundSeconds.Value, RoundTime))
            {
                AddSuspicion(22f, "ОНА СМОТРИТ! Не стреляй!");
            }

            PaintColor color = wd.eraser ? PaintColor.None : wd.color;
            PlayerAgent own = agent;
            Vector3 muzzle = origin + dir * 0.6f;
            if (wd.kind == WeaponKind.Hitscan)
            {
                RaycastHit hit;
                bool any = RaycastWithSeenPose(new Ray(origin, dir), 80f, own, seenYaw, seenPitch, out hit);
                Vector3 end = any ? hit.point : origin + dir * 40f;
                PresentTracer(muzzle, end, color);
                RpcTracer(muzzle, end, (byte)color);
                if (any) ResolveHit(hit, wd, color, agent.Slot, mult, dir);
                else ServerSound(SfxKind.Miss);
            }
            else
            {
                var p = new Proj { id = nextProjectileId++, pos = muzzle, vel = dir * wd.projectileSpeed, weapon = wd, color = color, owner = agent.Slot, mult = mult, life = 5f, shooter = own };
                projectiles.Add(p);
                PresentProjectile(p.id, p.pos, p.vel, 9.81f * wd.gravityScale, color, wd.projectileSize);
                RpcProjectile(p.id, p.pos, p.vel, 9.81f * wd.gravityScale, (byte)color, wd.projectileSize);
                ServerSound(SfxKind.Pop);
            }
        }

        /// <summary>
        /// Луч с позой головы, которую видел стрелок (в пределах того, что голова реально делала
        /// последние полсекунды). Спорные попадания решаются в пользу стрелка, как в GDD.
        /// </summary>
        bool RaycastWithSeenPose(Ray ray, float distance, PlayerAgent ignore, float seenYaw, float seenPitch, out RaycastHit hit)
        {
            var w = World.Instance;
            HeadRig head = w != null ? w.Head(0) : null;
            float curYaw = head != null ? head.Yaw : 0f, curPitch = head != null ? head.Pitch : 0f, curDodge = head != null ? head.DodgeAmount : 0f;
            bool rewound = false;
            if (head != null && poses.Count > 0)
            {
                float minY = float.MaxValue, maxY = float.MinValue, minP = float.MaxValue, maxP = float.MinValue;
                foreach (var ps in poses)
                {
                    if (ps.time < ServerTime - 0.6f) continue;
                    minY = Mathf.Min(minY, ps.yaw); maxY = Mathf.Max(maxY, ps.yaw);
                    minP = Mathf.Min(minP, ps.pitch); maxP = Mathf.Max(maxP, ps.pitch);
                }
                const float tol = 4f;
                if (minY <= maxY && seenYaw >= minY - tol && seenYaw <= maxY + tol && seenPitch >= minP - tol && seenPitch <= maxP + tol)
                {
                    head.ApplyInstant(seenYaw, seenPitch, curDodge);
                    rewound = true;
                }
            }
            Physics.SyncTransforms();
            bool any = RaycastIgnoring(ray, distance, ignore, out hit);
            if (rewound)
            {
                head.ApplyInstant(curYaw, curPitch, curDodge);
                Physics.SyncTransforms();
            }
            return any;
        }

        /// <summary>Ближайшее попадание, не считая самого стрелка (его капсулы и мишени).</summary>
        bool RaycastIgnoring(Ray ray, float distance, PlayerAgent ignore, out RaycastHit best)
        {
            best = default(RaycastHit);
            int n = Physics.RaycastNonAlloc(ray, hitBuffer, distance, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                if (ignore != null && hitBuffer[i].collider.GetComponentInParent<PlayerAgent>() == ignore) continue;
                if (hitBuffer[i].distance < bestDist) { bestDist = hitBuffer[i].distance; best = hitBuffer[i]; found = true; }
            }
            return found;
        }

        void SimulateProjectiles(float dt)
        {
            if (projectiles.Count == 0) return;
            Physics.SyncTransforms();
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Proj p = projectiles[i];
                p.vel.y -= 9.81f * p.weapon.gravityScale * dt;
                Vector3 next = p.pos + p.vel * dt;
                Vector3 step = next - p.pos;
                float len = step.magnitude;
                RaycastHit hit;
                if (len > 1e-5f && RaycastIgnoring(new Ray(p.pos, step / len), len + 0.02f, p.shooter, out hit))
                {
                    projectiles.RemoveAt(i);
                    PresentProjectileEnd(p.id);
                    RpcProjectileEnd(p.id);
                    ResolveHit(hit, p.weapon, p.color, p.owner, p.mult, p.vel.normalized);
                    continue;
                }
                p.pos = next;
                p.life -= dt;
                if (p.life <= 0f || p.pos.y < -2f)
                {
                    Trace("projectile " + p.weapon.shortName + " flew away at " + p.pos);
                    projectiles.RemoveAt(i);
                    PresentProjectileEnd(p.id);
                    RpcProjectileEnd(p.id);
                }
            }
        }

        void ResolveHit(RaycastHit hit, WeaponDef wd, PaintColor color, byte owner, byte mult, Vector3 dir)
        {
            var w = World.Instance;
            Collider c = hit.collider;
            Trace("hit " + c.name + " (" + (c.transform.parent != null ? c.transform.parent.name : "-") + ") by " + owner + " with " + wd.shortName + " at " + hit.point);
            PaintSurface face = c.GetComponent<PaintSurface>();
            if (face != null)
            {
                HitFace(face, hit.point, wd, color, owner, mult, dir);
                return;
            }
            if (w.palmCollider != null && c == w.palmCollider)
            {
                if (!wd.eraser) _palmColor.Value = (byte)color;
                ServerBanner("В ЛАДОНЬ! Модель закрылась", 1, 1f);
                ServerSound(SfxKind.Slap);
                SplatEverywhere(hit.point, hit.normal, wd.spotRadius * 2.2f, color);
                return;
            }
            if (w.kids != null && w.kids.gameObject.activeInHierarchy && w.kids.IsKid(c))
            {
                Stats(owner).kid++;
                _stateEndTick.Value = _stateEndTick.Value > Ticks(KidPenaltySeconds) ? _stateEndTick.Value - Ticks(KidPenaltySeconds) : Now;
                ServerBanner("В РЕБЁНКА! −" + KidPenaltySeconds + " с", 1, 1.2f);
                ServerFeed(NameOf(owner) + " попал в ребёнка");
                ServerSound(SfxKind.Buzz);
                SplatEverywhere(hit.point, hit.normal, wd.spotRadius * 2.5f, color);
                return;
            }
            if (w.Witnesses != null && w.Witnesses.gameObject.activeInHierarchy && w.Witnesses.IsWitness(c))
            {
                AddSuspicion(45f, "ЭЙ! Не в неё!");
                SplatEverywhere(hit.point, hit.normal, wd.spotRadius * 2.5f, color);
                return;
            }
            PlayerAgent victim = c.GetComponentInParent<PlayerAgent>();
            if (victim != null && victim.Slot != owner && victim.Slot != _modelSlot.Value)
            {
                if (wd.splattersPlayers && !wd.eraser) SplatterPlayer(victim, owner);
                SplatEverywhere(hit.point, hit.normal, wd.spotRadius * 2.5f, color);
                return;
            }
            SplatEverywhere(hit.point, hit.normal, wd.spotRadius * (wd.kind == WeaponKind.Projectile ? 2.6f : 2.2f), color);
            ServerSound(SfxKind.Miss);
        }

        void HitFace(PaintSurface face, Vector3 point, WeaponDef wd, PaintColor color, byte owner, byte mult, Vector3 dir)
        {
            var w = World.Instance;
            Vector2 uv;
            if (!face.TryGetUv(point, out uv))
            {
                ServerBanner("Затылок! Мимо лица", 1, 0.9f);
                ServerSound(SfxKind.Miss);
                return;
            }
            // уворот «сжигает» выстрел: заряд уходит в ухо
            if (face.faceIndex == 0 && Now < dodgeUntilTick)
            {
                Stats(owner).ear++;
                ServerBanner("УВОРОТ! В УХО", 1, 1f);
                ServerFeed(NameOf(_modelSlot.Value) + " увернулась от " + NameOf(owner));
                ServerSound(SfxKind.Swish);
                return;
            }

            BrushKind brush = wd.eraser ? BrushKind.Eraser : wd.brush;
            float angle;
            if (brush == BrushKind.Stripe) angle = face.UvAngleOf(dir);
            else if (brush == BrushKind.Kiss) angle = (float)(rng.NextDouble() * 50.0 - 25.0);
            else angle = (float)(rng.NextDouble() * 360.0);
            var s = new StampData
            {
                face = face.faceIndex, u = uv.x, v = uv.y, radius = wd.spotRadius, length = wd.stripeLength,
                color = (byte)color, brush = (byte)brush, angle = angle, owner = owner, mult = mult
            };

            var before = new FaceGrid();
            before.CopyFrom(face.Grid);
            ApplyStamp(s);
            ServerSound(wd.eraser ? SfxKind.Swish : (wd.kind == WeaponKind.Projectile ? SfxKind.Splat : SfxKind.Kiss));

            ReferenceData reference = CurrentReference(face.faceIndex);
            if (reference == null || wd.eraser) return;

            // «В ГЛАЗ»: краска в глаз, если референс там этого цвета не просил
            if (face.faceIndex == 0)
            {
                foreach (var eye in new[] { FaceArt.EyeLeftPos, FaceArt.EyeRightPos })
                {
                    float dx = (uv.x - eye.x) / FaceArt.EyeRadius.x, dy = (uv.y - eye.y) / FaceArt.EyeRadius.y;
                    if (dx * dx + dy * dy > 1f) continue;
                    bool wanted = false;
                    foreach (var z in reference.zones) if (z.Contains(eye) && z.color == color) wanted = true;
                    if (wanted) continue;
                    Stats(owner).eye++;
                    ServerBanner("В ГЛАЗ! −" + EyePenalty, 1, 1.1f);
                    ServerFeed(NameOf(owner) + " попал Модели в глаз");
                    RpcBlink();
                    break;
                }
            }

            // «одним выстрелом»: один мазок закрыл 80% зоны
            foreach (var z in reference.zones)
            {
                if (z.color != color) continue;
                string key = face.faceIndex + ":" + z.name;
                SlotStats st = Stats(owner);
                if (st.oneShotZones.Contains(key)) continue;
                if (Scorer.ZoneShareOfStamp(z, before, face.Grid) < 0.8f) continue;
                st.oneShotZones.Add(key);
                st.oneShot++;
                ServerBanner(z.name.ToUpperInvariant() + " — ОДНИМ ВЫСТРЕЛОМ! +" + OneShotBonus, 0, 1.6f);
                ServerFeed(NameOf(owner) + ": " + z.name.ToLowerInvariant() + " одним выстрелом");
            }
        }

        ReferenceData CurrentReference(int faceIndex)
        {
            LocationDef loc = Location;
            if (loc == null || serverOption < 0 || serverOption >= loc.pool.Length) return null;
            var set = loc.pool[serverOption];
            return faceIndex < set.faces.Length ? set.faces[faceIndex] : null;
        }

        void SplatterPlayer(PlayerAgent victim, byte by)
        {
            SlotStats s = Stats(victim.Slot);
            if (ServerTime < s.splattedUntil) return;
            s.splattedUntil = ServerTime + SplatSeconds;
            victim.Server_SetSplattered(Now + Ticks(SplatSeconds));
            ServerBanner("ЗАБРЫЗГАН: " + victim.DisplayName + "! Он видит референс", 2, 1.8f);
            ServerFeed(NameOf(by) + " забрызгал " + victim.DisplayName);
            ServerSound(SfxKind.Splat);
            if (victim.Owner != null && serverOption >= 0) TargetVision(victim.Owner, SplatSeconds, (byte)serverOption);
        }

        void AddSuspicion(float amount, string text)
        {
            if (roundEnded) return;
            _suspicion.Value = Mathf.Min(100f, _suspicion.Value + amount);
            ServerBanner(text, 1, 1f);
            ServerSound(SfxKind.Buzz);
            if (_suspicion.Value >= 100f)
            {
                ServerBanner(Location != null && Location.gimmick == Gimmick.Wedding ? "ГОСТИ ЗАМЕТИЛИ!" : "ЗАМЕТИЛА!", 1, 2f);
                EndRound(true);
            }
        }

        // ================= Модель =================

        [Server]
        public void Server_ModelPose(PlayerAgent agent, float yaw, float pitch)
        {
            if (agent == null || agent.Slot != _modelSlot.Value || _modelSlot.Value == 0) return;
            _headYaw.Value = Mathf.Clamp(yaw, -35f, 35f);
            _headPitch.Value = Mathf.Clamp(pitch, -20f, 20f);
            var head = World.Instance != null ? World.Instance.Head(0) : null;
            if (head != null && head.source != HeadRig.Source.Local) head.SetTarget(_headYaw.Value, _headPitch.Value);
        }

        [Server]
        public void Server_ModelAction(PlayerAgent agent, ModelAction action)
        {
            if (agent == null || agent.Slot != _modelSlot.Value || State != MatchState.Shoot || roundEnded) return;
            var w = World.Instance;
            switch (action)
            {
                case ModelAction.Palm:
                    if (_palmUp.Value || _palmLeft.Value == 0) return;
                    _palmLeft.Value--;
                    _palmUp.Value = true;
                    palmDownTick = Now + Ticks(PalmSeconds);
                    ServerSound(SfxKind.Slap);
                    break;
                case ModelAction.Dodge:
                    if (Now < _dodgeReadyTick.Value) return;
                    dodgeUntilTick = Now + Ticks(DodgeSeconds);
                    _dodgeReadyTick.Value = Now + Ticks(DodgeCooldown);
                    _dodgeSerial.Value = (byte)(_dodgeSerial.Value == 255 ? 1 : _dodgeSerial.Value + 1);
                    ServerSound(SfxKind.Swish);
                    break;
                case ModelAction.Smear:
                    if (_smearLeft.Value == 0) return;
                    Smear(w);
                    break;
            }
        }

        /// <summary>
        /// Размазать ладонью: полоса поперёк лица на высоте, куда наклонена голова.
        /// Цвет — тот, что на ладони, а если ладонь чистая — тот, что уже на лице в начале мазка.
        /// </summary>
        void Smear(World w)
        {
            PaintSurface face = w != null ? w.Face(0) : null;
            HeadRig head = w != null ? w.Head(0) : null;
            if (face == null || head == null) return;
            float pitch = head.Pitch;
            float v = pitch < -7f ? 0.74f : (pitch > 7f ? 0.32f : 0.46f);
            float tilt = Mathf.Clamp(head.Yaw * 0.3f, -12f, 12f);
            PaintColor color = (PaintColor)_palmColor.Value;
            if (color == PaintColor.None)
            {
                for (float u = 0.26f; u <= 0.74f && color == PaintColor.None; u += 1f / FaceGrid.Size)
                {
                    int cx = Mathf.Clamp(Mathf.FloorToInt(u * FaceGrid.Size), 0, FaceGrid.Size - 1);
                    for (int dy = -2; dy <= 2 && color == PaintColor.None; dy++)
                    {
                        int cy = Mathf.Clamp(Mathf.FloorToInt(v * FaceGrid.Size) + dy, 0, FaceGrid.Size - 1);
                        color = face.Grid.Colors[cy * FaceGrid.Size + cx];
                    }
                }
            }
            if (color == PaintColor.None)
            {
                ServerBanner("Ладонь чистая — размазывать нечего", 1, 1.2f);
                return;
            }
            _smearLeft.Value--;
            var s = new StampData
            {
                face = 0, u = 0.5f, v = v, radius = 0.032f, length = 0.34f, color = (byte)color, brush = (byte)BrushKind.Smear,
                angle = tilt, owner = FaceGrid.ModelOwner, mult = 1
            };
            ApplyStamp(s);
            ServerBanner("Модель размазала " + PaintColors.RussianName(color) + "!", 2, 1.3f);
            ServerSound(SfxKind.Swish);
        }

        [Server]
        public void Server_Taboo(PlayerAgent agent)
        {
            if (agent == null || State != MatchState.Shoot || roundEnded || _modelSlot.Value == 0) return;
            if (agent.Slot == _modelSlot.Value) return;
            if (ServerTime - lastTabooTime < 3f) return;
            lastTabooTime = ServerTime;
            uint pen = Ticks(TabooPenaltySeconds);
            _stateEndTick.Value = _stateEndTick.Value > Now + pen ? _stateEndTick.Value - pen : Now;
            ServerBanner("ЗАПРЕТНОЕ СЛОВО! −" + TabooPenaltySeconds + " с", 1, 1.4f);
            ServerFeed(agent.DisplayName + " поймал Модель на запретном слове");
            ServerSound(SfxKind.Buzz);
        }

        // ================= мазки, эффекты, звуки =================

        void ApplyStamp(StampData s)
        {
            roundStamps.Add(s);
            ApplyStampLocal(s);
            RpcStamp(s);
        }

        static void ApplyStampLocal(StampData s)
        {
            var w = World.Instance;
            PaintSurface face = w != null ? w.Face(s.face) : null;
            if (face != null) face.Apply(s);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcStamp(StampData s)
        {
            ApplyStampLocal(s);
        }

        [TargetRpc]
        void TargetRoundState(NetworkConnection conn, byte location, StampData[] stamps)
        {
            ClearWorldLocal(location);
            foreach (var s in stamps) ApplyStampLocal(s);
        }

        void SplatEverywhere(Vector3 point, Vector3 normal, float radius, PaintColor color)
        {
            PresentSplat(point, normal, radius, color);
            RpcSplat(point, normal, radius, (byte)color);
        }

        static void PresentSplat(Vector3 point, Vector3 normal, float radius, PaintColor color)
        {
            var w = World.Instance;
            if (w != null && w.fx != null && color != PaintColor.None) w.fx.Splat(point, normal, radius, color);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcSplat(Vector3 point, Vector3 normal, float radius, byte color)
        {
            PresentSplat(point, normal, radius, (PaintColor)color);
        }

        static void PresentTracer(Vector3 a, Vector3 b, PaintColor color)
        {
            var w = World.Instance;
            if (w != null && w.fx != null) w.fx.Tracer(a, b, color == PaintColor.None ? PaintColor.Pink : color);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcTracer(Vector3 a, Vector3 b, byte color)
        {
            PresentTracer(a, b, (PaintColor)color);
        }

        static void PresentProjectile(int id, Vector3 origin, Vector3 vel, float gravity, PaintColor color, float size)
        {
            var w = World.Instance;
            if (w != null && w.fx != null) w.fx.Projectile(id, origin, vel, gravity, color, size);
        }

        static void PresentProjectileEnd(int id)
        {
            var w = World.Instance;
            if (w != null && w.fx != null) w.fx.EndProjectile(id);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcProjectile(int id, Vector3 origin, Vector3 vel, float gravity, byte color, float size)
        {
            PresentProjectile(id, origin, vel, gravity, (PaintColor)color, size);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcProjectileEnd(int id)
        {
            PresentProjectileEnd(id);
        }

        void ServerSound(SfxKind kind)
        {
            PlaySound(kind);
            RpcSound((byte)kind);
        }

        static void PlaySound(SfxKind kind)
        {
            var w = World.Instance;
            if (w != null && w.sfx != null) w.sfx.Play(kind);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcSound(byte kind)
        {
            PlaySound((SfxKind)kind);
        }

        static void ClearWorldLocal(byte location)
        {
            var w = World.Instance;
            if (w == null) return;
            w.ShowLocation(location);
            foreach (var f in w.faces) if (f != null) f.Clear();
            if (w.fx != null) w.fx.ClearAll();
            foreach (var h in w.heads) if (h != null) h.ResetPose();
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcClearWorld(byte location)
        {
            ClearWorldLocal(location);
        }

        [ObserversRpc(ExcludeServer = true)]
        void RpcShowLocation(byte location)
        {
            if (World.Instance != null) World.Instance.ShowLocation(location);
        }

        // ================= события интерфейса =================

        void ServerBanner(string text, byte style, float seconds)
        {
            RpcBanner(text, style, seconds);
        }

        void ServerFeed(string text)
        {
            RpcFeed(text);
        }

        [ObserversRpc]
        void RpcBanner(string text, byte style, float seconds)
        {
            if (BannerShown != null) BannerShown(text, style, seconds);
        }

        [ObserversRpc]
        void RpcFeed(string text)
        {
            if (FeedAdded != null) FeedAdded(text);
        }

        [ObserversRpc]
        void RpcRoundBegin(byte round, byte modelSlot)
        {
            HasReveal = false;
            KnownOption = -1;
            PickOptions = new byte[0];
        }

        [ObserversRpc]
        void RpcBlink()
        {
            PlaySound(SfxKind.Buzz);
        }

        [TargetRpc]
        void TargetPick(NetworkConnection conn, byte[] options, byte location)
        {
            PickOptions = options;
            if (PickOffered != null) PickOffered();
        }

        [TargetRpc]
        void TargetPicked(NetworkConnection conn, byte option)
        {
            KnownOption = option;
        }

        [TargetRpc]
        void TargetVision(NetworkConnection conn, float seconds, byte option)
        {
            if (VisionGranted != null) VisionGranted(seconds, option);
        }

        [ObserversRpc]
        void RpcReveal(RevealPacket packet)
        {
            LastReveal = packet;
            HasReveal = true;
            KnownOption = packet.option;
            if (Revealed != null) Revealed(packet);
            PlaySound(SfxKind.Tada);
        }

        [ObserversRpc]
        void RpcTip(byte slot, int amount, int total)
        {
            PlaySound(SfxKind.Coin);
            if (FeedAdded != null) FeedAdded("Чаевые: " + NameOf(slot) + " +" + amount + " (всего " + total + ")");
        }

        [ObserversRpc]
        void RpcSummary(SummaryPacket packet)
        {
            LastSummary = packet;
            HasSummary = true;
            if (Summarized != null) Summarized(packet);
            PlaySound(SfxKind.Tada);
        }

        // ================= для автотестов =================

        /// <summary>Отпечаток лиц (одинаковый у всех компьютеров, если синхронизация работает).</summary>
        public static uint FacesHash()
        {
            var w = World.Instance;
            if (w == null) return 0;
            uint h = 17;
            foreach (var f in w.faces) if (f != null) h = h * 31 + f.Grid.Hash();
            return h;
        }

        public int ServerOptionForTests { get { return serverOption; } }
        public int RoundStampCount { get { return roundStamps.Count; } }
    }
}
