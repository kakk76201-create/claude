using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Свидетели: девушка на свидании или гости на свадьбе. Иногда оборачиваются к Модели.
    /// Пока кто-то смотрит, стрелять нельзя: растёт подозрение, на 100% — «ЗАМЕТИЛА!».
    /// </summary>
    public class WitnessGimmick : MonoBehaviour
    {
        [Tooltip("Головы свидетелей (поворачиваются)")]
        public Transform[] heads = new Transform[0];
        [Tooltip("Корни свидетелей (включаются по числу)")]
        public GameObject[] roots = new GameObject[0];
        public float awayYaw = 160f;
        [Tooltip("Наклон головы, когда не смотрит (вниз, в телефон)")]
        public float awayPitch = 0f;

        int count = 1;
        int cachedSeed = int.MinValue;
        float cachedDuration;
        readonly List<List<GimmickMath.Window>> schedules = new List<List<GimmickMath.Window>>();

        public void SetCount(int n)
        {
            count = Mathf.Clamp(n, 0, roots.Length);
            for (int i = 0; i < roots.Length; i++) if (roots[i] != null) roots[i].SetActive(i < count);
        }

        void EnsureSchedule(int seed, float duration)
        {
            if (seed == cachedSeed && Mathf.Approximately(duration, cachedDuration) && schedules.Count == heads.Length) return;
            cachedSeed = seed; cachedDuration = duration;
            schedules.Clear();
            for (int i = 0; i < heads.Length; i++) schedules.Add(GimmickMath.WitnessWindows(seed, duration, i));
        }

        /// <summary>Смотрит ли кто-нибудь на Модель в момент t.</summary>
        public bool AnyLooking(int seed, float duration, float t)
        {
            if (!gameObject.activeInHierarchy) return false;
            EnsureSchedule(seed, duration);
            for (int i = 0; i < count && i < schedules.Count; i++) if (GimmickMath.IsInside(schedules[i], t)) return true;
            return false;
        }

        /// <summary>Через сколько секунд кто-то обернётся (для подсказки стрелкам).</summary>
        public float SecondsUntilLook(int seed, float duration, float t)
        {
            EnsureSchedule(seed, duration);
            float best = float.MaxValue;
            for (int i = 0; i < count && i < schedules.Count; i++) best = Mathf.Min(best, GimmickMath.SecondsUntilNext(schedules[i], t));
            return best;
        }

        public void Pose(int seed, float duration, float t, bool running)
        {
            EnsureSchedule(seed, duration);
            for (int i = 0; i < heads.Length; i++)
            {
                if (heads[i] == null) continue;
                bool looking = running && i < schedules.Count && GimmickMath.IsInside(schedules[i], t);
                Quaternion target = Quaternion.Euler(looking ? 0f : awayPitch, looking ? LookYaw(i) : awayYaw, 0f);
                heads[i].localRotation = Quaternion.Slerp(heads[i].localRotation, target, Mathf.Min(1f, Time.deltaTime * 8f));
            }
        }

        float LookYaw(int i)
        {
            // повернуться к креслу Модели (начало координат)
            Transform h = heads[i];
            if (h == null || h.parent == null) return 0f;
            Vector3 toModel = h.parent.InverseTransformPoint(new Vector3(0f, h.position.y, 0f)) - h.localPosition;
            return Mathf.Atan2(toModel.x, toModel.z) * Mathf.Rad2Deg;
        }

        public bool IsWitness(Collider c)
        {
            foreach (var r in roots) if (r != null && r.activeInHierarchy && c.transform.IsChildOf(r.transform)) return true;
            return false;
        }
    }
}
