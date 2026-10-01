using System;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Простое рисование на процессоре в массив пикселей. Координаты uv: (0,0) слева внизу.
    /// Нужен, чтобы лицо, кисти и картинки референсов делались кодом, без внешних ассетов.
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Size;
        public readonly Color32[] Pixels;

        public PixelCanvas(int size, Color32 fill)
        {
            Size = size;
            Pixels = new Color32[size * size];
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = fill;
        }

        public PixelCanvas(int size, Color32[] source)
        {
            Size = size;
            Pixels = (Color32[])source.Clone();
        }

        /// <summary>
        /// Плавный переход 0→1, когда x идёт от edge0 до edge1 (как smoothstep в шейдерах).
        /// Внимание: Mathf.SmoothStep в Unity устроен иначе, поэтому здесь своя функция.
        /// </summary>
        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Наложить цвет поверх пикселя с прозрачностью alpha (0..1). Альфа пикселя тоже растёт.</summary>
        public void Blend(int x, int y, Color32 c, float alpha)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || alpha <= 0f) return;
            if (alpha > 1f) alpha = 1f;
            int i = y * Size + x;
            Color32 d = Pixels[i];
            float da = d.a / 255f;
            float outA = alpha + da * (1f - alpha);
            if (outA <= 0f) return;
            float wS = alpha / outA, wD = da * (1f - alpha) / outA;
            Pixels[i] = new Color32(
                (byte)Mathf.RoundToInt(c.r * wS + d.r * wD),
                (byte)Mathf.RoundToInt(c.g * wS + d.g * wD),
                (byte)Mathf.RoundToInt(c.b * wS + d.b * wD),
                (byte)Mathf.RoundToInt(outA * 255f));
        }

        /// <summary>
        /// Нарисовать фигуру. alphaFn получает локальные координаты (-1..1 по каждой оси) и возвращает плотность 0..1.
        /// </summary>
        public void BlendShape(Vector2 center, Vector2 halfSize, float rotationDeg, Color32 color, float opacity, Func<float, float, float> alphaFn)
        {
            float ext = Mathf.Max(halfSize.x, halfSize.y) * 1.45f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((center.x - ext) * Size)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt((center.x + ext) * Size));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((center.y - ext) * Size)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt((center.y + ext) * Size));
            float rad = -rotationDeg * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float px = (x + 0.5f) / Size - center.x, py = (y + 0.5f) / Size - center.y;
                    float lx = (px * cs - py * sn) / halfSize.x, ly = (px * sn + py * cs) / halfSize.y;
                    if (lx < -1.02f || lx > 1.02f || ly < -1.02f || ly > 1.02f) continue;
                    float a = alphaFn(lx, ly);
                    if (a > 0f) Blend(x, y, color, a * opacity);
                }
            }
        }

        /// <summary>Эллипс. softness — доля радиуса, на которой край размывается.</summary>
        public void Ellipse(Vector2 center, Vector2 radius, Color32 color, float opacity, float softness)
        {
            float s = Mathf.Max(softness, 1.5f / (Mathf.Min(radius.x, radius.y) * Size));
            BlendShape(center, radius, 0f, color, opacity, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y);
                return 1f - Smooth(1f - s, 1f, d);
            });
        }

        /// <summary>Линия по точкам (кружками вдоль отрезков).</summary>
        public void Stroke(Vector2[] points, float widthUv, Color32 color)
        {
            Vector2 r = new Vector2(widthUv * 0.5f, widthUv * 0.5f);
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / (widthUv * 0.3f)));
                for (int s = 0; s <= steps; s++) Ellipse(Vector2.Lerp(a, b, (float)s / steps), r, color, 1f, 0.3f);
            }
        }

        public Texture2D ToTexture(string name, bool mipmaps)
        {
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, mipmaps, false);
            t.name = name;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(Pixels);
            t.Apply(mipmaps, false);
            return t;
        }
    }
}
