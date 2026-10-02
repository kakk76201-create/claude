using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Детский праздник: дети носятся между креслом и линией 3 м и ловят выстрелы.
    /// Положение — чистая функция от времени раунда, поэтому у всех игроков дети в одном месте.
    /// </summary>
    public class KidsGimmick : MonoBehaviour
    {
        public Transform[] kids = new Transform[0];

        public void Pose(int seed, float roundTime, bool running)
        {
            for (int i = 0; i < kids.Length; i++)
            {
                if (kids[i] == null) continue;
                float t = running ? roundTime : 0f;
                Vector3 p = GimmickMath.KidPosition(seed, i, t);
                Vector3 ahead = GimmickMath.KidPosition(seed, i, t + 0.05f);
                p.y = GimmickMath.KidHop(i, t);
                kids[i].localPosition = p;
                Vector3 d = ahead - p; d.y = 0f;
                if (d.sqrMagnitude > 1e-6f) kids[i].localRotation = Quaternion.LookRotation(d);
            }
        }

        public bool IsKid(Collider c)
        {
            foreach (var k in kids) if (k != null && c.transform.IsChildOf(k)) return true;
            return false;
        }
    }

}
