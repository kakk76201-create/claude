using UnityEngine;

namespace MakeupSniper
{
    public enum WeaponKind { Hitscan, Projectile }

    /// <summary>Описание ствола. Создаётся как ассет, числа можно менять в инспекторе Unity.</summary>
    [CreateAssetMenu(menuName = "Makeup Sniper/Weapon", fileName = "Weapon")]
    public class WeaponDef : ScriptableObject
    {
        public string displayName = "Ствол";
        [Tooltip("Короткое имя для интерфейса")]
        public string shortName = "ствол";
        [Tooltip("Hitscan — мгновенный луч (снайперка). Projectile — снаряд по параболе.")]
        public WeaponKind kind = WeaponKind.Hitscan;
        public PaintColor color = PaintColor.Red;
        [Tooltip("Ластик: возвращает чистую кожу вместо краски")]
        public bool eraser;
        public BrushKind brush = BrushKind.Kiss;
        [Tooltip("Радиус пятна на лице, метры (у полосы — половина ширины)")]
        public float spotRadius = 0.015f;
        [Tooltip("Длина полосы, метры (только для кисти Stripe)")]
        public float stripeLength = 0f;
        [Tooltip("Перезарядка, секунды")]
        public float reload = 1.5f;
        [Tooltip("Зарядов за раунд, −1 — без ограничений")]
        public int charges = -1;
        public bool canZoom;
        [Tooltip("Угол обзора при зуме (60 без зума; 15 — это зум ×4)")]
        public float zoomFov = 15f;
        [Tooltip("Разброс без зума, градусы")]
        public float spreadDeg = 0.35f;
        [Tooltip("Разброс в зуме, градусы")]
        public float spreadZoomDeg = 0.08f;
        [Tooltip("Скорость снаряда, м/с (только для Projectile)")]
        public float projectileSpeed = 24f;
        [Tooltip("Доля земного притяжения для снаряда")]
        public float gravityScale = 1f;
        [Tooltip("Размер снаряда на экране, метры")]
        public float projectileSize = 0.16f;
        [Tooltip("Попадание в другого стрелка забрызгивает его")]
        public bool splattersPlayers = true;
        [Tooltip("Цвет корпуса ствола")]
        public Color bodyColor = new Color(0.95f, 0.79f, 0.65f);
    }
}
