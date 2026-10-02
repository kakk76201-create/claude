using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace MakeupSniper.Net
{
    /// <summary>
    /// Создание игры и вход по коду. Хост запускает сервер у себя (FishNet, хост решает всё),
    /// друзья подключаются по коду. Два способа связи работают одновременно:
    /// прямой (Tugboat: своя сеть, Radmin VPN и т. п.) и через Steam (FishySteamworks).
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public enum Phase { Offline, Starting, Searching, Connecting, Online, Failed }

        public const int MaxPlayers = 4;
        public const int TugboatIndex = 0;
        public const int SteamIndex = 1;

        public static NetSession Instance { get; private set; }

        public NetworkManager network;
        public Multipass multipass;
        public Tugboat tugboat;
        public FishySteamworks.FishySteamworks steamTransport;
        public SteamService steam;

        public Phase State { get; private set; } = Phase.Offline;
        public bool IsHost { get; private set; }
        public bool IsPractice { get; private set; }
        public string Message { get; private set; } = "";
        public string DirectCode { get; private set; } = "";
        public string SteamCode { get; private set; } = "";
        public string SteamCodeStatus { get; private set; } = "";
        public ushort Port { get; private set; }
        public List<NetAdapter> Adapters { get; private set; } = new List<NetAdapter>();
        public int AdapterIndex { get; private set; }
        public NetAdapter CurrentAdapter { get { return AdapterIndex >= 0 && AdapterIndex < Adapters.Count ? Adapters[AdapterIndex] : default(NetAdapter); } }

        /// <summary>Что-то поменялось (состояние, коды, сообщение) — интерфейсу пора обновиться.</summary>
        public event Action Changed;

        float connectDeadline;
        bool steamServerStarted;
        readonly System.Random rng = new System.Random();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (network == null) network = FindAnyObjectByType<NetworkManager>();
            if (network == null) { Debug.LogError("[MakeupSniper] NetworkManager не найден"); return; }
            network.ServerManager.OnServerConnectionState += OnServerState;
            network.ClientManager.OnClientConnectionState += OnClientState;
            // клиентский транспорт по умолчанию — прямой (иначе FishNet ругается при первой проверке)
            if (multipass != null) multipass.SetClientTransport<Tugboat>();
        }

        void OnDestroy()
        {
            if (network != null)
            {
                network.ServerManager.OnServerConnectionState -= OnServerState;
                network.ClientManager.OnClientConnectionState -= OnClientState;
            }
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if ((State == Phase.Connecting || State == Phase.Searching) && Time.unscaledTime > connectDeadline)
                Fail(State == Phase.Searching
                    ? "Steam долго ищет комнату. Проверь код и попробуй ещё раз"
                    : "Не получилось подключиться. Проверь код, что хост в игре и что вы в одной сети (или в Radmin VPN)");
        }

        void Notify() { if (Changed != null) Changed(); }

        // ---------- создание игры ----------

        /// <summary>Создать игру. practice — тренировка в одиночку (без кода Steam).</summary>
        public void Host(bool practice)
        {
            if (network == null) return;
            StopAll();
            IsHost = true;
            IsPractice = practice;
            DirectCode = SteamCode = "";
            SteamCodeStatus = "";
            Port = FindFreePort();
            if (Port == 0) { Fail("Все порты 7770–7801 заняты. Закрой другие копии игры"); return; }

            tugboat.SetPort(Port);
            tugboat.SetMaximumClients(MaxPlayers);
            State = Phase.Starting;
            Message = practice ? "Запускаем тренировку…" : "Создаём игру…";
            connectDeadline = Time.unscaledTime + 10f;
            Notify();
            if (!multipass.StartConnection(true, TugboatIndex))
                Fail("Не удалось запустить сервер на порту " + Port);
        }

        void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.TransportIndex != TugboatIndex) return;
            if (args.ConnectionState == LocalConnectionState.Started && IsHost && State == Phase.Starting)
            {
                // свой же клиент хоста подключается к себе по петле
                multipass.SetClientTransport<Tugboat>();
                tugboat.SetClientAddress("127.0.0.1");
                tugboat.SetPort(Port);
                State = Phase.Connecting;
                connectDeadline = Time.unscaledTime + 10f;
                network.ClientManager.StartConnection();
                RefreshDirectCode();
                if (!IsPractice) StartSteamHosting();
                Notify();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped && IsHost && State != Phase.Offline && State != Phase.Failed)
            {
                Fail("Сервер остановлен");
            }
        }

        void StartSteamHosting()
        {
            if (steam == null || steamTransport == null || !steam.Ready)
            {
                SteamCodeStatus = steam != null ? steam.Status : "Steam недоступен";
                return;
            }
            try
            {
                steamServerStarted = multipass.StartConnection(true, SteamIndex);
            }
            catch (Exception e)
            {
                steamServerStarted = false;
                SteamCodeStatus = "Steam: сервер не запустился (" + e.Message + ")";
                return;
            }
            if (!steamServerStarted) { SteamCodeStatus = "Steam: сервер не запустился"; return; }
            string code = JoinCode.NewSteamCode(rng);
            SteamCodeStatus = "Создаём комнату в Steam…";
            steam.CreateLobby(code, MaxPlayers, (ok, err) =>
            {
                if (ok) { SteamCode = code; SteamCodeStatus = "Через интернет: у друга должен быть запущен Steam"; }
                else SteamCodeStatus = err;
                Notify();
            });
        }

        /// <summary>Показать код для другой сети компьютера (например, Radmin вместо домашней).</summary>
        public void NextAdapter()
        {
            if (Adapters.Count == 0) return;
            AdapterIndex = (AdapterIndex + 1) % Adapters.Count;
            DirectCode = JoinCode.EncodeDirect(CurrentAdapter.Address, Port);
            Notify();
        }

        void RefreshDirectCode()
        {
            Adapters = NetAdapters.List();
            AdapterIndex = 0;
            DirectCode = Adapters.Count > 0 ? JoinCode.EncodeDirect(Adapters[0].Address, Port) : "";
        }

        static ushort FindFreePort()
        {
            for (int i = 0; i < JoinCode.PortRange; i++)
            {
                ushort p = (ushort)(JoinCode.BasePort + i);
                try
                {
                    using (var probe = new UdpClient(new IPEndPoint(IPAddress.Any, p))) { }
                    return p;
                }
                catch (SocketException) { }
            }
            return 0;
        }

        // ---------- вход по коду ----------

        public void Join(string rawCode)
        {
            if (network == null) return;
            StopAll();
            IsHost = false;
            IsPractice = false;
            string code = JoinCode.Normalize(rawCode);
            switch (JoinCode.Classify(code))
            {
                case CodeKind.Direct:
                {
                    string ip; ushort port;
                    JoinCode.TryDecodeDirect(code, out ip, out port);
                    multipass.SetClientTransport<Tugboat>();
                    tugboat.SetClientAddress(ip);
                    tugboat.SetPort(port);
                    State = Phase.Connecting;
                    Message = "Подключаемся к " + ip + "…";
                    connectDeadline = Time.unscaledTime + 15f;
                    Notify();
                    network.ClientManager.StartConnection();
                    break;
                }
                case CodeKind.Steam:
                {
                    if (steam == null || !steam.Ready) { Fail("Это код Steam, а Steam не запущен. Запусти Steam и перезапусти игру"); return; }
                    State = Phase.Searching;
                    Message = "Ищем комнату " + JoinCode.Pretty(code) + " в Steam…";
                    connectDeadline = Time.unscaledTime + 20f;
                    Notify();
                    steam.FindLobby(code, (hostId, err) =>
                    {
                        if (State != Phase.Searching) return;
                        if (hostId == 0) { Fail(err); return; }
                        multipass.SetClientTransport<FishySteamworks.FishySteamworks>();
                        steamTransport.SetClientAddress(hostId.ToString());
                        State = Phase.Connecting;
                        Message = "Комната найдена, подключаемся через Steam…";
                        connectDeadline = Time.unscaledTime + 20f;
                        Notify();
                        network.ClientManager.StartConnection();
                    });
                    break;
                }
                default:
                    Fail("Код не подходит. Прямой код — 8 знаков (например K7QX-2M9B), код Steam — 6 букв");
                    break;
            }
        }

        void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                State = Phase.Online;
                Message = IsHost ? (IsPractice ? "Тренировка" : "Игра создана") : "Подключено";
                Notify();
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (State == Phase.Connecting) Fail(IsHost ? "Не удалось подключиться к своему серверу" : "Не получилось подключиться. Проверь код и сеть");
                else if (State == Phase.Online)
                {
                    bool wasHost = IsHost;
                    StopAll();
                    State = Phase.Offline;
                    Message = wasHost ? "Игра закрыта" : "Связь с хостом потеряна (хост вышел или пропал интернет)";
                    Notify();
                }
            }
        }

        // ---------- выход ----------

        public void Leave()
        {
            StopAll();
            IsHost = false;
            IsPractice = false;
            State = Phase.Offline;
            Message = "";
            Notify();
        }

        void Fail(string message)
        {
            StopAll();
            State = Phase.Failed;
            Message = message;
            Notify();
        }

        void StopAll()
        {
            State = Phase.Offline; // чтобы события остановки не считались ошибкой
            if (network != null)
            {
                if (multipass != null && multipass.ClientTransport != null
                    && multipass.ClientTransport.GetConnectionState(false) != LocalConnectionState.Stopped)
                    network.ClientManager.StopConnection();
                if (multipass != null)
                {
                    for (int i = 0; i < multipass.Transports.Count; i++)
                    {
                        if (multipass.GetConnectionState(true, i) == LocalConnectionState.Stopped) continue;
                        try { multipass.StopServerConnection(true, i); }
                        catch (Exception e) { Debug.LogWarning("[MakeupSniper] остановка транспорта " + i + ": " + e.Message); }
                    }
                }
            }
            if (steam != null) steam.LeaveLobby();
            steamServerStarted = false;
            DirectCode = SteamCode = "";
        }
    }
}
