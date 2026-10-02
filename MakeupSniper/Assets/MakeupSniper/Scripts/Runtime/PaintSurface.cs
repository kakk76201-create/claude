using System.Collections.Generic;
using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Лицевой холст головы. Два представления (как в GDD):
    /// 1) RenderTexture 1024² для красоты — в неё ставятся штампы кистей;
    /// 2) логическая сетка 64×64 — только по ней считается счёт.
    /// Координата на холсте получается фронтальной проекцией локальной точки попадания.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PaintSurface : MonoBehaviour
    {
        [Tooltip("Номер лица в раунде: 0 — Модель, 1 — жених")]
        public byte faceIndex;
        [Tooltip("Ширина лицевого холста в метрах (в локальных координатах головы)")]
        public float faceSize = 0.7f;
        [Tooltip("Радиус головы в метрах (до вытягивания по вертикали)")]
        public float headRadius = 0.3f;
        public int textureSize = 1024;
        [Tooltip("Материал ластика (шейдер Hidden/MakeupSniper/EraserBlit)")]
        public Material eraserMaterial;
        [Tooltip("Материал мазка (шейдер Hidden/MakeupSniper/BrushBlit)")]
        public Material brushMaterial;

        public RenderTexture Texture { get; private set; }
        public FaceGrid Grid { get; } = new FaceGrid();
        public float FaceRadiusUv { get { return headRadius / faceSize; } }
        public int StampCount { get; private set; }
        /// <summary>Вызывается после каждого мазка (для звуков и эффектов).</summary>
        public event System.Action<StampData> Stamped;

        Texture2D baseTexture;
        readonly Dictionary<int, Texture2D> brushes = new Dictionary<int, Texture2D>();
        Material eraserInstance;
        bool initialized;
        bool gpu;

        void Awake() { Initialize(); }

        /// <summary>Создаёт меш, коллайдер, текстуры. Можно вызывать повторно.</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            gpu = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

            GetComponent<MeshFilter>().sharedMesh = HeadMeshFactory.CreateVisual(headRadius, faceSize);
            var col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = HeadMeshFactory.CreateCollider(headRadius);
            col.convex = true;

            baseTexture = FaceArt.DrawBaseFace(textureSize, FaceRadiusUv).ToTexture("FaceBase", false);

            if (gpu)
            {
                Texture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Texture.name = "FacePaint" + faceIndex;
                Texture.wrapMode = TextureWrapMode.Clamp;
                Texture.filterMode = FilterMode.Bilinear;
                Texture.useMipMap = true;
                Texture.autoGenerateMips = true;
                Texture.Create();

                var mat = GetComponent<MeshRenderer>().material; // свой экземпляр материала для этой головы
                mat.mainTexture = Texture;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            }
            if (eraserMaterial != null) eraserInstance = new Material(eraserMaterial);

            Clear();
        }

        /// <summary>Вернуть чистое лицо.</summary>
        public void Clear()
        {
            Initialize();
            if (gpu && Texture != null) Graphics.Blit(baseTexture, Texture);
            Grid.Clear();
            StampCount = 0;
        }

        /// <summary>Мировая точка попадания → координата холста. false, если попали в затылок.</summary>
        public bool TryGetUv(Vector3 worldPoint, out Vector2 uv)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            uv = new Vector2(-local.x / faceSize + 0.5f, local.y / faceSize + 0.5f); // x отражён, как в HeadMeshFactory
            return local.z > 0.04f;
        }

        /// <summary>Направление в мире → угол на холсте (для полос туши по направлению полёта).</summary>
        public float UvAngleOf(Vector3 worldDirection)
        {
            Vector3 local = transform.InverseTransformDirection(worldDirection);
            if (Mathf.Abs(local.x) < 1e-4f && Mathf.Abs(local.y) < 1e-4f) return 0f;
            return Mathf.Atan2(local.y, -local.x) * Mathf.Rad2Deg;
        }

        /// <summary>Совместимость: круглый штамп.</summary>
        public void StampUv(Vector2 uv, float radiusMeters, PaintColor color, BrushKind brush, byte owner, float angleDeg)
        {
            Apply(new StampData { face = faceIndex, u = uv.x, v = uv.y, radius = radiusMeters, color = (byte)color, brush = (byte)brush, angle = angleDeg, owner = owner, mult = 1 });
        }

        /// <summary>Нарисовать мазок: картинка в RenderTexture и отметка в логической сетке.</summary>
        public void Apply(StampData s)
        {
            Initialize();
            var uv = new Vector2(s.u, s.v);
            BrushKind brush = s.Brush;
            PaintColor color = s.Color;
            bool erase = brush == BrushKind.Eraser;

            if (gpu && Texture != null) DrawToTexture(s, uv, brush, color);

            // логическая сетка
            byte mult = s.mult == 0 ? (byte)1 : s.mult;
            if (FaceArt.IsElongated(brush))
                Grid.StampStripe(uv, Mathf.Max(s.length, s.radius * 2f) / faceSize, s.radius / faceSize, s.angle, color, s.owner, mult);
            else
                Grid.StampDisc(uv, s.radius / faceSize, erase ? PaintColor.None : color, s.owner, mult);

            StampCount++;
            if (Stamped != null) Stamped(s);
        }

        void DrawToTexture(StampData s, Vector2 uv, BrushKind brush, PaintColor color)
        {
            float px = uv.x * textureSize, py = uv.y * textureSize;
            float scale = FaceArt.BrushScale(brush);
            float halfH = s.radius / faceSize * textureSize * scale;
            float halfW = FaceArt.IsElongated(brush) ? Mathf.Max(s.length, s.radius * 2f) * 0.5f / faceSize * textureSize * scale : halfH;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = Texture;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, textureSize, 0, textureSize); // y вверх, как у uv
            if (brush == BrushKind.Eraser && eraserInstance != null)
            {
                // кусочек чистого лица того же размера: sourceRect в uv базовой текстуры
                float ru = halfH / textureSize;
                eraserInstance.SetVector("_Center", new Vector4(uv.x, uv.y, 0f, 0f));
                eraserInstance.SetFloat("_Radius", ru);
                Graphics.DrawTexture(new Rect(px - halfH, py - halfH, halfH * 2f, halfH * 2f), baseTexture,
                    new Rect(uv.x - ru, uv.y + ru, ru * 2f, -ru * 2f), 0, 0, 0, 0, eraserInstance);
            }
            else
            {
                Texture2D tex = GetBrush(brush, color);
                GL.MultMatrix(Matrix4x4.TRS(new Vector3(px, py, 0f), Quaternion.Euler(0f, 0f, s.angle), Vector3.one));
                // при оси y вверх картинка кисти рисуется перевёрнутой, поэтому берём её с отрицательной высотой
                if (brushMaterial != null)
                    Graphics.DrawTexture(new Rect(-halfW, -halfH, halfW * 2f, halfH * 2f), tex, new Rect(0f, 1f, 1f, -1f), 0, 0, 0, 0, brushMaterial);
                else
                    Graphics.DrawTexture(new Rect(-halfW, -halfH, halfW * 2f, halfH * 2f), tex, new Rect(0f, 1f, 1f, -1f), 0, 0, 0, 0);
            }
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        Texture2D GetBrush(BrushKind kind, PaintColor color)
        {
            int key = (int)kind * 16 + (int)color;
            Texture2D t;
            if (!brushes.TryGetValue(key, out t))
            {
                t = FaceArt.CreateBrush(kind, color, 256);
                brushes[key] = t;
            }
            return t;
        }

        /// <summary>Копия текущего лица в обычную текстуру (для проверки, полароида и зеркальца).</summary>
        public Texture2D ReadBack()
        {
            var t = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            if (!gpu || Texture == null) return t;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = Texture;
            t.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }

        /// <summary>Картинка чистого лица (для экрана выбора и полароида).</summary>
        public Texture BaseTexture { get { return baseTexture; } }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            if (baseTexture != null) Destroy(baseTexture);
            if (eraserInstance != null) Destroy(eraserInstance);
            foreach (var b in brushes.Values) Destroy(b);
        }
    }
}
