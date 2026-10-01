using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MakeupSniper
{
    /// <summary>
    /// Весь интерфейс недели 1, собранный кодом (без префабов): таймер, прицел, подсказки,
    /// выбор референса, «телефон» Модели и экран раскрытия с полароидом.
    /// Полировки нет намеренно: это рабочий отладочный вид.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.23f, 0.14f, 0.19f);
        static readonly Color Accent = new Color(0.90f, 0.24f, 0.44f);
        static readonly Color Panel = new Color(1f, 0.98f, 0.99f, 0.88f);
        static readonly Color Paper = new Color(0.97f, 0.91f, 0.93f, 0.96f);

        public Canvas Canvas { get; private set; }

        Font font;
        GameObject hudRoot, crosshair, pickRoot, phoneRoot, revealRoot, bannerRoot;
        Text timerText, infoText, hintText, roleText, bannerText, countdownText;
        RawImage[] pickImages; Text[] pickNames;
        RawImage phoneImage; Text phoneName;
        RawImage revealRef, revealResult; Text revealStamp, revealList, revealTitle;
        float bannerLeft;
        bool built;

        void Awake() { Build(); }

        void Update()
        {
            if (bannerLeft > 0f)
            {
                bannerLeft -= Time.unscaledDeltaTime;
                if (bannerLeft <= 0f && bannerRoot != null) bannerRoot.SetActive(false);
            }
        }

        // ---------- сборка ----------

        public void Build()
        {
            if (built) return;
            built = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cgo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            Canvas = cgo.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = cgo.transform;

            // HUD
            hudRoot = Node("HUD", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
            timerText = Label("Timer", hudRoot.transform, "60", 84, TextAnchor.UpperCenter, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(320f, 110f));
            timerText.fontStyle = FontStyle.Bold;
            AddOutline(timerText, Accent);

            var infoBox = Box("InfoBox", hudRoot.transform, Panel, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(640f, 150f));
            infoText = Label("Info", infoBox.transform, "", 27, TextAnchor.UpperLeft, Ink, new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(610f, 130f));

            var roleBox = Box("RoleBox", hudRoot.transform, Panel, new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(560f, 56f));
            roleText = Label("Role", roleBox.transform, "", 26, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 50f));

            var hintBox = Box("HintBox", hudRoot.transform, Panel, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(1000f, 96f));
            hintText = Label("Hint", hintBox.transform, "", 22, TextAnchor.UpperLeft, Ink, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(975f, 80f));

            crosshair = Node("Crosshair", hudRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f)).gameObject;
            Box("V", crosshair.transform, Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 36f));
            Box("H", crosshair.transform, Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 3f));
            Box("Dot", crosshair.transform, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));

            countdownText = Label("Countdown", root, "", 260, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 360f));
            countdownText.fontStyle = FontStyle.Bold;
            AddOutline(countdownText, Accent);
            countdownText.gameObject.SetActive(false);

            var bannerImg = Box("Banner", root, Accent, new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(660f, 96f));
            bannerRoot = bannerImg.gameObject;
            bannerRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            bannerText = Label("Text", bannerRoot.transform, "", 58, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(650f, 92f));
            bannerText.fontStyle = FontStyle.Bold;
            bannerRoot.SetActive(false);

            // «телефон» Модели
            var phone = Box("Phone", root, Color.white, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(350f, 500f));
            phoneRoot = phone.gameObject;
            phoneImage = Picture("Ref", phoneRoot.transform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(310f, 356f));
            phoneName = Label("Name", phoneRoot.transform, "", 30, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(330f, 44f));
            phoneName.fontStyle = FontStyle.Bold;
            Label("Help", phoneRoot.transform, "Ты Модель. Опиши это словами.\nМышь — голова. Tab — к стрелку.", 19, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(336f, 56f));
            phoneRoot.SetActive(false);

            // выбор референса
            var pick = Box("Pick", root, Paper, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
            pickRoot = pick.gameObject;
            var pt = Label("Title", pickRoot.transform, "MAKEUP SNIPER · выбери референс", 54, TextAnchor.MiddleCenter, Accent, new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(1600f, 80f));
            pt.fontStyle = FontStyle.Bold;
            pickImages = new RawImage[3]; pickNames = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var card = Box("Card" + i, pickRoot.transform, Color.white, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 430f, 60f), new Vector2(380f, 560f));
                pickImages[i] = Picture("Ref", card.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(340f, 391f));
                pickNames[i] = Label("Name", card.transform, "", 32, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(360f, 50f));
                pickNames[i].fontStyle = FontStyle.Bold;
                var key = Label("Key", card.transform, "клавиша " + (i + 1), 30, TextAnchor.MiddleCenter, Accent, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(360f, 50f));
                key.fontStyle = FontStyle.Bold;
            }
            Label("Help", pickRoot.transform,
                "1 / 2 / 3 — выбрать референс. Ты видишь его как Модель, потом Tab переключает на стрелка.\nПробел — случайный секретный референс: сразу стреляешь, картинка откроется на раскрытии.",
                28, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(1700f, 120f));
            pickRoot.SetActive(false);

            // раскрытие
            var rev = Box("Reveal", root, new Color(0.23f, 0.14f, 0.19f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4000f, 4000f));
            revealRoot = rev.gameObject;
            var polaroid = Box("Polaroid", revealRoot.transform, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1180f, 900f));
            revealRef = Picture("Reference", polaroid.transform, new Vector2(0.5f, 1f), new Vector2(-280f, -30f), new Vector2(420f, 483f));
            revealResult = Picture("Result", polaroid.transform, new Vector2(0.5f, 1f), new Vector2(280f, -30f), new Vector2(420f, 483f));
            var l1 = Label("L1", polaroid.transform, "РЕФЕРЕНС", 36, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 1f), new Vector2(-280f, -520f), new Vector2(420f, 50f)); l1.fontStyle = FontStyle.BoldAndItalic;
            var l2 = Label("L2", polaroid.transform, "РЕЗУЛЬТАТ", 36, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 1f), new Vector2(280f, -520f), new Vector2(420f, 50f)); l2.fontStyle = FontStyle.BoldAndItalic;
            revealTitle = Label("Title", polaroid.transform, "", 26, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -572f), new Vector2(1100f, 40f));
            revealList = Label("List", polaroid.transform, "", 28, TextAnchor.UpperLeft, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -618f), new Vector2(1060f, 270f));
            var stampBox = Box("Stamp", polaroid.transform, new Color(1f, 1f, 1f, 0.92f), new Vector2(1f, 1f), new Vector2(-10f, 34f), new Vector2(250f, 120f));
            stampBox.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            revealStamp = Label("Pct", stampBox.transform, "0%", 92, TextAnchor.MiddleCenter, Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 120f));
            revealStamp.fontStyle = FontStyle.Bold;
            var rh = Label("Help", revealRoot.transform, "R или Enter — ещё раунд", 30, TextAnchor.MiddleCenter, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, -490f), new Vector2(900f, 50f));
            rh.fontStyle = FontStyle.Bold;
            revealRoot.SetActive(false);

            hudRoot.SetActive(false);
        }

        // ---------- управление ----------

        public void ShowPick(Texture[] previews, string[] names)
        {
            pickRoot.SetActive(true);
            for (int i = 0; i < pickImages.Length; i++)
            {
                bool has = i < previews.Length;
                pickImages[i].transform.parent.gameObject.SetActive(has);
                if (!has) continue;
                pickImages[i].texture = previews[i];
                pickNames[i].text = names[i];
            }
        }

        public void HidePick() { pickRoot.SetActive(false); }

        public void ShowHud(bool on) { hudRoot.SetActive(on); }

        public void SetCrosshair(bool on) { crosshair.SetActive(on); }

        public void SetTimer(int seconds, bool hurry)
        {
            timerText.text = seconds.ToString();
            timerText.color = hurry ? new Color(1f, 0.82f, 0.4f) : Color.white;
        }

        public void SetInfo(string text) { infoText.text = text; }
        public void SetRole(string text) { roleText.text = text; }
        public void SetHint(string text) { hintText.text = text; }

        public void ShowCountdown(string text)
        {
            countdownText.gameObject.SetActive(!string.IsNullOrEmpty(text));
            countdownText.text = text ?? "";
        }

        public void Banner(string text, float seconds)
        {
            bannerText.text = text;
            bannerRoot.SetActive(true);
            bannerLeft = seconds;
        }

        public void ShowPhone(bool on, Texture preview, string name)
        {
            phoneRoot.SetActive(on);
            if (!on) return;
            phoneImage.texture = preview;
            phoneName.text = name;
        }

        public void ShowReveal(Texture reference, Texture result, ScoreResult score, string title)
        {
            revealRoot.SetActive(true);
            revealRef.texture = reference;
            revealResult.texture = result;
            revealStamp.text = score.matchPercent + "%";
            revealTitle.text = title;
            var sb = new StringBuilder();
            foreach (var z in score.zones)
            {
                string mark = z.ok ? "<color=#2e9c68><b>ЕСТЬ</b></color>" : "<color=#d64a4a><b>МИМО</b></color>";
                sb.Append(mark).Append("   ").Append(z.name).Append(" (").Append(PaintColors.RussianName(z.color)).Append(") — ")
                  .Append(Mathf.RoundToInt(z.coverage * 100f)).Append("%\n");
            }
            if (score.overshootCells > 0)
                sb.Append("<color=#d64a4a>Залёт мимо зон: −").Append(Mathf.RoundToInt(score.penaltyPercent)).Append("%</color>\n");
            revealList.text = sb.ToString();
        }

        public void HideReveal() { revealRoot.SetActive(false); }

        // ---------- помощники ----------

        static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        // anchor — точка привязки и одновременно опорная точка элемента (0..1)
        static Image Box(string name, Transform parent, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Node(name, parent, anchor, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text Label(string name, Transform parent, string text, int size, TextAnchor align, Color color, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = Node(name, parent, anchor, anchor, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = align; t.color = color; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true; t.raycastTarget = false;
            return t;
        }

        static RawImage Picture(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Node(name, parent, anchor, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        static void AddOutline(Text t, Color c)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(3f, -3f);
        }
    }
}
