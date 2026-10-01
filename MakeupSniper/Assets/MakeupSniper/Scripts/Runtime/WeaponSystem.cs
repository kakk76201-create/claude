using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>Что произошло с выстрелом. Нужен интерфейсу для баннеров и счётчиков.</summary>
    public struct ShotResult
    {
        public bool hitFace;       // краска легла на лицо
        public bool backOfHead;    // попали в голову, но в затылок
        public Vector2 uv;
        public PaintColor color;
        public PaintSurface surface;
    }

    /// <summary>
    /// Стрельба. Снарядов-Rigidbody нет (правило GDD): снайперка — мгновенный луч,
    /// базука — аналитическая парабола, которую мы сами двигаем и проверяем лучом каждый кадр.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        public Camera aimCamera;
        public Transform muzzle;
        public WeaponDef[] weapons = new WeaponDef[0];
        public Material redMaterial, pinkMaterial, blackMaterial;
        public float gravity = 9.8f;

        public int CurrentIndex { get; private set; }
        public bool AltShade { get; private set; }
        public float ReloadLeft { get; private set; }
        public int ShotsFired { get; private set; }
        public int FaceHits { get; private set; }
        public byte OwnerId = 1;

        public event Action<ShotResult> ShotResolved;
        public event Action<WeaponDef> Fired;

        public WeaponDef Current { get { return weapons[Mathf.Clamp(CurrentIndex, 0, weapons.Length - 1)]; } }
        public PaintColor CurrentColor { get { return AltShade && Current.hasAltShade ? Current.altColor : Current.color; } }
        public BrushKind CurrentBrush { get { return AltShade && Current.hasAltShade ? Current.altBrush : Current.brush; } }
        public float ReloadFraction { get { return Current.reload <= 0f ? 1f : 1f - Mathf.Clamp01(ReloadLeft / Current.reload); } }

        class Projectile { public Vector3 pos, vel; public WeaponDef weapon; public PaintColor color; public BrushKind brush; public GameObject visual; public float life; }
        class Timed { public GameObject go; public float life; }

        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<Timed> tracers = new List<Timed>();
        readonly List<GameObject> splats = new List<GameObject>();

        public void Select(int index)
        {
            if (index < 0 || index >= weapons.Length || index == CurrentIndex) return;
            CurrentIndex = index;
            ReloadLeft = Mathf.Max(ReloadLeft, 0.7f); // смена ствола занимает 0,7 с
        }

        public void ToggleShade()
        {
            if (Current.hasAltShade) AltShade = !AltShade;
        }

        public void ResetForRound()
        {
            ReloadLeft = 0f; ShotsFired = 0; FaceHits = 0; AltShade = false; CurrentIndex = 0;
            foreach (var p in projectiles) if (p.visual != null) Destroy(p.visual);
            projectiles.Clear();
            foreach (var s in splats) if (s != null) Destroy(s);
            splats.Clear();
            foreach (var t in tracers) if (t.go != null) Destroy(t.go);
            tracers.Clear();
        }

        /// <summary>Выстрел из центра экрана с разбросом. false, если идёт перезарядка.</summary>
        public bool TryFire(bool zoomed)
        {
            if (ReloadLeft > 0f || aimCamera == null) return false;
            WeaponDef w = Current;
            float spread = zoomed ? w.spreadZoomDeg : w.spreadDeg;
            Vector2 off = UnityEngine.Random.insideUnitCircle * spread;
            Vector3 dir = aimCamera.transform.rotation * Quaternion.Euler(off.y, off.x, 0f) * Vector3.forward;
            FireRay(new Ray(aimCamera.transform.position, dir));
            return true;
        }

        /// <summary>Выстрел по заданному лучу (также используется в автотестах).</summary>
        public void FireRay(Ray ray)
        {
            WeaponDef w = Current;
            PaintColor color = CurrentColor;
            BrushKind brush = CurrentBrush;
            ReloadLeft = w.reload;
            ShotsFired++;
            if (Fired != null) Fired(w);

            Vector3 from = muzzle != null ? muzzle.position : ray.origin;
            if (w.kind == WeaponKind.Hitscan)
            {
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                {
                    SpawnTracer(from, hit.point, color);
                    Resolve(hit, w, color, brush);
                }
                else
                {
                    SpawnTracer(from, ray.origin + ray.direction * 40f, color);
                    Report(new ShotResult { color = color });
                }
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "Projectile";
                Destroy(visual.GetComponent<Collider>());
                visual.transform.localScale = Vector3.one * 0.16f;
                visual.transform.position = ray.origin;
                visual.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
                projectiles.Add(new Projectile { pos = ray.origin, vel = ray.direction.normalized * w.projectileSpeed, weapon = w, color = color, brush = brush, visual = visual, life = 5f });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (ReloadLeft > 0f) ReloadLeft = Mathf.Max(0f, ReloadLeft - dt);

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = projectiles[i];
                p.vel.y -= gravity * dt;
                Vector3 next = p.pos + p.vel * dt;
                RaycastHit hit;
                if (Physics.Linecast(p.pos, next, out hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    Resolve(hit, p.weapon, p.color, p.brush);
                    Destroy(p.visual);
                    projectiles.RemoveAt(i);
                    continue;
                }
                p.pos = next;
                p.life -= dt;
                if (p.visual != null) p.visual.transform.position = p.pos;
                if (p.life <= 0f) { Destroy(p.visual); projectiles.RemoveAt(i); }
            }

            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                tracers[i].life -= dt;
                if (tracers[i].life <= 0f) { Destroy(tracers[i].go); tracers.RemoveAt(i); }
            }
        }

        void Resolve(RaycastHit hit, WeaponDef w, PaintColor color, BrushKind brush)
        {
            var surface = hit.collider.GetComponent<PaintSurface>();
            if (surface != null)
            {
                Vector2 uv;
                if (surface.TryGetUv(hit.point, out uv))
                {
                    float angle = brush == BrushKind.Kiss ? UnityEngine.Random.Range(-25f, 25f) : UnityEngine.Random.Range(0f, 360f);
                    surface.StampUv(uv, w.spotRadius, color, brush, OwnerId, angle);
                    FaceHits++;
                    Report(new ShotResult { hitFace = true, uv = uv, color = color, surface = surface });
                }
                else
                {
                    Report(new ShotResult { backOfHead = true, color = color, surface = surface });
                }
                return;
            }
            SpawnSplat(hit.point, hit.normal, w.spotRadius * 2.2f, color);
            Report(new ShotResult { color = color });
        }

        void Report(ShotResult r) { if (ShotResolved != null) ShotResolved(r); }

        Material MaterialFor(PaintColor c)
        {
            switch (c)
            {
                case PaintColor.Pink: return pinkMaterial;
                case PaintColor.Black: return blackMaterial;
                default: return redMaterial;
            }
        }

        // Клякса на стене/полу/кресле: сплющенный шарик, прижатый к поверхности.
        void SpawnSplat(Vector3 point, Vector3 normal, float radius, PaintColor color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Splat";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normal * 0.004f;
            go.transform.rotation = Quaternion.LookRotation(normal);
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 0.012f);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            splats.Add(go);
            if (splats.Count > 80) { Destroy(splats[0]); splats.RemoveAt(0); }
        }

        void SpawnTracer(Vector3 a, Vector3 b, PaintColor color)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tracer";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.LookRotation(b - a);
            go.transform.localScale = new Vector3(0.012f, 0.012f, len);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(color);
            tracers.Add(new Timed { go = go, life = 0.08f });
        }
    }
}
