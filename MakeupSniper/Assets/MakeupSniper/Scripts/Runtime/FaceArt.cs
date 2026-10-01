using UnityEngine;

namespace MakeupSniper
{
    /// <summary>Форма кисти. Kiss — отпечаток губ, Blush — мягкое пятно с брызгами, Smudge — неровная клякса.</summary>
    public enum BrushKind { Kiss, Blush, Smudge }

    /// <summary>
    /// Всё, что рисуется кодом: базовое лицо, кисти макияжа, картинки референсов.
    /// Благодаря этому макияж выглядит как помада и румяна, а не как ровные круги.
    /// </summary>
    public static class FaceArt
    {
        public static readonly Color32 Skin = new Color32(251, 243, 239, 255);

        // Положение черт лица на холсте (uv). Совпадает с зонами референсов.
        public static readonly Vector2 EyeRightPos = new Vector2(0.39f, 0.60f);   // правый глаз Модели (слева на картинке)
        public static readonly Vector2 EyeLeftPos = new Vector2(0.61f, 0.60f);
        public static readonly Vector2 EyeRadius = new Vector2(0.07f, 0.06f);

        // ---------- формы ----------

        static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        static float Sat(float v) { return Mathf.Clamp01(v); }

        /// <summary>Губы. x,y в -1..1; губы занимают всю ширину и примерно ±0.75 по высоте.</summary>
        public static float LipsAlpha(float x, float y, bool creases)
        {
            float ax = Mathf.Abs(x);
            if (ax >= 1f) return 0f;
            float k = 1f - ax * ax;
            float top = 0.72f * Mathf.Pow(k, 0.6f) * (1f - 0.30f * Mathf.Exp(-(ax / 0.17f) * (ax / 0.17f)));
            float bottom = -0.86f * Mathf.Pow(k, 0.75f);
            float mid = 0.05f * k;
            float gap = 0.035f + 0.03f * k;
            const float e = 0.07f;
            float upper = Sat((top - y) / e) * Sat((y - (mid + gap)) / e);
            float lower = Sat((y - bottom) / e) * Sat(((mid - gap) - y) / e);
            float a = Mathf.Max(upper, lower);
            if (a <= 0f) return 0f;
            if (creases)
            {
                float lines = 0.70f + 0.30f * Mathf.Abs(Mathf.Sin(x * 13f + y * 2.5f));
                float grain = 0.80f + 0.20f * Hash(Mathf.Floor(x * 40f), Mathf.Floor(y * 40f));
                a *= lines * grain;
            }
            return a;
        }

        /// <summary>Мягкое пятно румян.</summary>
        public static float SoftAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return 1f - PixelCanvas.Smooth(0.25f, 1f, d);
        }

        /// <summary>Клякса с неровным краем.</summary>
        public static float BlobAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float th = Mathf.Atan2(y, x);
            float r = 0.86f + 0.07f * Mathf.Sin(3f * th + 1f) + 0.05f * Mathf.Sin(5f * th + 2.3f);
            return 1f - PixelCanvas.Smooth(r - 0.1f, r, d);
        }

        // ---------- базовое лицо ----------

        public static PixelCanvas DrawBaseFace(int size, float faceRadiusUv)
        {
            var c = new PixelCanvas(size, Skin);
            // лёгкий контур головы, чтобы на полароиде читался овал
            var outline = new Color32(226, 204, 212, 255);
            c.Ellipse(new Vector2(0.5f, 0.5f), new Vector2(faceRadiusUv + 0.004f, faceRadiusUv + 0.004f), outline, 1f, 0.01f);
            c.Ellipse(new Vector2(0.5f, 0.5f), new Vector2(faceRadiusUv - 0.004f, faceRadiusUv - 0.004f), Skin, 1f, 0.01f);

            var brow = new Color32(106, 74, 92, 255);
            var lineDark = new Color32(74, 48, 64, 255);
            foreach (var eye in new[] { EyeRightPos, EyeLeftPos })
            {
                // бровь: дуга над глазом
                var pts = new Vector2[9];
                for (int i = 0; i < pts.Length; i++)
                {
                    float a = Mathf.Lerp(140f, 40f, i / 8f) * Mathf.Deg2Rad;
                    pts[i] = new Vector2(eye.x + Mathf.Cos(a) * 0.075f, eye.y + 0.035f + Mathf.Sin(a) * 0.075f);
                }
                c.Stroke(pts, 0.012f, brow);
                // глаз
                c.Ellipse(eye, new Vector2(0.050f, 0.034f), lineDark, 1f, 0.05f);
                c.Ellipse(eye, new Vector2(0.044f, 0.028f), new Color32(255, 255, 255, 255), 1f, 0.05f);
                c.Ellipse(eye, new Vector2(0.017f, 0.017f), new Color32(42, 31, 42, 255), 1f, 0.1f);
                c.Ellipse(eye + new Vector2(-0.006f, 0.006f), new Vector2(0.005f, 0.005f), new Color32(255, 255, 255, 255), 1f, 0.2f);
            }
            // нос
            var nose = new Color32(169, 127, 146, 255);
            c.Stroke(new[]
            {
                new Vector2(0.470f, 0.500f), new Vector2(0.466f, 0.470f), new Vector2(0.475f, 0.447f), new Vector2(0.500f, 0.440f),
                new Vector2(0.525f, 0.447f), new Vector2(0.534f, 0.470f), new Vector2(0.530f, 0.500f)
            }, 0.008f, nose);
            // рот
            var mouth = new Color32(176, 106, 131, 255);
            var mp = new Vector2[11];
            for (int i = 0; i < mp.Length; i++)
            {
                float t = i / 10f, x = Mathf.Lerp(0.425f, 0.575f, t);
                mp[i] = new Vector2(x, 0.34f - 0.045f * (1f - (2f * t - 1f) * (2f * t - 1f)));
            }
            c.Stroke(mp, 0.009f, mouth);
            return c;
        }

        // ---------- кисти ----------

        /// <summary>Кисть с уже запечённым цветом: квадратная текстура, прозрачная вне формы.</summary>
        public static Texture2D CreateBrush(BrushKind kind, PaintColor color, int size)
        {
            Color32 col = PaintColors.ToColor(color);
            var c = new PixelCanvas(size, new Color32(col.r, col.g, col.b, 0));
            var mid = new Vector2(0.5f, 0.5f);
            switch (kind)
            {
                case BrushKind.Kiss:
                    c.BlendShape(mid, new Vector2(0.48f, 0.32f), 0f, col, 0.95f, (x, y) => LipsAlpha(x, y * 0.78f, true));
                    break;
                case BrushKind.Blush:
                    c.BlendShape(mid, new Vector2(0.34f, 0.34f), 0f, col, 0.92f, SoftAlpha);
                    for (int i = 0; i < 16; i++)
                    {
                        float a = Hash(i, 1f) * Mathf.PI * 2f, d = 0.30f + 0.17f * Hash(i, 2f), r = 0.012f + 0.022f * Hash(i, 3f);
                        c.Ellipse(mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, new Vector2(r, r), col, 0.8f, 0.25f);
                    }
                    break;
                default:
                    c.BlendShape(mid, new Vector2(0.46f, 0.46f), 0f, col, 0.96f, BlobAlpha);
                    break;
            }
            return c.ToTexture("Brush_" + kind + "_" + color, true);
        }

        /// <summary>Во сколько раз квадрат штампа больше радиуса пятна (у каждой кисти свои поля).</summary>
        public static float BrushScale(BrushKind kind)
        {
            switch (kind)
            {
                case BrushKind.Kiss: return 1.35f;
                case BrushKind.Blush: return 1.45f;
                default: return 1.1f;
            }
        }

        // ---------- картинка референса ----------

        public static Texture2D CreateReferencePreview(ReferenceData reference, int size, float faceRadiusUv)
        {
            var c = DrawBaseFace(size, faceRadiusUv);
            foreach (var z in reference.zones)
            {
                Color32 col = PaintColors.ToColor(z.color);
                switch (z.style)
                {
                    case ZoneStyle.Soft:
                        c.BlendShape(z.center, z.radius * 1.2f, 0f, col, 0.92f, SoftAlpha);
                        break;
                    case ZoneStyle.Lips:
                        c.BlendShape(z.center, new Vector2(z.radius.x, z.radius.y * 1.35f), 0f, col, 0.95f, (x, y) => LipsAlpha(x, y * 0.8f, false));
                        c.BlendShape(z.center + new Vector2(-z.radius.x * 0.3f, -z.radius.y * 0.45f), new Vector2(z.radius.x * 0.22f, z.radius.y * 0.2f), -15f,
                            new Color32(255, 255, 255, 255), 0.35f, SoftAlpha);
                        break;
                    case ZoneStyle.Kiss:
                        c.BlendShape(z.center, new Vector2(z.radius.x, z.radius.x * 0.66f), 20f, col, 0.95f, (x, y) => LipsAlpha(x, y * 0.78f, true));
                        break;
                    default:
                        c.BlendShape(z.center, z.radius, 0f, col, 0.94f, BlobAlpha);
                        c.BlendShape(z.center + new Vector2(-z.radius.x * 0.3f, z.radius.y * 0.3f), z.radius * 0.28f, -30f,
                            new Color32(255, 255, 255, 255), z.color == PaintColor.Black ? 0.12f : 0.4f, SoftAlpha);
                        break;
                }
            }
            return c.ToTexture("Reference_" + reference.name, false);
        }
    }
}
