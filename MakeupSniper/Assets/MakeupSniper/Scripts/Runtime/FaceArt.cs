using UnityEngine;

namespace MakeupSniper
{
    /// <summary>Форма кисти. Kiss — отпечаток губ, Blush — мягкое пятно с брызгами, Smudge — клякса,
    /// Stripe — полоса туши, Smear — мазок ладонью, Eraser — тональник (возвращает чистую кожу).</summary>
    public enum BrushKind { Kiss, Blush, Smudge, Stripe, Smear, Eraser }

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

        /// <summary>Полоса туши: вытянутая по x, с заострёнными концами и «щетинками».</summary>
        public static float StripeAlpha(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (ax >= 1f) return 0f;
            // к концам полоса сужается
            float width = 0.95f * Mathf.Pow(1f - ax * ax * ax, 0.5f);
            float a = 1f - PixelCanvas.Smooth(width - 0.18f, width, Mathf.Abs(y));
            float bristles = 0.78f + 0.22f * Mathf.Abs(Mathf.Sin(y * 23f + Mathf.Floor(x * 9f) * 1.7f));
            return a * bristles;
        }

        /// <summary>Мазок ладонью: широкая мягкая полоса, неровная по краям.</summary>
        public static float SmearAlpha(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (ax >= 1f) return 0f;
            float edge = 0.8f + 0.12f * Mathf.Sin(x * 11f) + 0.06f * Mathf.Sin(x * 29f + 1.3f);
            float a = 1f - PixelCanvas.Smooth(edge - 0.35f, edge, Mathf.Abs(y));
            float fade = 1f - PixelCanvas.Smooth(0.55f, 1f, ax);   // к концам мазок бледнеет
            float streaks = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(y * 17f));
            return a * fade * streaks;
        }

        /// <summary>Маска ластика: мягкий круг (сам цвет берётся из чистого лица шейдером).</summary>
        public static float EraserAlpha(float x, float y)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return 1f - PixelCanvas.Smooth(0.7f, 1f, d);
        }

        /// <summary>Кисть с уже запечённым цветом: квадратная текстура, прозрачная вне формы.</summary>
        public static Texture2D CreateBrush(BrushKind kind, PaintColor color, int size)
        {
            Color32 col = PaintColors.ToColor(color);
            if (kind == BrushKind.Eraser) col = new Color32(255, 255, 255, 255);
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
                case BrushKind.Stripe:
                    c.BlendShape(mid, new Vector2(0.49f, 0.49f), 0f, col, 0.96f, StripeAlpha);
                    break;
                case BrushKind.Smear:
                    c.BlendShape(mid, new Vector2(0.49f, 0.49f), 0f, col, 0.85f, SmearAlpha);
                    break;
                case BrushKind.Eraser:
                    c.BlendShape(mid, new Vector2(0.49f, 0.49f), 0f, col, 1f, EraserAlpha);
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
                case BrushKind.Eraser: return 1.05f;
                default: return 1.1f;
            }
        }

        /// <summary>Вытянутые кисти рисуются прямоугольником: длина × ширина.</summary>
        public static bool IsElongated(BrushKind kind)
        {
            return kind == BrushKind.Stripe || kind == BrushKind.Smear;
        }

        // ---------- картинка референса ----------

        public static Texture2D CreateReferencePreview(ReferenceData reference, int size, float faceRadiusUv)
        {
            var c = DrawBaseFace(size, faceRadiusUv);
            // грязь, которая будет на лице в начале раунда (её надо стереть)
            if (reference.startDirt != null)
                foreach (var d in reference.startDirt)
                {
                    Color32 dc = PaintColors.ToColor(d.color);
                    if (d.style == ZoneStyle.Kiss) c.BlendShape(d.center, new Vector2(d.radius.x, d.radius.x * 0.66f), 20f, dc, 0.55f, (x, y) => LipsAlpha(x, y * 0.78f, true));
                    else c.BlendShape(d.center, d.radius, 0f, dc, 0.55f, BlobAlpha);
                }
            foreach (var z in reference.zones)
            {
                Color32 col = PaintColors.ToColor(z.color);
                if (z.color == PaintColor.None)
                {
                    // «стереть»: голубое кольцо вокруг места, которое должно стать чистым
                    var ring = new Color32(70, 170, 235, 255);
                    c.BlendShape(z.center, z.radius, 0f, ring, 0.95f, (x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        bool dash = Mathf.Repeat(Mathf.Atan2(y, x) * 4f, 1.2f) < 0.75f;
                        return d > 0.86f && d < 1f && dash ? 1f : 0f;
                    });
                    continue;
                }
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
                    case ZoneStyle.Whiskers:
                    {
                        // три уса веером от центра зоны наружу (зона — щека)
                        float side = z.center.x < 0.5f ? -1f : 1f;
                        for (int k = -1; k <= 1; k++)
                        {
                            float ang = (side < 0f ? 180f : 0f) + k * 14f * side;
                            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                            Vector2 mid2 = z.center + dir * z.radius.x * 0.15f + new Vector2(0f, k * z.radius.y * 0.45f);
                            c.BlendShape(mid2, new Vector2(z.radius.x * 0.95f, 0.011f), ang + k * 6f * side, col, 0.95f, StripeAlpha);
                        }
                        break;
                    }
                    case ZoneStyle.Freckles:
                        for (int k = 0; k < 22; k++)
                        {
                            float fx = (Hash(k, 7f) * 2f - 1f) * 0.9f, fy = (Hash(k, 11f) * 2f - 1f) * 0.9f;
                            if (fx * fx + fy * fy > 0.85f) continue;
                            float fr = 0.006f + 0.006f * Hash(k, 13f);
                            c.Ellipse(z.center + new Vector2(fx * z.radius.x, fy * z.radius.y), new Vector2(fr, fr), col, 0.9f, 0.3f);
                        }
                        break;
                    case ZoneStyle.Tear:
                        // капля-слеза под глазом
                        c.BlendShape(z.center, new Vector2(z.radius.x * 0.6f, z.radius.y), 0f, col, 0.95f, (x, y) =>
                        {
                            float w = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01((y + 1f) * 0.5f));
                            return Mathf.Abs(x) < w ? 1f - PixelCanvas.Smooth(0.8f, 1f, Mathf.Sqrt(x * x / (w * w) * 0.6f + y * y * 0.5f)) : 0f;
                        });
                        break;
                    case ZoneStyle.Patch:
                    {
                        // пиратская повязка: круг на глазу и ремешок через лоб
                        c.BlendShape(z.center, z.radius, 0f, col, 0.97f, BlobAlpha);
                        Vector2 a1 = z.center + new Vector2(-z.radius.x * 0.7f, z.radius.y * 0.7f);
                        Vector2 a2 = new Vector2(0.5f - (z.center.x - 0.5f) * 1.6f, 0.80f);
                        c.Stroke(new[] { a1, Vector2.Lerp(a1, a2, 0.5f) + new Vector2(0f, 0.03f), a2 }, 0.012f, col);
                        Vector2 b1 = z.center + new Vector2(z.radius.x * 0.8f, z.radius.y * 0.3f);
                        c.Stroke(new[] { b1, new Vector2(z.center.x + z.radius.x * 2.2f, z.center.y + 0.02f) }, 0.012f, col);
                        break;
                    }
                    case ZoneStyle.Stripes:
                    {
                        // тигровые полоски: три наклонные полосы внутри зоны
                        for (int k = -1; k <= 1; k++)
                        {
                            Vector2 p = z.center + new Vector2(k * z.radius.x * 0.55f, 0f);
                            c.BlendShape(p, new Vector2(z.radius.y * 0.95f, z.radius.x * 0.16f), 75f + k * 8f, col, 0.95f, StripeAlpha);
                        }
                        break;
                    }
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
