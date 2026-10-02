using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Видимые эффекты выстрелов: снаряды в полёте, трассеры помады, кляксы на стенах и людях.
    /// На счёт не влияют — только картинка. Снаряды здесь летят «для глаз», попадание решает хост.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public Material redMaterial, pinkMaterial, blackMaterial, skinMaterial;
        public int maxSplats = 120;

        class Flying { public int id; public GameObject go; public Vector3 pos, vel; public float gravity, life; }
        class Timed { public GameObject go; public float life; }

        readonly List<Flying> flying = new List<Flying>();
        readonly List<Timed> tracers = new List<Timed>();
        readonly Queue<GameObject> splats = new Queue<GameObject>();

        public Material MaterialFor(PaintColor c)
        {
            switch (c)
            {
                case PaintColor.Pink: return pinkMaterial;
                case PaintColor.Black: return blackMaterial;
                case PaintColor.None: return skinMaterial;
                default: return redMaterial;
            }
        }

        public void Projectile(int id, Vector3 origin, Vector3 velocity, float gravity, PaintColor color, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile" + id;
            DestroyImmediate(go.GetComponent<Collider>());
            go.layer = 2; // Ignore Raycast: эффекты не мешают выстрелам
            go.transform.localScale = Vector3.one * size;
            go.transform.position = origin;
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            flying.Add(new Flying { id = id, go = go, pos = origin, vel = velocity, gravity = gravity, life = 5f });
        }

        public void EndProjectile(int id)
        {
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                if (flying[i].id != id) continue;
                Destroy(flying[i].go);
                flying.RemoveAt(i);
            }
        }

        public void Tracer(Vector3 a, Vector3 b, PaintColor color)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tracer";
            DestroyImmediate(go.GetComponent<Collider>());
            go.layer = 2; // Ignore Raycast: эффекты не мешают выстрелам
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(b - a);
            go.transform.localScale = new Vector3(0.012f, 0.012f, len);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            tracers.Add(new Timed { go = go, life = 0.09f });
        }

        /// <summary>Клякса: сплющенный шарик, прижатый к поверхности. parent — если клякса на движущемся человеке.</summary>
        public void Splat(Vector3 point, Vector3 normal, float radius, PaintColor color, Transform parent = null)
        {
            if (normal.sqrMagnitude < 1e-4f) normal = Vector3.up;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Splat";
            DestroyImmediate(go.GetComponent<Collider>());
            go.layer = 2; // Ignore Raycast: эффекты не мешают выстрелам
            go.transform.position = point + normal * 0.004f;
            go.transform.rotation = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f * Random.Range(0.75f, 1.1f), 0.012f);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            if (parent != null) go.transform.SetParent(parent, true);
            splats.Enqueue(go);
            while (splats.Count > maxSplats)
            {
                var old = splats.Dequeue();
                if (old != null) Destroy(old);
            }
        }

        public void ClearAll()
        {
            foreach (var f in flying) if (f.go != null) Destroy(f.go);
            flying.Clear();
            foreach (var t in tracers) if (t.go != null) Destroy(t.go);
            tracers.Clear();
            while (splats.Count > 0)
            {
                var s = splats.Dequeue();
                if (s != null) Destroy(s);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                Flying f = flying[i];
                f.vel.y -= f.gravity * dt;
                f.pos += f.vel * dt;
                f.life -= dt;
                if (f.go != null) f.go.transform.position = f.pos;
                if (f.life <= 0f || f.pos.y < -1f)
                {
                    Destroy(f.go);
                    flying.RemoveAt(i);
                }
            }
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                tracers[i].life -= dt;
                if (tracers[i].life > 0f) continue;
                Destroy(tracers[i].go);
                tracers.RemoveAt(i);
            }
        }
    }
}
