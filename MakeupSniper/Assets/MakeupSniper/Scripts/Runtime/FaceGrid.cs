using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Логическая сетка лица 64×64: в каждой ячейке класс цвета, владелец (кто покрасил) и множитель выстрела.
    /// Счёт считается только по ней, картинка на лице — отдельное представление.
    /// Координаты uv: (0,0) слева внизу холста, (1,1) справа вверху.
    /// </summary>
    public sealed class FaceGrid
    {
        public const int Size = 64;
        /// <summary>Владелец «Модель» — для размазанной ладонью краски.</summary>
        public const byte ModelOwner = 200;

        public readonly PaintColor[] Colors = new PaintColor[Size * Size];
        public readonly byte[] Owners = new byte[Size * Size];
        public readonly byte[] Mults = new byte[Size * Size];

        public void Clear()
        {
            System.Array.Clear(Colors, 0, Colors.Length);
            System.Array.Clear(Owners, 0, Owners.Length);
            System.Array.Clear(Mults, 0, Mults.Length);
        }

        public static Vector2 CellCenter(int index)
        {
            return new Vector2(((index % Size) + 0.5f) / Size, ((index / Size) + 0.5f) / Size);
        }

        void Set(int i, PaintColor color, byte owner, byte mult)
        {
            // у стёртой ячейки тоже запоминаем, кто стёр: ластик получает очки за зоны «стереть»
            Colors[i] = color;
            Owners[i] = owner;
            Mults[i] = mult;
        }

        /// <summary>Диск радиусом radiusUv. PaintColor.None стирает. Возвращает число затронутых ячеек.</summary>
        public int StampDisc(Vector2 uv, float radiusUv, PaintColor color, byte owner, byte mult = 1)
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
                    Set(y * Size + x, color, owner, mult);
                    touched++;
                }
            }
            return touched;
        }

        /// <summary>
        /// Полоса (как от туши): отрезок длиной lengthUv и половиной ширины halfWidthUv, повёрнутый на angleDeg
        /// (0° — вправо, против часовой стрелки).
        /// </summary>
        public int StampStripe(Vector2 uv, float lengthUv, float halfWidthUv, float angleDeg, PaintColor color, byte owner, byte mult = 1)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float halfLen = lengthUv * 0.5f;
            float ext = halfLen + halfWidthUv;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((uv.x - ext) * Size)), x1 = Mathf.Min(Size - 1, Mathf.CeilToInt((uv.x + ext) * Size));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((uv.y - ext) * Size)), y1 = Mathf.Min(Size - 1, Mathf.CeilToInt((uv.y + ext) * Size));
            // ячейка считается задетой, если её центр ближе половины ширины (+ полклетки) к отрезку
            float hw = halfWidthUv + 0.5f / Size;
            int touched = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 c = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size) - uv;
                    float along = Vector2.Dot(c, dir);
                    float clamped = Mathf.Clamp(along, -halfLen, halfLen);
                    Vector2 closest = dir * clamped;
                    if ((c - closest).sqrMagnitude > hw * hw) continue;
                    Set(y * Size + x, color, owner, mult);
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

        /// <summary>Отпечаток сетки для сравнения двух компьютеров (одинаковое лицо → одинаковое число).</summary>
        public uint Hash()
        {
            uint h = 2166136261;
            for (int i = 0; i < Colors.Length; i++)
            {
                h = (h ^ (byte)Colors[i]) * 16777619;
                h = (h ^ Owners[i]) * 16777619;
            }
            return h;
        }

        public void CopyFrom(FaceGrid other)
        {
            System.Array.Copy(other.Colors, Colors, Colors.Length);
            System.Array.Copy(other.Owners, Owners, Owners.Length);
            System.Array.Copy(other.Mults, Mults, Mults.Length);
        }
    }
}
