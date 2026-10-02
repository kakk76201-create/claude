using UnityEngine;

namespace MakeupSniper
{
    /// <summary>Класс цвета краски. По нему считается счёт (точные оттенки не важны).</summary>
    public enum PaintColor : byte { None = 0, Red = 1, Pink = 2, Black = 3 }

    public static class PaintColors
    {
        public static Color32 ToColor(PaintColor c)
        {
            switch (c)
            {
                case PaintColor.Red: return new Color32(215, 38, 61, 255);
                case PaintColor.Pink: return new Color32(255, 143, 177, 255);
                case PaintColor.Black: return new Color32(34, 24, 28, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }

        public static string RussianName(PaintColor c)
        {
            switch (c)
            {
                case PaintColor.Red: return "красный";
                case PaintColor.Pink: return "розовый";
                case PaintColor.Black: return "чёрный";
                default: return "чисто";
            }
        }
    }
}
