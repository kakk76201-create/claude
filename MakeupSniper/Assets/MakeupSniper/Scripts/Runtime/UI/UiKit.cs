using System;
using UnityEngine;
using UnityEngine.UI;

namespace MakeupSniper
{
    /// <summary>Помощники для сборки интерфейса кодом (без префабов). Координаты — в пикселях экрана 1920×1080.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color(0.23f, 0.14f, 0.19f);
        public static readonly Color Muted = new Color(0.55f, 0.42f, 0.48f);
        public static readonly Color Accent = new Color(0.90f, 0.24f, 0.44f);
        public static readonly Color AccentDark = new Color(0.71f, 0.15f, 0.33f);
        public static readonly Color Panel = new Color(1f, 0.98f, 0.99f, 0.92f);
        public static readonly Color Paper = new Color(0.97f, 0.91f, 0.93f, 0.97f);
        public static readonly Color Line = new Color(0.93f, 0.82f, 0.87f);
        public static readonly Color Good = new Color(0.18f, 0.61f, 0.41f);
        public static readonly Color Bad = new Color(0.84f, 0.29f, 0.29f);
        public static readonly Color Gold = new Color(0.88f, 0.66f, 0.11f);
        public static readonly Color Shade = new Color(0.23f, 0.14f, 0.19f, 0.62f);

        static Font font;
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Растянуть на весь родитель.</summary>
        public static RectTransform Fill(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Box(string name, Transform parent, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Node(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Backdrop(string name, Transform parent, Color color)
        {
            var rt = Fill(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true; // ловит клики, чтобы они не уходили в игру
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, TextAnchor align, Color color, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var rt = Node(name, parent, anchor, pos, box);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.alignment = align; t.color = color; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true; t.raycastTarget = false;
            return t;
        }

        public static RawImage Picture(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Node(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            return img;
        }

        public static Button Button(string name, Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, bool primary = true, int fontSize = 30)
        {
            var img = Box(name, parent, primary ? Accent : Panel, anchor, pos, size);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.96f);
            colors.pressedColor = new Color(0.85f, 0.8f, 0.82f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            b.colors = colors;
            b.targetGraphic = img;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = primary ? AccentDark : Line;
            outline.effectDistance = new Vector2(0f, -4f);
            var label = Label("Text", img.transform, text, fontSize, TextAnchor.MiddleCenter, primary ? Color.white : Ink, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(16f, 4f));
            label.fontStyle = FontStyle.Bold;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static InputField Input(string name, Transform parent, string placeholder, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize = 34)
        {
            var img = Box(name, parent, Color.white, anchor, pos, size);
            img.raycastTarget = true;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(3f, -3f);
            var ph = Label("Placeholder", img.transform, placeholder, fontSize, TextAnchor.MiddleCenter, Muted, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 8f));
            ph.fontStyle = FontStyle.Italic;
            var tx = Label("Text", img.transform, "", fontSize, TextAnchor.MiddleCenter, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(24f, 8f));
            tx.supportRichText = false;
            var field = img.gameObject.AddComponent<InputField>();
            field.textComponent = tx;
            field.placeholder = ph;
            field.targetGraphic = img;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        public static Outline AddOutline(Graphic g, Color c, float distance = 3f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(distance, -distance);
            return o;
        }

        public static string ColorTag(string text, Color c)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";
        }

        public static string Stars(int n, int max = 3)
        {
            return new string('★', Mathf.Clamp(n, 0, max)) + new string('☆', Mathf.Max(0, max - n));
        }
    }
}
