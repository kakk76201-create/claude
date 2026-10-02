using System;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>Как зона нарисована на картинке референса (на счёт не влияет).</summary>
    public enum ZoneStyle { Solid, Soft, Lips, Kiss, Whiskers, Freckles, Tear, Patch, Stripes }

    /// <summary>Зона референса: эллипс на холсте лица и цвет, которым её надо закрасить.</summary>
    [Serializable]
    public class ZoneTarget
    {
        public string name = "Зона";
        [Tooltip("Центр зоны на холсте, 0..1. Лево/право — со стороны Модели.")]
        public Vector2 center = new Vector2(0.5f, 0.5f);
        [Tooltip("Полуоси эллипса на холсте, 0..1")]
        public Vector2 radius = new Vector2(0.06f, 0.06f);
        public PaintColor color = PaintColor.Red;
        public ZoneStyle style = ZoneStyle.Solid;
        [Tooltip("Какая доля зоны должна быть покрыта нужным цветом, чтобы зона засчиталась")]
        [Range(0.1f, 1f)]
        public float required = 0.7f;

        public bool Contains(Vector2 uv)
        {
            float dx = (uv.x - center.x) / radius.x, dy = (uv.y - center.y) / radius.y;
            return dx * dx + dy * dy <= 1f;
        }
    }

    /// <summary>Референс макияжа: имя, зоны и запретные слова. Создаётся как ассет (ScriptableObject).</summary>
    [CreateAssetMenu(menuName = "Makeup Sniper/Reference", fileName = "Reference")]
    public class ReferenceData : ScriptableObject
    {
        public string displayName = "Референс";
        public ZoneTarget[] zones = new ZoneTarget[0];
        [Tooltip("Слова, которые Модели нельзя произносить, описывая этот образ")]
        public string[] taboo = new string[0];
        [Tooltip("Грязь, которая уже есть на лице в начале раунда (вино, торт, засос). Её стирают тональником")]
        public ZoneTarget[] startDirt = new ZoneTarget[0];
    }
}
