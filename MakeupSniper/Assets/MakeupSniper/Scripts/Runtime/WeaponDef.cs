using UnityEngine;

namespace MakeupSniper
{
    public enum WeaponKind { Hitscan, Projectile }

    /// <summary>Описание ствола. Создаётся как ассет, числа можно менять в инспекторе Unity.</summary>
    [CreateAssetMenu(menuName = "Makeup Sniper/Weapon", fileName = "Weapon")]
    public class WeaponDef : ScriptableObject
    {
        public string displayName = "Ствол";
        [Tooltip("Hitscan — мгновенный луч (снайперка). Projectile — снаряд по параболе (базука).")]
        public WeaponKind kind = WeaponKind.Hitscan;
        public PaintColor color = PaintColor.Red;
        [Tooltip("Есть второй оттенок, переключается клавишей B")]
        public bool hasAltShade;
        public PaintColor altColor = PaintColor.Black;
        public BrushKind brush = BrushKind.Kiss;
        public BrushKind altBrush = BrushKind.Smudge;
        [Tooltip("Радиус пятна на лице, метры")]
        public float spotRadius = 0.015f;
        [Tooltip("Перезарядка, секунды")]
        public float reload = 1.5f;
        public bool canZoom;
        [Tooltip("Угол обзора при зуме (60 без зума; 15 — это зум ×4)")]
        public float zoomFov = 15f;
        [Tooltip("Разброс без зума, градусы")]
        public float spreadDeg = 0.35f;
        [Tooltip("Разброс в зуме, градусы")]
        public float spreadZoomDeg = 0.08f;
        [Tooltip("Скорость снаряда, м/с (только для Projectile)")]
        public float projectileSpeed = 24f;
    }
}
