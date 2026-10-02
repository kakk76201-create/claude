using System;
using Steamworks;
using UnityEngine;

namespace MakeupSniper.Net
{
    /// <summary>
    /// Связь со Steam. Своей страницы в Steam у игры пока нет, поэтому используется тестовый
    /// номер приложения 480 (Spacewar), который Valve даёт всем разработчикам для проверок.
    /// Steam нужен только для игры через интернет по коду Steam: он сам соединяет компьютеры
    /// через свои серверы, без настроек роутера.
    /// </summary>
    public class SteamService : MonoBehaviour
    {
        public const uint DevAppId = 480;
        const string KeyGame = "ms_game";
        const string KeyCode = "ms_code";
        const string KeyHost = "ms_host";
        const string GameTag = "makeupsniper-proto-1";

        public bool Ready { get; private set; }
        public string Status { get; private set; } = "Steam не проверялся";
        public ulong LocalSteamId { get; private set; }
        public string PersonaName { get; private set; } = "";
        public bool HasLobby { get { return lobby.m_SteamID != 0; } }

        CSteamID lobby;
        CallResult<LobbyCreated_t> lobbyCreated;
        CallResult<LobbyMatchList_t> lobbyList;
        Action<bool, string> pendingCreate;
        Action<ulong, string> pendingFind;
        string pendingCode;
        bool initTried;

        void Awake()
        {
            TryInit();
        }

        /// <summary>Подключиться к запущенному Steam. В автотестах и с ключом -nosteam не трогаем Steam вовсе.</summary>
        public void TryInit()
        {
            if (initTried) return;
            initTried = true;
            if (Application.isBatchMode || HasArg("-nosteam"))
            {
                Status = "Steam отключён (тестовый запуск)";
                return;
            }
            try
            {
                // без своей страницы в Steam используем тестовое приложение 480
                Environment.SetEnvironmentVariable("SteamAppId", DevAppId.ToString());
                Environment.SetEnvironmentVariable("SteamGameId", DevAppId.ToString());
                if (!Packsize.Test()) { Status = "Steam: неверная сборка библиотеки"; return; }
                if (!DllCheck.Test()) { Status = "Steam: не та версия steam_api64.dll"; return; }
                if (!SteamAPI.IsSteamRunning()) { Status = "Steam не запущен — код Steam недоступен"; return; }
                Ready = SteamAPI.Init();
                if (!Ready) { Status = "Steam не ответил — перезапусти Steam"; return; }
                LocalSteamId = SteamUser.GetSteamID().m_SteamID;
                PersonaName = SteamFriends.GetPersonaName();
                lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
                lobbyList = CallResult<LobbyMatchList_t>.Create(OnLobbyList);
                SteamNetworkingUtils.InitRelayNetworkAccess();
                Status = "Steam подключён: " + PersonaName;
            }
            catch (DllNotFoundException)
            {
                Ready = false;
                Status = "Steam: нет файла steam_api64.dll рядом с игрой";
            }
            catch (Exception e)
            {
                Ready = false;
                Status = "Steam: ошибка " + e.Message;
            }
        }

        static bool HasArg(string arg)
        {
            foreach (string a in Environment.GetCommandLineArgs()) if (string.Equals(a, arg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void Update()
        {
            if (Ready) SteamAPI.RunCallbacks();
        }

        void OnApplicationQuit()
        {
            if (!Ready) return;
            LeaveLobby();
            SteamAPI.Shutdown();
            Ready = false;
        }

        /// <summary>Создать комнату с кодом, которую друг найдёт через поиск лобби Steam.</summary>
        public void CreateLobby(string code, int maxPlayers, Action<bool, string> done)
        {
            if (!Ready) { if (done != null) done(false, Status); return; }
            LeaveLobby();
            pendingCreate = done;
            pendingCode = code;
            lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers));
        }

        void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            var done = pendingCreate;
            pendingCreate = null;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                if (done != null) done(false, "Steam не создал комнату: " + result.m_eResult);
                return;
            }
            lobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, KeyGame, GameTag);
            SteamMatchmaking.SetLobbyData(lobby, KeyCode, pendingCode);
            SteamMatchmaking.SetLobbyData(lobby, KeyHost, LocalSteamId.ToString());
            if (done != null) done(true, null);
        }

        /// <summary>Найти комнату по коду. Возвращает SteamID хоста.</summary>
        public void FindLobby(string code, Action<ulong, string> done)
        {
            if (!Ready) { if (done != null) done(0, Status); return; }
            pendingFind = done;
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyGame, GameTag, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyCode, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(5);
            lobbyList.Set(SteamMatchmaking.RequestLobbyList());
        }

        void OnLobbyList(LobbyMatchList_t result, bool ioFailure)
        {
            var done = pendingFind;
            pendingFind = null;
            if (done == null) return;
            if (ioFailure) { done(0, "Steam не ответил на поиск комнаты"); return; }
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID id = SteamMatchmaking.GetLobbyByIndex(i);
                string host = SteamMatchmaking.GetLobbyData(id, KeyHost);
                ulong hostId;
                if (ulong.TryParse(host, out hostId) && hostId != 0) { done(hostId, null); return; }
            }
            done(0, "Комната с таким кодом не найдена. Проверь код и что у хоста запущена игра");
        }

        public void LeaveLobby()
        {
            if (!Ready || lobby.m_SteamID == 0) return;
            SteamMatchmaking.LeaveLobby(lobby);
            lobby = new CSteamID(0);
        }
    }
}
