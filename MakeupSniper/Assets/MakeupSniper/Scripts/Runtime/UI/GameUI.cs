using System.Collections.Generic;
using System.Text;
using MakeupSniper.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MakeupSniper
{
    /// <summary>
    /// Весь интерфейс, собранный кодом: меню с кодом входа, лобби, интерфейс раунда для стрелка и Модели,
    /// выбор образа, раскрытие с полароидом и чаевыми, итоги вечера, пауза.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        public Canvas Canvas { get; private set; }

        /// <summary>Интерфейс сейчас перехватывает мышь и клавиатуру (меню, пауза, ввод текста, выбор образа).</summary>
        public bool BlocksGameplay { get; private set; }

        GameObject menuRoot, connectRoot, lobbyRoot, hudRoot, pickRoot, revealRoot, summaryRoot, pauseRoot;
        // меню
        InputField nameField, codeField;
        Text menuStatus, steamStatus;
        // подключение
        Text connectText;
        // лобби
        Text lobbySteamCode, lobbySteamHint, lobbyDirectCode, lobbyDirectHint, lobbyPlayers, lobbyLocation, lobbyRounds, lobbyHint, lobbyTitle;
        Button btnPrevLoc, btnNextLoc, btnRoundsMinus, btnRoundsPlus, btnStart, btnOtherNet;
        GameObject lobbyCodesPanel;
        // раунд
        Text timerText, roundText, infoText, playersText, hintText, bannerText, feedText, countdownText, waitText, suspicionText;
        GameObject crosshair, scope, bannerRoot, suspicionRoot, waitRoot;
        Image bannerImage, suspicionFill, reloadFill;
        GameObject phoneRoot;
        RawImage[] phoneImages = new RawImage[2];
        Text phoneName, phoneTaboo;
        GameObject mirrorRoot;
        RawImage mirrorImage;
        GameObject visionRoot;
        RawImage[] visionImages = new RawImage[2];
        Text visionText;
        // выбор образа
        RawImage[,] pickImages = new RawImage[3, 2];
        Text[] pickNames = new Text[3], pickTaboo = new Text[3];
        GameObject[] pickCards = new GameObject[3];
        Text betText, pickTimer;
        int bet = 50;
        bool pickSent;
        // раскрытие
        RawImage[] revRef = new RawImage[2], revRes = new RawImage[2];
        Text[] revRefLabel = new Text[2], revResLabel = new Text[2];
        Text revHeadline, revStamp, revZones, revPlayers, revStars, revUnlock, revTips, revHint;
        GameObject tipsRoot;
        Button[] tipButtons = new Button[4];
        Button btnRevealNext;
        // итоги
        Text sumAwards, sumPlayers, sumRounds;
        Button btnSumAgain, btnSumLobby;
        // пауза
        Text sensText;
        bool pauseOpen;

        float bannerLeft;
        readonly List<string> feed = new List<string>();
        readonly List<float> feedTimes = new List<float>();
        float revealArrivedAt = -10f;
        bool polaroidSaved = true;
        PolaroidSaver saver;
        RectTransform polaroidRect;
        float visionUntil;
        int visionOption = -1;
        Match boundMatch;
        readonly Dictionary<ReferenceData, Texture2D> previews = new Dictionary<ReferenceData, Texture2D>();
        bool built;

        void Awake()
        {
            Instance = this;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var t in previews.Values) if (t != null) Destroy(t);
        }

        // ================= сборка =================

        void Build()
        {
            if (built) return;
            built = true;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var module = es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            var cgo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = cgo.transform;

            saver = gameObject.AddComponent<PolaroidSaver>();
            BuildHud(root);
            BuildPick(root);
            BuildReveal(root);
            BuildSummary(root);
            BuildLobby(root);
            BuildMenu(root);
            BuildConnect(root);
            BuildPause(root);
        }

        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        static readonly Vector2 TL = new Vector2(0f, 1f);
        static readonly Vector2 TR = new Vector2(1f, 1f);
        static readonly Vector2 BL = new Vector2(0f, 0f);
        static readonly Vector2 BR = new Vector2(1f, 0f);
        static readonly Vector2 TC = new Vector2(0.5f, 1f);
        static readonly Vector2 BC = new Vector2(0.5f, 0f);

        void BuildMenu(Transform root)
        {
            menuRoot = UiKit.Backdrop("Menu", root, new Color(0.97f, 0.91f, 0.93f, 0.78f)).gameObject;
            Transform m = menuRoot.transform;
            var title = UiKit.Label("Title", m, "MAKEUP SNIPER", 120, TextAnchor.MiddleCenter, UiKit.Accent, TC, new Vector2(0f, -70f), new Vector2(1600f, 150f));
            title.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(title, Color.white, 4f);
            UiKit.Label("Sub", m, "Накрась друга как на фото. Из снайперской винтовки.", 36, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -215f), new Vector2(1600f, 60f));

            var card = UiKit.Box("Card", m, UiKit.Panel, C, new Vector2(0f, -40f), new Vector2(980f, 620f));
            Transform c = card.transform;
            UiKit.Label("NameLabel", c, "Твоё имя", 28, TextAnchor.MiddleLeft, UiKit.Muted, TC, new Vector2(-250f, -30f), new Vector2(400f, 40f));
            nameField = UiKit.Input("Name", c, "как тебя зовут", TC, new Vector2(0f, -85f), new Vector2(900f, 70f));
            nameField.characterLimit = 16;
            nameField.text = PlayerPrefs.GetString(PlayerAgent.NamePref, "");
            nameField.onEndEdit.AddListener(v => { PlayerPrefs.SetString(PlayerAgent.NamePref, v.Trim()); PlayerPrefs.Save(); });

            UiKit.Button("Host", c, "Создать игру", TC, new Vector2(-230f, -195f), new Vector2(440f, 90f), () => { SaveName(); NetSession.Instance.Host(false); }, true, 34);
            UiKit.Button("Practice", c, "Тренировка одному", TC, new Vector2(230f, -195f), new Vector2(440f, 90f), () => { SaveName(); NetSession.Instance.Host(true); }, false, 30);

            UiKit.Label("CodeLabel", c, "Код от друга", 28, TextAnchor.MiddleLeft, UiKit.Muted, TC, new Vector2(-250f, -285f), new Vector2(400f, 40f));
            codeField = UiKit.Input("Code", c, "K7QX-2M9B или KQX-MBR", TC, new Vector2(-150f, -345f), new Vector2(600f, 76f), 38);
            codeField.characterLimit = 12;
            codeField.onSubmit.AddListener(v => JoinPressed());
            UiKit.Button("Join", c, "Войти", TC, new Vector2(310f, -345f), new Vector2(280f, 76f), JoinPressed, true, 34);

            menuStatus = UiKit.Label("Status", c, "", 28, TextAnchor.UpperCenter, UiKit.Bad, TC, new Vector2(0f, -405f), new Vector2(900f, 80f));
            steamStatus = UiKit.Label("Steam", c, "", 24, TextAnchor.UpperCenter, UiKit.Muted, TC, new Vector2(0f, -490f), new Vector2(900f, 40f));
            UiKit.Label("Help", c, "Код из 8 знаков — по локальной сети или через Radmin VPN. Код из 6 букв — через Steam (у обоих запущен Steam).",
                22, TextAnchor.UpperCenter, UiKit.Muted, TC, new Vector2(0f, -535f), new Vector2(900f, 70f));
            UiKit.Button("Quit", m, "Выход", BR, new Vector2(-30f, 30f), new Vector2(220f, 70f), () => Application.Quit(), false, 28);
            UiKit.Label("Version", m, "прототип · неделя 2: игра по коду", 22, TextAnchor.LowerLeft, UiKit.Muted, BL, new Vector2(30f, 30f), new Vector2(700f, 40f));
        }

        void SaveName()
        {
            PlayerPrefs.SetString(PlayerAgent.NamePref, nameField.text.Trim());
            PlayerPrefs.Save();
        }

        void JoinPressed()
        {
            SaveName();
            if (NetSession.Instance != null) NetSession.Instance.Join(codeField.text);
        }

        void BuildConnect(Transform root)
        {
            connectRoot = UiKit.Backdrop("Connect", root, new Color(0.97f, 0.91f, 0.93f, 0.85f)).gameObject;
            connectText = UiKit.Label("Text", connectRoot.transform, "Подключаемся…", 44, TextAnchor.MiddleCenter, UiKit.Ink, C, new Vector2(0f, 60f), new Vector2(1500f, 200f));
            UiKit.Button("Cancel", connectRoot.transform, "Отмена", C, new Vector2(0f, -110f), new Vector2(300f, 80f), () => NetSession.Instance.Leave(), false);
        }

        void BuildLobby(Transform root)
        {
            lobbyRoot = UiKit.Backdrop("Lobby", root, new Color(0.97f, 0.91f, 0.93f, 0.55f)).gameObject;
            Transform l = lobbyRoot.transform;
            lobbyTitle = UiKit.Label("Title", l, "ЛОББИ", 64, TextAnchor.MiddleCenter, UiKit.Accent, TC, new Vector2(0f, -40f), new Vector2(1400f, 90f));
            lobbyTitle.fontStyle = FontStyle.Bold;

            var codes = UiKit.Box("Codes", l, UiKit.Panel, TL, new Vector2(40f, -150f), new Vector2(620f, 700f));
            lobbyCodesPanel = codes.gameObject;
            Transform cp = codes.transform;
            UiKit.Label("H1", cp, "Позови друзей", 36, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -20f), new Vector2(580f, 50f)).fontStyle = FontStyle.Bold;
            UiKit.Label("SteamTitle", cp, "Через интернет (Steam)", 26, TextAnchor.MiddleCenter, UiKit.Muted, TC, new Vector2(0f, -85f), new Vector2(580f, 40f));
            lobbySteamCode = UiKit.Label("SteamCode", cp, "—", 84, TextAnchor.MiddleCenter, UiKit.Accent, TC, new Vector2(0f, -130f), new Vector2(580f, 100f));
            lobbySteamCode.fontStyle = FontStyle.Bold;
            lobbySteamHint = UiKit.Label("SteamHint", cp, "", 22, TextAnchor.UpperCenter, UiKit.Muted, TC, new Vector2(0f, -235f), new Vector2(560f, 70f));
            UiKit.Label("DirectTitle", cp, "По сети / Radmin VPN", 26, TextAnchor.MiddleCenter, UiKit.Muted, TC, new Vector2(0f, -320f), new Vector2(580f, 40f));
            lobbyDirectCode = UiKit.Label("DirectCode", cp, "—", 76, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -365f), new Vector2(580f, 95f));
            lobbyDirectCode.fontStyle = FontStyle.Bold;
            lobbyDirectHint = UiKit.Label("DirectHint", cp, "", 22, TextAnchor.UpperCenter, UiKit.Muted, TC, new Vector2(0f, -465f), new Vector2(560f, 100f));
            btnOtherNet = UiKit.Button("OtherNet", cp, "Другая сеть", TC, new Vector2(0f, -590f), new Vector2(300f, 66f), () => NetSession.Instance.NextAdapter(), false, 26);

            var players = UiKit.Box("Players", l, UiKit.Panel, TC, new Vector2(0f, -150f), new Vector2(560f, 700f));
            UiKit.Label("H2", players.transform, "Бригада", 36, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -20f), new Vector2(520f, 50f)).fontStyle = FontStyle.Bold;
            lobbyPlayers = UiKit.Label("List", players.transform, "", 34, TextAnchor.UpperLeft, UiKit.Ink, TC, new Vector2(0f, -90f), new Vector2(500f, 580f));

            var loc = UiKit.Box("Location", l, UiKit.Panel, TR, new Vector2(-40f, -150f), new Vector2(620f, 700f));
            Transform lp = loc.transform;
            UiKit.Label("H3", lp, "Куда едем", 36, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -20f), new Vector2(580f, 50f)).fontStyle = FontStyle.Bold;
            lobbyLocation = UiKit.Label("Loc", lp, "", 30, TextAnchor.UpperCenter, UiKit.Ink, TC, new Vector2(0f, -85f), new Vector2(560f, 330f));
            btnPrevLoc = UiKit.Button("Prev", lp, "◀", TC, new Vector2(-200f, -430f), new Vector2(110f, 76f), () => HostCmd(HostCommand.SetLocation, (byte)Mathf.Max(0, Match.Instance.LocationIndex - 1)), false, 36);
            btnNextLoc = UiKit.Button("Next", lp, "▶", TC, new Vector2(200f, -430f), new Vector2(110f, 76f), () => HostCmd(HostCommand.SetLocation, (byte)(Match.Instance.LocationIndex + 1)), false, 36);
            lobbyRounds = UiKit.Label("Rounds", lp, "", 28, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -520f), new Vector2(560f, 50f));
            btnRoundsMinus = UiKit.Button("RMinus", lp, "−", TC, new Vector2(-230f, -520f), new Vector2(70f, 60f), () => HostCmd(HostCommand.SetRounds, (byte)(Match.Instance.RoundsPerPlayer - 1)), false, 34);
            btnRoundsPlus = UiKit.Button("RPlus", lp, "+", TC, new Vector2(230f, -520f), new Vector2(70f, 60f), () => HostCmd(HostCommand.SetRounds, (byte)(Match.Instance.RoundsPerPlayer + 1)), false, 34);
            btnStart = UiKit.Button("Start", lp, "НАЧАТЬ", TC, new Vector2(0f, -605f), new Vector2(420f, 90f), () => HostCmd(HostCommand.Start, 0), true, 40);

            lobbyHint = UiKit.Label("Hint", l, "", 26, TextAnchor.MiddleCenter, UiKit.Ink, BC, new Vector2(0f, 120f), new Vector2(1700f, 80f));
            UiKit.Button("Leave", l, "Выйти в меню", BC, new Vector2(0f, 30f), new Vector2(360f, 70f), () => NetSession.Instance.Leave(), false, 28);
        }

        void HostCmd(HostCommand cmd, byte arg)
        {
            var me = PlayerAgent.Local;
            if (me != null) me.CmdHost((byte)cmd, arg);
        }

        void BuildHud(Transform root)
        {
            hudRoot = UiKit.Fill("HUD", root).gameObject;
            Transform h = hudRoot.transform;

            scope = UiKit.Box("Scope", h, new Color(0.12f, 0.08f, 0.12f, 0.55f), C, Vector2.zero, new Vector2(4000f, 4000f)).gameObject;
            var hole = UiKit.Box("Hole", scope.transform, new Color(1f, 1f, 1f, 0f), C, Vector2.zero, new Vector2(700f, 700f));
            hole.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.05f, 0.1f, 0.9f);
            scope.SetActive(false);

            timerText = UiKit.Label("Timer", h, "60", 84, TextAnchor.UpperCenter, Color.white, TC, new Vector2(0f, -10f), new Vector2(320f, 110f));
            timerText.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(timerText, UiKit.Accent);
            roundText = UiKit.Label("Round", h, "", 24, TextAnchor.UpperCenter, UiKit.Ink, TC, new Vector2(0f, -112f), new Vector2(700f, 36f));
            UiKit.AddOutline(roundText, Color.white, 2f);

            suspicionRoot = UiKit.Box("Suspicion", h, UiKit.Panel, TC, new Vector2(0f, -152f), new Vector2(520f, 60f)).gameObject;
            suspicionText = UiKit.Label("Text", suspicionRoot.transform, "", 22, TextAnchor.UpperCenter, UiKit.Ink, TC, new Vector2(0f, -4f), new Vector2(500f, 30f));
            UiKit.Box("Back", suspicionRoot.transform, UiKit.Line, BC, new Vector2(0f, 8f), new Vector2(480f, 14f));
            suspicionFill = UiKit.Box("Fill", suspicionRoot.transform, UiKit.Bad, new Vector2(0f, 0f), new Vector2(20f, 8f), new Vector2(0f, 14f));
            suspicionRoot.SetActive(false);

            var infoBox = UiKit.Box("InfoBox", h, UiKit.Panel, TL, new Vector2(24f, -24f), new Vector2(560f, 190f));
            infoText = UiKit.Label("Info", infoBox.transform, "", 26, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(16f, -12f), new Vector2(530f, 140f));
            UiKit.Box("ReloadBack", infoBox.transform, UiKit.Line, BL, new Vector2(16f, 14f), new Vector2(528f, 12f));
            reloadFill = UiKit.Box("ReloadFill", infoBox.transform, UiKit.Accent, BL, new Vector2(16f, 14f), new Vector2(528f, 12f));

            var playersBox = UiKit.Box("PlayersBox", h, UiKit.Panel, TR, new Vector2(-24f, -24f), new Vector2(420f, 210f));
            playersText = UiKit.Label("Players", playersBox.transform, "", 26, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(16f, -12f), new Vector2(390f, 190f));

            var hintBox = UiKit.Box("HintBox", h, UiKit.Panel, BL, new Vector2(24f, 24f), new Vector2(1040f, 96f));
            hintText = UiKit.Label("Hint", hintBox.transform, "", 22, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(14f, -10f), new Vector2(1010f, 80f));

            feedText = UiKit.Label("Feed", h, "", 24, TextAnchor.LowerRight, UiKit.Ink, BR, new Vector2(-24f, 24f), new Vector2(760f, 220f));
            UiKit.AddOutline(feedText, Color.white, 2f);

            crosshair = UiKit.Node("Crosshair", h, C, Vector2.zero, new Vector2(40f, 40f)).gameObject;
            UiKit.Box("V", crosshair.transform, UiKit.Accent, C, Vector2.zero, new Vector2(3f, 36f));
            UiKit.Box("H", crosshair.transform, UiKit.Accent, C, Vector2.zero, new Vector2(36f, 3f));
            UiKit.Box("Dot", crosshair.transform, Color.white, C, Vector2.zero, new Vector2(5f, 5f));

            // телефон Модели с образом
            var phone = UiKit.Box("Phone", h, Color.white, BR, new Vector2(-24f, 250f), new Vector2(420f, 600f));
            phoneRoot = phone.gameObject;
            phoneImages[0] = UiKit.Picture("Ref0", phone.transform, TC, new Vector2(0f, -14f), new Vector2(390f, 390f));
            phoneImages[1] = UiKit.Picture("Ref1", phone.transform, TC, new Vector2(100f, -14f), new Vector2(190f, 190f));
            phoneName = UiKit.Label("Name", phone.transform, "", 32, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -410f), new Vector2(400f, 46f));
            phoneName.fontStyle = FontStyle.Bold;
            phoneTaboo = UiKit.Label("Taboo", phone.transform, "", 22, TextAnchor.UpperCenter, UiKit.Bad, TC, new Vector2(0f, -460f), new Vector2(400f, 130f));
            phoneRoot.SetActive(false);

            // зеркальце
            var mirror = UiKit.Box("Mirror", h, Color.white, C, new Vector2(0f, 40f), new Vector2(520f, 580f));
            mirrorRoot = mirror.gameObject;
            mirrorImage = UiKit.Picture("Image", mirror.transform, TC, new Vector2(0f, -16f), new Vector2(488f, 488f));
            mirrorImage.uvRect = new Rect(1f, 0f, -1f, 1f); // зеркало отражает
            UiKit.Label("Label", mirror.transform, "ЗЕРКАЛЬЦЕ: вот что у тебя на лице", 26, TextAnchor.MiddleCenter, UiKit.Ink, BC, new Vector2(0f, 20f), new Vector2(500f, 50f)).fontStyle = FontStyle.Bold;
            mirrorRoot.SetActive(false);

            // видение забрызганного стрелка
            visionRoot = UiKit.Fill("Vision", h).gameObject;
            visionRoot.AddComponent<Image>().color = new Color(1f, 0.56f, 0.69f, 0.55f);
            visionRoot.GetComponent<Image>().raycastTarget = false;
            visionImages[0] = UiKit.Picture("Ref0", visionRoot.transform, C, new Vector2(-170f, 40f), new Vector2(420f, 420f));
            visionImages[1] = UiKit.Picture("Ref1", visionRoot.transform, C, new Vector2(270f, 40f), new Vector2(420f, 420f));
            visionText = UiKit.Label("Text", visionRoot.transform, "", 40, TextAnchor.MiddleCenter, Color.white, C, new Vector2(0f, -250f), new Vector2(1500f, 120f));
            visionText.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(visionText, UiKit.AccentDark, 3f);
            visionRoot.SetActive(false);

            waitRoot = UiKit.Box("Wait", h, UiKit.Panel, C, new Vector2(0f, 120f), new Vector2(1100f, 120f)).gameObject;
            waitText = UiKit.Label("Text", waitRoot.transform, "", 38, TextAnchor.MiddleCenter, UiKit.Ink, C, Vector2.zero, new Vector2(1080f, 110f));
            waitRoot.SetActive(false);

            countdownText = UiKit.Label("Countdown", h, "", 260, TextAnchor.MiddleCenter, Color.white, C, Vector2.zero, new Vector2(1400f, 360f));
            countdownText.fontStyle = FontStyle.Bold;
            UiKit.AddOutline(countdownText, UiKit.Accent, 5f);

            bannerImage = UiKit.Box("Banner", root, UiKit.Accent, C, new Vector2(0f, 230f), new Vector2(980f, 96f));
            bannerRoot = bannerImage.gameObject;
            bannerRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            bannerText = UiKit.Label("Text", bannerRoot.transform, "", 50, TextAnchor.MiddleCenter, Color.white, C, Vector2.zero, new Vector2(960f, 92f));
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.resizeTextForBestFit = true;
            bannerText.resizeTextMinSize = 24;
            bannerText.resizeTextMaxSize = 50;
            bannerRoot.SetActive(false);
        }

        void BuildPick(Transform root)
        {
            pickRoot = UiKit.Backdrop("Pick", root, UiKit.Paper).gameObject;
            Transform p = pickRoot.transform;
            var t = UiKit.Label("Title", p, "Ты в кресле! Выбери образ", 60, TextAnchor.MiddleCenter, UiKit.Accent, TC, new Vector2(0f, -30f), new Vector2(1700f, 80f));
            t.fontStyle = FontStyle.Bold;
            UiKit.Label("Sub", p, "Стрелки образ не видят. Описывай словами, но НЕ говори запретные слова. Лево и право — твои.", 28, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -110f), new Vector2(1700f, 50f));
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var card = UiKit.Box("Card" + i, p, Color.white, C, new Vector2((i - 1) * 560f, 40f), new Vector2(520f, 640f));
                card.raycastTarget = true;
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = card;
                b.onClick.AddListener(() => SendPick(idx));
                pickCards[i] = card.gameObject;
                pickImages[i, 0] = UiKit.Picture("Ref0", card.transform, TC, new Vector2(0f, -16f), new Vector2(380f, 380f));
                pickImages[i, 1] = UiKit.Picture("Ref1", card.transform, TC, new Vector2(125f, -16f), new Vector2(240f, 240f));
                pickNames[i] = UiKit.Label("Name", card.transform, "", 34, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -405f), new Vector2(500f, 50f));
                pickNames[i].fontStyle = FontStyle.Bold;
                pickTaboo[i] = UiKit.Label("Taboo", card.transform, "", 22, TextAnchor.UpperCenter, UiKit.Bad, TC, new Vector2(0f, -455f), new Vector2(490f, 110f));
                var key = UiKit.Label("Key", card.transform, "клавиша " + (i + 1), 30, TextAnchor.MiddleCenter, UiKit.Accent, BC, new Vector2(0f, 18f), new Vector2(500f, 44f));
                key.fontStyle = FontStyle.Bold;
            }
            UiKit.Label("BetTitle", p, "Тайная ставка: какой % получится? Угадаешь ±10 — +" + Match.BetBonus + " монет", 28, TextAnchor.MiddleCenter, UiKit.Ink, BC, new Vector2(0f, 150f), new Vector2(1400f, 44f));
            UiKit.Button("BetMinus", p, "−10", BC, new Vector2(-170f, 70f), new Vector2(120f, 70f), () => bet = Mathf.Clamp(bet - 10, 0, 100), false, 30);
            betText = UiKit.Label("Bet", p, "50%", 54, TextAnchor.MiddleCenter, UiKit.Accent, BC, new Vector2(0f, 70f), new Vector2(200f, 70f));
            betText.fontStyle = FontStyle.Bold;
            UiKit.Button("BetPlus", p, "+10", BC, new Vector2(170f, 70f), new Vector2(120f, 70f), () => bet = Mathf.Clamp(bet + 10, 0, 100), false, 30);
            pickTimer = UiKit.Label("Timer", p, "", 34, TextAnchor.MiddleRight, UiKit.Muted, BR, new Vector2(-40f, 40f), new Vector2(500f, 50f));
        }

        void SendPick(int index)
        {
            if (pickSent) return;
            var me = PlayerAgent.Local;
            var m = Match.Instance;
            if (me == null || m == null || index >= m.PickOptions.Length) return;
            pickSent = true;
            me.CmdPick((byte)index, (byte)bet);
            if (World.Instance != null && World.Instance.sfx != null) World.Instance.sfx.Play(SfxKind.Click);
        }

        void BuildReveal(Transform root)
        {
            revealRoot = UiKit.Backdrop("Reveal", root, UiKit.Shade).gameObject;
            Transform r = revealRoot.transform;
            var pol = UiKit.Box("Polaroid", r, Color.white, TL, new Vector2(40f, -30f), new Vector2(1060f, 1020f));
            polaroidRect = pol.rectTransform;
            Transform p = pol.transform;
            for (int f = 0; f < 2; f++)
            {
                float y = f == 0 ? -24f : -470f;
                revRef[f] = UiKit.Picture("Ref" + f, p, TL, new Vector2(30f, y), new Vector2(400f, 400f));
                revRes[f] = UiKit.Picture("Res" + f, p, TL, new Vector2(470f, y), new Vector2(400f, 400f));
                revRefLabel[f] = UiKit.Label("RefL" + f, p, "РЕФЕРЕНС", 30, TextAnchor.MiddleCenter, UiKit.Ink, TL, new Vector2(30f, y - 400f), new Vector2(400f, 40f));
                revResLabel[f] = UiKit.Label("ResL" + f, p, "РЕЗУЛЬТАТ", 30, TextAnchor.MiddleCenter, UiKit.Ink, TL, new Vector2(470f, y - 400f), new Vector2(400f, 40f));
                revRefLabel[f].fontStyle = revResLabel[f].fontStyle = FontStyle.BoldAndItalic;
                foreach (var lbl in new[] { revRefLabel[f], revResLabel[f] })
                {
                    // длинные имена («Невеста после девичника») ужимаются, а не наезжают на соседей
                    lbl.resizeTextForBestFit = true; lbl.resizeTextMinSize = 16; lbl.resizeTextMaxSize = 30;
                    lbl.verticalOverflow = VerticalWrapMode.Truncate;
                }
            }
            var stamp = UiKit.Box("Stamp", p, new Color(1f, 1f, 1f, 0.95f), TR, new Vector2(30f, 20f), new Vector2(250f, 120f));
            stamp.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            UiKit.AddOutline(stamp, UiKit.Accent, 4f);
            revStamp = UiKit.Label("Pct", stamp.transform, "0%", 92, TextAnchor.MiddleCenter, UiKit.Accent, C, Vector2.zero, new Vector2(250f, 120f));
            revStamp.fontStyle = FontStyle.Bold;
            revStars = UiKit.Label("Stars", p, "", 60, TextAnchor.MiddleRight, UiKit.Gold, BR, new Vector2(-30f, 24f), new Vector2(260f, 76f));
            revHeadline = UiKit.Label("Headline", p, "", 30, TextAnchor.UpperLeft, UiKit.Ink, BL, new Vector2(30f, 20f), new Vector2(760f, 80f));
            revHeadline.fontStyle = FontStyle.Bold;
            revHeadline.resizeTextForBestFit = true; revHeadline.resizeTextMinSize = 18; revHeadline.resizeTextMaxSize = 30;
            revHeadline.verticalOverflow = VerticalWrapMode.Truncate;
            revHeadline.rectTransform.sizeDelta = new Vector2(760f, 92f);

            var side = UiKit.Box("Side", r, UiKit.Panel, TR, new Vector2(-40f, -30f), new Vector2(740f, 1020f));
            Transform s = side.transform;
            revZones = UiKit.Label("Zones", s, "", 25, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(20f, -16f), new Vector2(700f, 330f));
            revPlayers = UiKit.Label("Players", s, "", 24, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(20f, -350f), new Vector2(700f, 330f));
            revUnlock = UiKit.Label("Unlock", s, "", 28, TextAnchor.UpperCenter, UiKit.Good, TL, new Vector2(20f, -690f), new Vector2(700f, 50f));
            revUnlock.fontStyle = FontStyle.Bold;
            tipsRoot = UiKit.Node("Tips", s, TL, new Vector2(20f, -745f), new Vector2(700f, 180f)).gameObject;
            revTips = UiKit.Label("TipsText", tipsRoot.transform, "", 24, TextAnchor.UpperLeft, UiKit.Ink, TL, Vector2.zero, new Vector2(700f, 70f));
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                tipButtons[i] = UiKit.Button("Tip" + i, tipsRoot.transform, "", TL, new Vector2(i * 175f, -80f), new Vector2(165f, 80f), () => TipPressed(idx), false, 20);
            }
            revHint = UiKit.Label("Hint", s, "", 24, TextAnchor.MiddleCenter, UiKit.Muted, BC, new Vector2(0f, 90f), new Vector2(700f, 40f));
            btnRevealNext = UiKit.Button("Next", s, "Дальше", BC, new Vector2(0f, 20f), new Vector2(300f, 66f), () => HostCmd(HostCommand.Skip, 0), true, 28);
        }

        void TipPressed(int index)
        {
            var me = PlayerAgent.Local;
            var m = Match.Instance;
            if (me == null || m == null) return;
            var shooters = LocalController.ShootersForTips(m);
            if (index < shooters.Count) me.CmdTip(shooters[index].Slot);
        }

        void BuildSummary(Transform root)
        {
            summaryRoot = UiKit.Backdrop("Summary", root, UiKit.Paper).gameObject;
            Transform s = summaryRoot.transform;
            var t = UiKit.Label("Title", s, "ИТОГИ ВЕЧЕРА", 72, TextAnchor.MiddleCenter, UiKit.Accent, TC, new Vector2(0f, -40f), new Vector2(1600f, 100f));
            t.fontStyle = FontStyle.Bold;
            sumAwards = UiKit.Label("Awards", s, "", 34, TextAnchor.UpperCenter, UiKit.Ink, TC, new Vector2(0f, -160f), new Vector2(1700f, 220f));
            var left = UiKit.Box("Players", s, UiKit.Panel, TL, new Vector2(120f, -400f), new Vector2(800f, 460f));
            sumPlayers = UiKit.Label("List", left.transform, "", 32, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(20f, -16f), new Vector2(760f, 430f));
            var right = UiKit.Box("Rounds", s, UiKit.Panel, TR, new Vector2(-120f, -400f), new Vector2(800f, 460f));
            sumRounds = UiKit.Label("List", right.transform, "", 26, TextAnchor.UpperLeft, UiKit.Ink, TL, new Vector2(20f, -16f), new Vector2(760f, 430f));
            btnSumAgain = UiKit.Button("Again", s, "Ещё вечер", BC, new Vector2(-330f, 50f), new Vector2(300f, 80f), () => HostCmd(HostCommand.Start, 0), true, 32);
            btnSumLobby = UiKit.Button("Lobby", s, "В лобби", BC, new Vector2(0f, 50f), new Vector2(300f, 80f), () => HostCmd(HostCommand.ToLobby, 0), false, 32);
            UiKit.Button("Leave", s, "Выйти в меню", BC, new Vector2(330f, 50f), new Vector2(300f, 80f), () => NetSession.Instance.Leave(), false, 28);
            UiKit.Button("Album", s, "Открыть полароиды", BR, new Vector2(-40f, 50f), new Vector2(330f, 70f),
                () => Application.OpenURL("file:///" + PolaroidSaver.Folder.Replace('\\', '/')), false, 24);
        }

        void BuildPause(Transform root)
        {
            pauseRoot = UiKit.Backdrop("Pause", root, UiKit.Shade).gameObject;
            var box = UiKit.Box("Box", pauseRoot.transform, UiKit.Panel, C, Vector2.zero, new Vector2(700f, 520f));
            Transform b = box.transform;
            UiKit.Label("Title", b, "Пауза", 56, TextAnchor.MiddleCenter, UiKit.Accent, TC, new Vector2(0f, -30f), new Vector2(600f, 80f)).fontStyle = FontStyle.Bold;
            UiKit.Label("Note", b, "Игра идёт дальше — это не настоящая пауза", 22, TextAnchor.MiddleCenter, UiKit.Muted, TC, new Vector2(0f, -100f), new Vector2(600f, 40f));
            UiKit.Button("Continue", b, "Продолжить", TC, new Vector2(0f, -170f), new Vector2(420f, 80f), () => pauseOpen = false, true, 32);
            UiKit.Label("SensL", b, "Чувствительность мыши", 26, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -275f), new Vector2(600f, 40f));
            UiKit.Button("SensMinus", b, "−", TC, new Vector2(-170f, -330f), new Vector2(80f, 64f), () => LocalController.Sensitivity -= 0.1f, false, 34);
            sensText = UiKit.Label("Sens", b, "", 36, TextAnchor.MiddleCenter, UiKit.Ink, TC, new Vector2(0f, -330f), new Vector2(200f, 64f));
            UiKit.Button("SensPlus", b, "+", TC, new Vector2(170f, -330f), new Vector2(80f, 64f), () => LocalController.Sensitivity += 0.1f, false, 34);
            UiKit.Button("Leave", b, "Выйти в меню", TC, new Vector2(0f, -425f), new Vector2(420f, 76f), () => { pauseOpen = false; NetSession.Instance.Leave(); }, false, 30);
            pauseRoot.SetActive(false);
        }

        // ================= обновление =================

        void Update()
        {
            BindMatch();
            var net = NetSession.Instance;
            var m = Match.Instance;
            var me = PlayerAgent.Local;
            var kb = Keyboard.current;
            bool online = net != null && net.State == NetSession.Phase.Online;
            bool connecting = net != null && (net.State == NetSession.Phase.Starting || net.State == NetSession.Phase.Searching || net.State == NetSession.Phase.Connecting);
            bool ready = online && m != null && me != null && me.Slot != 0;

            if (kb != null && kb.escapeKey.wasPressedThisFrame && ready) pauseOpen = !pauseOpen;
            if (!ready) pauseOpen = false;

            MatchState state = m != null ? m.State : MatchState.Lobby;
            bool isModel = ready && me.IsModelNow;
            bool showMenu = !online && !connecting;
            bool showConnect = connecting || (online && !ready);
            bool showLobby = ready && state == MatchState.Lobby;
            bool showPick = ready && state == MatchState.Pick && isModel && m.PickOptions.Length > 0;
            bool showHud = ready && (state == MatchState.Pick || state == MatchState.Countdown || state == MatchState.Shoot) && !showPick;
            bool showReveal = ready && state == MatchState.Reveal && m.HasReveal && Time.time - revealArrivedAt > 1.8f;
            bool showSummary = ready && state == MatchState.Summary && m.HasSummary;

            SetActive(menuRoot, showMenu);
            SetActive(connectRoot, showConnect);
            SetActive(lobbyRoot, showLobby);
            SetActive(pickRoot, showPick);
            SetActive(hudRoot, showHud);
            SetActive(revealRoot, showReveal);
            SetActive(summaryRoot, showSummary);
            SetActive(pauseRoot, pauseOpen);
            if (!showPick) pickSent = false;

            if (showMenu) UpdateMenu(net);
            if (showConnect) connectText.text = online ? "Загружаем лофт…" : (net != null ? net.Message : "");
            if (showLobby) UpdateLobby(net, m, me);
            if (showPick) UpdatePick(m, kb);
            if (showHud) UpdateHud(m, me, isModel);
            if (showReveal) UpdateReveal(m, me);
            if (showReveal && !polaroidSaved) SavePolaroid(m);
            if (showSummary) UpdateSummary(m, me);
            if (pauseOpen) sensText.text = LocalController.Sensitivity.ToString("0.0");

            UpdateBanner();
            UpdateMirror(m, isModel && showHud);

            bool typing = false;
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null)
            {
                var field = es.currentSelectedGameObject.GetComponent<InputField>();
                typing = field != null && field.isFocused;
            }
            BlocksGameplay = pauseOpen || typing || showMenu || showConnect || showLobby || showPick || showSummary || (showReveal && isModel);
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void BindMatch()
        {
            var m = Match.Instance;
            if (m == boundMatch) return;
            if (boundMatch != null)
            {
                boundMatch.BannerShown -= OnBanner;
                boundMatch.FeedAdded -= OnFeed;
                boundMatch.Revealed -= OnRevealed;
                boundMatch.VisionGranted -= OnVision;
                boundMatch.PickOffered -= OnPickOffered;
            }
            boundMatch = m;
            if (m != null)
            {
                m.BannerShown += OnBanner;
                m.FeedAdded += OnFeed;
                m.Revealed += OnRevealed;
                m.VisionGranted += OnVision;
                m.PickOffered += OnPickOffered;
            }
        }

        void OnBanner(string text, byte style, float seconds)
        {
            Banner(text, style, seconds);
        }

        public void Banner(string text, byte style, float seconds)
        {
            bannerText.text = text;
            bannerImage.color = style == 1 ? UiKit.Bad : (style == 2 ? new Color(0.36f, 0.62f, 0.95f) : UiKit.Accent);
            bannerRoot.SetActive(true);
            bannerLeft = seconds;
        }

        void UpdateBanner()
        {
            if (bannerLeft <= 0f) return;
            bannerLeft -= Time.unscaledDeltaTime;
            if (bannerLeft <= 0f) bannerRoot.SetActive(false);
        }

        void OnFeed(string text)
        {
            feed.Add(text);
            feedTimes.Add(Time.time);
            while (feed.Count > 6) { feed.RemoveAt(0); feedTimes.RemoveAt(0); }
        }

        void OnRevealed(RevealPacket p)
        {
            revealArrivedAt = Time.time;
            polaroidSaved = false;
        }

        /// <summary>Сохранить полароид раунда картинкой 9:16 (один раз за раскрытие).</summary>
        void SavePolaroid(Match m)
        {
            polaroidSaved = true;
            if (!PolaroidSaver.Enabled || saver == null) return;
            var w = World.Instance;
            RevealPacket p = m.LastReveal;
            LocationDef loc = w != null && p.location < w.locations.Length ? w.locations[p.location] : null;
            ReferenceSet set = loc != null && p.option < loc.pool.Length ? loc.pool[p.option] : null;
            if (set == null) return;
            var results = new Texture[] { w.Face(0) != null ? w.Face(0).Texture : null, w.Face(1) != null ? w.Face(1).Texture : null };
            string file = saver.Save(p, set, results, Preview, loc.displayName);
            if (file != null) OnFeed("Полароид сохранён: Изображения / MakeupSniper");
        }

        void OnVision(float seconds, byte option)
        {
            visionUntil = Time.time + seconds;
            visionOption = option;
        }

        void OnPickOffered()
        {
            pickSent = false;
            bet = 50;
        }

        // ---------- меню ----------

        void UpdateMenu(NetSession net)
        {
            // имя по умолчанию — из Steam, если игрок его не вводил
            if (string.IsNullOrEmpty(nameField.text) && !nameField.isFocused && net != null && net.steam != null && !string.IsNullOrEmpty(net.steam.PersonaName))
                nameField.text = net.steam.PersonaName;
            menuStatus.text = net != null && net.State == NetSession.Phase.Failed ? net.Message : (net != null ? net.Message : "");
            var steam = net != null ? net.steam : null;
            steamStatus.text = steam != null ? steam.Status : "";
        }

        // ---------- лобби ----------

        void UpdateLobby(NetSession net, Match m, PlayerAgent me)
        {
            var w = World.Instance;
            bool host = net.IsHost;
            lobbyTitle.text = net.IsPractice ? "ТРЕНИРОВКА" : "ЛОББИ";
            SetActive(lobbyCodesPanel, host && !net.IsPractice);
            if (host && !net.IsPractice)
            {
                lobbySteamCode.text = string.IsNullOrEmpty(net.SteamCode) ? "—" : JoinCode.Pretty(net.SteamCode);
                lobbySteamHint.text = net.SteamCodeStatus;
                lobbyDirectCode.text = string.IsNullOrEmpty(net.DirectCode) ? "—" : JoinCode.Pretty(net.DirectCode);
                lobbyDirectHint.text = string.IsNullOrEmpty(net.DirectCode) ? "Сеть не найдена" :
                    net.CurrentAdapter.Label + " · " + net.CurrentAdapter.Address + "\nДруг в той же сети или в той же сети Radmin VPN вводит этот код";
                btnOtherNet.gameObject.SetActive(net.Adapters.Count > 1);
            }

            var sb = new StringBuilder();
            foreach (var a in SortedPlayers())
            {
                sb.Append(UiKit.ColorTag("●", PlayerColors.Of(a.Slot))).Append(' ').Append(a.DisplayName);
                if (a.IsHostPlayer) sb.Append(UiKit.ColorTag("  хост", UiKit.Muted));
                if (a == me) sb.Append(UiKit.ColorTag("  (ты)", UiKit.Muted));
                sb.Append('\n');
            }
            lobbyPlayers.text = sb.ToString();

            int li = m.LocationIndex;
            LocationDef loc = w != null && li < w.locations.Length ? w.locations[li] : null;
            if (loc != null)
            {
                bool unlocked = m.IsUnlocked(li);
                var ls = new StringBuilder();
                ls.Append("<size=44><b>").Append(loc.displayName).Append("</b></size>\n");
                ls.Append(UiKit.ColorTag(UiKit.Stars(Mathf.Min(m.StarsAt(li), 9), Mathf.Max(Progression.UnlockStars, Mathf.Min(m.StarsAt(li), 9))), UiKit.Gold)).Append("\n\n");
                ls.Append(loc.description).Append("\n\n");
                ls.Append(UiKit.ColorTag("Раунд " + Mathf.RoundToInt(loc.seconds) + " секунд · место " + (li + 1) + " из " + w.locations.Length, UiKit.Muted));
                if (!unlocked) ls.Append("\n").Append(UiKit.ColorTag("Закрыто: нужно " + Progression.UnlockStars + " ★ в предыдущем месте", UiKit.Bad));
                lobbyLocation.text = ls.ToString();
            }
            int count = SortedPlayers().Count;
            lobbyRounds.text = count <= 1 ? "Раундов: " + Match.SoloRounds + " (Модель — бот)" : "Каждый в кресле: " + m.RoundsPerPlayer + " раз(а)";
            btnPrevLoc.gameObject.SetActive(host);
            btnNextLoc.gameObject.SetActive(host);
            btnRoundsMinus.gameObject.SetActive(host && count > 1);
            btnRoundsPlus.gameObject.SetActive(host && count > 1);
            btnStart.gameObject.SetActive(host);
            btnPrevLoc.interactable = li > 0;
            btnNextLoc.interactable = w != null && li + 1 < w.locations.Length && m.IsUnlocked(li + 1);
            if (!host) lobbyHint.text = "Хост выбирает место и начинает. Голос — через Discord: Модель описывает, вы стреляете.";
            else if (net.IsPractice) lobbyHint.text = "Тренировка: ты стрелок, Модель — бот. Образ откроется только на раскрытии.";
            else lobbyHint.text = count < 2 ? "Скажи другу код. Пока ты один, Моделью будет бот." : "Все в сборе? Жми НАЧАТЬ. Каждый посидит в кресле Модели.";
        }

        static List<PlayerAgent> SortedPlayers()
        {
            var list = new List<PlayerAgent>();
            foreach (var a in PlayerAgent.All) if (a != null && a.Slot != 0) list.Add(a);
            list.Sort((x, y) => x.Slot.CompareTo(y.Slot));
            return list;
        }

        // ---------- выбор образа ----------

        void UpdatePick(Match m, Keyboard kb)
        {
            LocationDef loc = m.Location;
            for (int i = 0; i < 3; i++)
            {
                bool has = loc != null && i < m.PickOptions.Length && m.PickOptions[i] < loc.pool.Length;
                SetActive(pickCards[i], has);
                if (!has) continue;
                ReferenceSet set = loc.pool[m.PickOptions[i]];
                FillFaces(set, pickImages[i, 0], pickImages[i, 1], 380f, 240f);
                pickNames[i].text = set.Title;
                pickTaboo[i].text = "Нельзя говорить: " + TabooOf(set);
            }
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) SendPick(0);
                if (kb.digit2Key.wasPressedThisFrame) SendPick(1);
                if (kb.digit3Key.wasPressedThisFrame) SendPick(2);
                if (kb.leftArrowKey.wasPressedThisFrame) bet = Mathf.Clamp(bet - 10, 0, 100);
                if (kb.rightArrowKey.wasPressedThisFrame) bet = Mathf.Clamp(bet + 10, 0, 100);
            }
            betText.text = bet + "%";
            pickTimer.text = pickSent ? "Выбор отправлен…" : "Осталось " + Mathf.CeilToInt(m.SecondsLeft) + " с";
        }

        static string TabooOf(ReferenceSet set)
        {
            var words = new List<string>();
            foreach (var f in set.faces)
                if (f != null) foreach (var t in f.taboo) if (!words.Contains(t)) words.Add(t);
            return words.Count == 0 ? "—" : string.Join(", ", words);
        }

        void FillFaces(ReferenceSet set, RawImage big, RawImage second, float bigSize, float smallSize)
        {
            bool two = set.faces.Length > 1 && set.faces[1] != null;
            big.texture = Preview(set.faces.Length > 0 ? set.faces[0] : null);
            var rt = big.rectTransform;
            rt.sizeDelta = two ? new Vector2(smallSize, smallSize) : new Vector2(bigSize, bigSize);
            rt.anchoredPosition = two ? new Vector2(-smallSize * 0.52f, rt.anchoredPosition.y) : new Vector2(0f, rt.anchoredPosition.y);
            SetActive(second.gameObject, two);
            if (two)
            {
                second.texture = Preview(set.faces[1]);
                second.rectTransform.sizeDelta = new Vector2(smallSize, smallSize);
                second.rectTransform.anchoredPosition = new Vector2(smallSize * 0.52f, rt.anchoredPosition.y);
            }
        }

        public Texture2D Preview(ReferenceData r)
        {
            if (r == null) return null;
            Texture2D t;
            if (!previews.TryGetValue(r, out t) || t == null)
            {
                var w = World.Instance;
                float radius = w != null && w.faces.Length > 0 ? w.faces[0].FaceRadiusUv : 0.3f / 0.7f;
                t = FaceArt.CreateReferencePreview(r, 512, radius);
                previews[r] = t;
            }
            return t;
        }

        // ---------- раунд ----------

        void UpdateHud(Match m, PlayerAgent me, bool isModel)
        {
            var w = World.Instance;
            var lc = LocalController.Instance;
            MatchState state = m.State;
            LocationDef loc = m.Location;
            float left = m.SecondsLeft;

            timerText.text = state == MatchState.Shoot ? Mathf.CeilToInt(left).ToString() : (loc != null ? Mathf.RoundToInt(loc.seconds).ToString() : "");
            timerText.color = state == MatchState.Shoot && left <= 10f ? new Color(1f, 0.82f, 0.4f) : Color.white;
            roundText.text = "Раунд " + (m.Round + 1) + " из " + m.RoundsTotal + (loc != null ? " · " + loc.displayName : "");

            bool countdown = state == MatchState.Countdown;
            countdownText.gameObject.SetActive(countdown || (state == MatchState.Shoot && m.RoundTime < 0.8f));
            countdownText.text = countdown ? Mathf.Max(1, Mathf.CeilToInt(left)).ToString() : "КРАСЬ!";

            bool waiting = state == MatchState.Pick && !isModel;
            SetActive(waitRoot, waiting);
            if (waiting) waitText.text = (m.BotModel ? "Бот-Модель" : Match.NameOf(m.ModelSlot)) + " выбирает образ… Пока займи позицию: 3, 8 или 15 м";

            // подозрение свидетелей
            bool witness = loc != null && (loc.gimmick == Gimmick.Witness || loc.gimmick == Gimmick.Wedding) && state == MatchState.Shoot;
            SetActive(suspicionRoot, witness);
            if (witness)
            {
                bool looking = w != null && w.Witnesses != null && w.Witnesses.AnyLooking(m.Seed, m.RoundSeconds, m.RoundTime);
                float soon = w != null && w.Witnesses != null ? w.Witnesses.SecondsUntilLook(m.Seed, m.RoundSeconds, m.RoundTime) : 99f;
                string who = loc.gimmick == Gimmick.Wedding ? "Гости" : "Девушка";
                suspicionText.text = looking ? UiKit.ColorTag(who.ToUpperInvariant() + " СМОТРИТ! НЕ СТРЕЛЯЙ", UiKit.Bad)
                    : (soon < 1.5f ? who + " вот-вот обернётся…" : "Подозрение: " + Mathf.RoundToInt(m.Suspicion) + "%");
                suspicionFill.rectTransform.sizeDelta = new Vector2(480f * Mathf.Clamp01(m.Suspicion / 100f), 14f);
            }

            // стрелки и Модель видят разное
            var sb = new StringBuilder();
            float reloadFrac = 1f;
            bool splattered = !isModel && me.SplatSecondsLeft > 0f;
            if (isModel)
            {
                sb.Append("<b>Ты МОДЕЛЬ</b> в кресле\n");
                sb.Append("Ладонь: ").Append(m.PalmLeft).Append(" · Размазать: ").Append(m.SmearLeft).Append('\n');
                sb.Append("Уворот: ").Append(m.DodgeReady ? UiKit.ColorTag("готов", UiKit.Good) : UiKit.ColorTag("перезарядка", UiKit.Muted));
                hintText.text = "Мышь — повернуть голову · ПКМ — закрыться ладонью · Пробел — уворот · E — размазать ладонью\nОписывай образ голосом (Discord), но без запретных слов. Esc — пауза";
            }
            else if (w != null && w.weapons.Length > 0 && lc != null)
            {
                float z = lc.LineZ;
                WeaponDef wd = w.weapons[Mathf.Clamp(lc.WeaponIndex, 0, w.weapons.Length - 1)];
                sb.Append("Линия ").Append(World.LineName(z)).Append(" · множитель <b>×").Append(World.LineMultiplier(z)).Append("</b>\n");
                sb.Append("<b>").Append(wd.displayName).Append("</b>");
                if (wd.eraser && wd.charges > 0) sb.Append(" · зарядов ").Append(me.Charges);
                sb.Append('\n');
                float rl = lc.ReloadLeft(lc.WeaponIndex);
                if (lc.SwitchLeft > 0f) { sb.Append(UiKit.ColorTag("смена ствола…", UiKit.Muted)); reloadFrac = 1f - lc.SwitchLeft / Match.SwitchSeconds; }
                else if (rl > 0f) { sb.Append(UiKit.ColorTag("перезарядка " + rl.ToString("0.0") + " с", UiKit.Muted)); reloadFrac = 1f - rl / Mathf.Max(0.01f, wd.reload); }
                else sb.Append(UiKit.ColorTag("готово", UiKit.Good));
                if (splattered) sb.Append("\n").Append(UiKit.ColorTag("ЗАБРЫЗГАН: " + Mathf.CeilToInt(me.SplatSecondsLeft) + " с", UiKit.Bad));
                hintText.text = "ЛКМ — выстрел · ПКМ — зум (помада) · 1 помада · 2 тушь · 3 румяна · 4 тональник-ластик · колесо — ствол\nWASD — ходить, Shift — бегом · T — Модель сказала запретное слово (−5 с) · Esc — пауза";
            }
            infoText.text = sb.ToString();
            reloadFill.rectTransform.sizeDelta = new Vector2(528f * Mathf.Clamp01(reloadFrac), 12f);

            var ps = new StringBuilder();
            foreach (var a in SortedPlayers())
            {
                ps.Append(UiKit.ColorTag("●", PlayerColors.Of(a.Slot))).Append(' ').Append(a.DisplayName);
                if (a.Slot == m.ModelSlot) ps.Append(UiKit.ColorTag(" — Модель", UiKit.Accent));
                ps.Append("  ").Append(UiKit.ColorTag(a.Coins + " мон.", UiKit.Muted)).Append('\n');
            }
            if (m.BotModel) ps.Append(UiKit.ColorTag("● Бот-Модель", UiKit.Muted));
            playersText.text = ps.ToString();

            var fs = new StringBuilder();
            for (int i = 0; i < feed.Count; i++) if (Time.time - feedTimes[i] < 9f) fs.Append(feed[i]).Append('\n');
            feedText.text = fs.ToString();

            bool shooterView = !isModel && state != MatchState.Pick;
            SetActive(crosshair, shooterView && !splattered);
            SetActive(scope, shooterView && lc != null && lc.Zoomed);

            // телефон Модели
            bool phone = isModel && (state == MatchState.Countdown || state == MatchState.Shoot) && m.KnownOption >= 0 && loc != null && m.KnownOption < loc.pool.Length;
            SetActive(phoneRoot, phone);
            if (phone)
            {
                ReferenceSet set = loc.pool[m.KnownOption];
                FillFaces(set, phoneImages[0], phoneImages[1], 390f, 190f);
                phoneName.text = set.Title;
                phoneTaboo.text = "Нельзя говорить:\n" + TabooOf(set);
            }

            // видение забрызганного
            bool vision = !isModel && Time.time < visionUntil && loc != null && visionOption >= 0 && visionOption < loc.pool.Length;
            SetActive(visionRoot, vision || splattered);
            if (vision || splattered)
            {
                if (vision)
                {
                    ReferenceSet set = loc.pool[visionOption];
                    visionImages[0].gameObject.SetActive(true);
                    visionImages[0].texture = Preview(set.faces[0]);
                    bool two = set.faces.Length > 1;
                    visionImages[1].gameObject.SetActive(two);
                    if (two) visionImages[1].texture = Preview(set.faces[1]);
                    visionText.text = "ЗАБРЫЗГАН! ВИДЕНИЕ: вот образ — " + set.Title.ToUpperInvariant() + "\nПерескажи остальным!";
                }
                else
                {
                    visionImages[0].gameObject.SetActive(false);
                    visionImages[1].gameObject.SetActive(false);
                    visionText.text = "ЗАБРЫЗГАН! Ничего не видно…";
                }
            }
        }

        void UpdateMirror(Match m, bool isModelHud)
        {
            var w = World.Instance;
            bool open = isModelHud && m != null && m.MirrorOpen;
            SetActive(mirrorRoot, open);
            if (w != null && w.mirrorCamera != null)
            {
                if (w.mirrorCamera.enabled != open) w.mirrorCamera.enabled = open;
                if (open) mirrorImage.texture = w.mirrorTexture;
            }
        }

        // ---------- раскрытие ----------

        void UpdateReveal(Match m, PlayerAgent me)
        {
            var w = World.Instance;
            RevealPacket p = m.LastReveal;
            LocationDef loc = w != null && p.location < w.locations.Length ? w.locations[p.location] : null;
            ReferenceSet set = loc != null && p.option < loc.pool.Length ? loc.pool[p.option] : null;
            int faces = set != null ? Mathf.Min(2, set.faces.Length) : 1;
            polaroidRect.sizeDelta = new Vector2(1060f, faces > 1 ? 1020f : 760f);
            for (int f = 0; f < 2; f++)
            {
                bool on = f < faces;
                SetActive(revRef[f].gameObject, on);
                SetActive(revRes[f].gameObject, on);
                SetActive(revRefLabel[f].gameObject, on);
                SetActive(revResLabel[f].gameObject, on);
                if (!on) continue;
                revRef[f].texture = set != null ? Preview(set.faces[f]) : null;
                PaintSurface face = w != null ? w.Face(f) : null;
                revRes[f].texture = face != null ? (Texture)face.Texture : null;
                revRefLabel[f].text = faces > 1 ? "РЕФЕРЕНС: " + set.faces[f].displayName.ToUpperInvariant() : "РЕФЕРЕНС";
                int fm = p.faceMatch != null && f < p.faceMatch.Length ? p.faceMatch[f] : p.match;
                revResLabel[f].text = "РЕЗУЛЬТАТ " + fm + "%";
                float size = faces > 1 ? 360f : 500f;
                revRef[f].rectTransform.sizeDelta = revRes[f].rectTransform.sizeDelta = new Vector2(size, size);
                if (faces == 1)
                {
                    revRef[f].rectTransform.anchoredPosition = new Vector2(20f, -60f);
                    revRes[f].rectTransform.anchoredPosition = new Vector2(540f, -60f);
                    revRefLabel[f].rectTransform.anchoredPosition = new Vector2(70f, -575f);
                    revResLabel[f].rectTransform.anchoredPosition = new Vector2(590f, -575f);
                }
                else
                {
                    float y = f == 0 ? -20f : -450f;
                    revRef[f].rectTransform.anchoredPosition = new Vector2(40f, y);
                    revRes[f].rectTransform.anchoredPosition = new Vector2(460f, y);
                    revRefLabel[f].rectTransform.anchoredPosition = new Vector2(20f, y - 365f);
                    revResLabel[f].rectTransform.anchoredPosition = new Vector2(440f, y - 365f);
                    revRefLabel[f].rectTransform.sizeDelta = revResLabel[f].rectTransform.sizeDelta = new Vector2(400f, 44f);
                }
            }
            revStamp.text = p.match + "%";
            revStars.text = UiKit.Stars(p.stars);
            revHeadline.text = p.headline + (p.modelSlot != 0 ? "\nМодель: " + p.modelName + (p.betWon ? " · ставка " + p.bet + "% сыграла!" : " · ставка " + p.bet + "%") : "\nМодель: бот");

            var zs = new StringBuilder();
            zs.Append("<b>Зоны</b>\n");
            int lastFace = -1;
            if (p.zones != null)
            {
                foreach (var z in p.zones)
                {
                    if (faces > 1 && z.face != lastFace) { lastFace = z.face; zs.Append("<b>").Append(set.faces[z.face].displayName).Append("</b>\n"); }
                    zs.Append(z.ok ? UiKit.ColorTag("ЕСТЬ ", UiKit.Good) : UiKit.ColorTag("МИМО ", UiKit.Bad));
                    zs.Append(z.name).Append(" (").Append(PaintColors.RussianName((PaintColor)z.color)).Append(") ").Append(z.coverage).Append("%");
                    if (!z.ok && z.blame != 0) zs.Append(" — виноват ").Append(UiKit.ColorTag(PlayerColors.NameOf(z.blame), PlayerColors.Of(z.blame)));
                    zs.Append('\n');
                }
            }
            if (p.noticed) zs.Append(UiKit.ColorTag("ЗАМЕТИЛИ: −30%", UiKit.Bad)).Append('\n');
            revZones.text = zs.ToString();

            var ps = new StringBuilder();
            ps.Append("<b>Очки раунда</b>\n");
            if (p.players != null)
            {
                foreach (var pl in p.players)
                {
                    var agent = Match.BySlot(pl.slot);
                    int coins = agent != null ? agent.Coins : pl.coinsTotal;
                    ps.Append(UiKit.ColorTag("●", PlayerColors.Of(pl.slot))).Append(' ').Append(pl.name).Append(": <b>+").Append(pl.points).Append("</b>  ");
                    ps.Append(UiKit.ColorTag("(" + pl.note + ")", UiKit.Muted)).Append("  всего ").Append(coins).Append('\n');
                }
            }
            revPlayers.text = ps.ToString();
            revUnlock.text = p.unlock ?? "";

            bool iAmModel = me.Slot == p.modelSlot && p.modelSlot != 0;
            var shooters = LocalController.ShootersForTips(m);
            bool tipsOpen = m.TipsLeft > 0 && m.SecondsLeft > Match.RevealSeconds - Match.TipsSeconds;
            SetActive(tipsRoot, p.modelSlot != 0);
            if (p.modelSlot != 0)
            {
                if (iAmModel)
                    revTips.text = tipsOpen ? "<b>Раздай чаевые</b>: осталось " + m.TipsLeft + " монет. Жми кнопку или 1–" + Mathf.Min(4, shooters.Count) + " (по " + Match.TipStep + ")"
                                            : "Чаевые розданы";
                else
                    revTips.text = tipsOpen ? "Модель раздаёт чаевые… осталось " + m.TipsLeft : "Чаевые розданы";
                for (int i = 0; i < tipButtons.Length; i++)
                {
                    bool show = iAmModel && tipsOpen && i < shooters.Count;
                    SetActive(tipButtons[i].gameObject, show);
                    if (show) UiKit.SetButtonText(tipButtons[i], (i + 1) + ") +" + Match.TipStep + "\n" + shooters[i].DisplayName);
                }
            }
            var kb = Keyboard.current;
            if (kb != null && iAmModel && tipsOpen)
            {
                if (kb.digit1Key.wasPressedThisFrame) TipPressed(0);
                if (kb.digit2Key.wasPressedThisFrame) TipPressed(1);
                if (kb.digit3Key.wasPressedThisFrame) TipPressed(2);
                if (kb.digit4Key.wasPressedThisFrame) TipPressed(3);
            }
            bool host = me.IsHostPlayer;
            if (kb != null && host && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) HostCmd(HostCommand.Skip, 0);
            SetActive(btnRevealNext.gameObject, host);
            revHint.text = host ? "Enter — дальше (или подожди " + Mathf.CeilToInt(m.SecondsLeft) + " с)" : "Дальше через " + Mathf.CeilToInt(m.SecondsLeft) + " с";
        }

        // ---------- итоги ----------

        void UpdateSummary(Match m, PlayerAgent me)
        {
            SummaryPacket s = m.LastSummary;
            sumAwards.text = "Визажист вечера: <b>" + s.bestArtist + "</b>\nЛицо вечера: <b>" + s.bestFace + "</b>\nХудшее лицо вечера: <b>" + s.worstFace + "</b>";
            var ps = new StringBuilder();
            ps.Append("<b>Монеты</b>\n");
            int place = 1;
            if (s.players != null)
                foreach (var pl in s.players)
                    ps.Append(place++).Append(". ").Append(UiKit.ColorTag("●", PlayerColors.Of(pl.slot))).Append(' ').Append(pl.name).Append(" — <b>").Append(pl.coinsTotal).Append("</b>\n");
            sumPlayers.text = ps.ToString();
            var rs = new StringBuilder();
            rs.Append("<b>Альбом полароидов</b>\n");
            if (s.rounds != null)
                foreach (var r in s.rounds)
                    rs.Append(r.modelName).Append(": ").Append(r.title).Append(" — <b>").Append(r.match).Append("%</b>\n");
            sumRounds.text = rs.ToString();
            bool host = me.IsHostPlayer;
            SetActive(btnSumAgain.gameObject, host);
            SetActive(btnSumLobby.gameObject, host);
        }
    }
}
