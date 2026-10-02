using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    public enum Gimmick { None, Kids, Witness, Wedding }

    /// <summary>Вариант заказа: одно лицо или два (на свадьбе — невеста и жених).</summary>
    [Serializable]
    public class ReferenceSet
    {
        public ReferenceData[] faces = new ReferenceData[0];

        public string Title
        {
            get
            {
                var names = new List<string>();
                foreach (var f in faces) if (f != null) names.Add(f.displayName);
                return string.Join(" + ", names);
            }
        }
    }

    /// <summary>Место, куда едет бригада визажистов: свои заказы, своя помеха, своё время.</summary>
    [CreateAssetMenu(menuName = "Makeup Sniper/Location", fileName = "Location")]
    public class LocationDef : ScriptableObject
    {
        public string displayName = "Лофт";
        [TextArea] public string description = "";
        public float seconds = 60f;
        public Gimmick gimmick = Gimmick.None;
        [Tooltip("Имя объекта с декорациями этого места в сцене")]
        public string propRoot = "Loft";
        public Color wallColor = new Color(0.84f, 0.94f, 0.89f);
        public ReferenceSet[] pool = new ReferenceSet[0];
    }

    /// <summary>Расписания помех. Чистые функции от зерна и времени: у всех компьютеров получается одно и то же.</summary>
    public static class GimmickMath
    {
        public struct Window { public float start, end; }

        /// <summary>Когда свидетельница (гость) оборачивается: окна по 1,8–3,6 с каждые 5–12 с.</summary>
        public static List<Window> WitnessWindows(int seed, float duration, int witnessIndex)
        {
            var rng = new System.Random(seed * 31 + witnessIndex * 7919);
            var list = new List<Window>();
            float t = 4f + (float)rng.NextDouble() * 4f;
            while (t < duration)
            {
                float len = 1.8f + (float)rng.NextDouble() * 1.8f;
                list.Add(new Window { start = t, end = Mathf.Min(duration, t + len) });
                t += len + 5f + (float)rng.NextDouble() * 7f;
            }
            return list;
        }

        public static bool IsInside(List<Window> windows, float t)
        {
            if (windows == null) return false;
            foreach (var w in windows) if (t >= w.start && t < w.end) return true;
            return false;
        }

        /// <summary>Сколько секунд осталось до поворота (для подсказки «сейчас обернётся»).</summary>
        public static float SecondsUntilNext(List<Window> windows, float t)
        {
            if (windows == null) return float.MaxValue;
            foreach (var w in windows) if (w.start > t) return w.start - t;
            return float.MaxValue;
        }

        /// <summary>Где бегает ребёнок номер i в момент t (между креслом и линией 3 м).</summary>
        public static Vector3 KidPosition(int seed, int i, float t)
        {
            float speed = 1.1f + 0.45f * i + 0.15f * ((seed >> i) & 3);
            float phase = i * 2.1f + (seed % 7) * 0.37f;
            float span = 3.4f;
            // туда-обратно по ширине комнаты, у каждого своя скорость и своя «дорожка» по глубине
            float s = Mathf.PingPong(t * speed + phase * span, span * 2f) - span;
            float z = 1.15f + i * 0.55f + 0.12f * Mathf.Sin(t * 1.7f + i);
            return new Vector3(s, 0f, z);
        }

        public static float KidHop(int i, float t)
        {
            return Mathf.Abs(Mathf.Sin(t * 6.5f + i * 1.3f)) * 0.08f;
        }
    }
}
