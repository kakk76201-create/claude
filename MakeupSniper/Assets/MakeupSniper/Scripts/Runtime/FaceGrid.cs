using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Логическая сетка лица 64×64: в каждой ячейке класс цвета и владелец (кто покрасил).
    /// Счёт считается только по ней, картинка на лице — отдельное представление.
    /// Координаты uv: (0,0) слева внизу холста, (1,1) справа вверху.
    /// </summary>
    public sealed class FaceGrid
    {
        public const int Size = 64;

        public readonly PaintColor[] Colors = new PaintColor[Size * Size];
        public readonly byte[] Owners = new byte[Size * Size];

        public void Clear()
        {
            System.Array.Clear(Colors, 0, Colors.Length);
            System.Array.Clear(Owners, 0, Owners.Length);
        }

        public static Vector2 CellCenter(int index)
        {
            return new Vector2(((index % Size) + 0.5f) / Size, ((index / Size) + 0.5f) / Size);
        }

        /// <summary>Штамп отражается в сетке как диск. Возвращает число затронутых ячеек.</summary>
        public int StampDisc(Vector2 uv, float radiusUv, PaintColor color, byte owner)
        {
            float rg = radiusUv * Size;
            float cx = uv.x * Size - 0.5f, cy = uv.y * Size - 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rg)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cx + rg));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - rg)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(cy + rg));
            int touched = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > rg * rg) continue;
                    int i = y * Size + x;
                    Colors[i] = color;
                    Owners[i] = owner;
                    touched++;
                }
            }
            return touched;
        }

        public int PaintedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Colors.Length; i++) if (Colors[i] != PaintColor.None) n++;
                return n;
            }
        }
    }
}
