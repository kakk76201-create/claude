namespace MakeupSniper
{
    /// <summary>
    /// Описание одного мазка краски. Сервер рассылает его всем игрокам, и каждый рисует у себя
    /// одно и то же: поэтому лицо у всех одинаковое.
    /// </summary>
    public struct StampData
    {
        /// <summary>Какое лицо: 0 — Модель, 1 — жених на свадьбе.</summary>
        public byte face;
        public float u;
        public float v;
        /// <summary>Радиус пятна в метрах (у полосы — половина ширины).</summary>
        public float radius;
        /// <summary>Длина полосы в метрах (для туши и мазка ладонью).</summary>
        public float length;
        public byte color;
        public byte brush;
        /// <summary>Поворот кисти в градусах на холсте лица (0° — вправо).</summary>
        public float angle;
        /// <summary>Чей мазок: номер игрока 1–4, 200 — сама Модель.</summary>
        public byte owner;
        /// <summary>Множитель очков выстрела (дистанция).</summary>
        public byte mult;

        public PaintColor Color { get { return (PaintColor)color; } }
        public BrushKind Brush { get { return (BrushKind)brush; } }
    }
}
