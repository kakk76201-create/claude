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
        [Tooltip("Ширина лицевого холста в метрах (в локальных координатах головы)")]
        public float faceSize = 0.7f;
        [Tooltip("Радиус головы в метрах (до вытягивания по вертикали)")]
        public float headRadius = 0.3f;
        public int textureSize = 1024;

        public RenderTexture Texture { get; private set; }
        public FaceGrid Grid { get; } = new FaceGrid();
        public float FaceRadiusUv { get { return headRadius / faceSize; } }

        Texture2D baseTexture;
        readonly Dictionary<int, Texture2D> brushes = new Dictionary<int, Texture2D>();
        bool initialized;

        void Awake() { Initialize(); }

        /// <summary>Создаёт меш, коллайдер, текстуры. Можно вызывать повторно.</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            GetComponent<MeshFilter>().sharedMesh = HeadMeshFactory.CreateVisual(headRadius, faceSize);
            var col = GetComponent<MeshCollider>();
            if (col == null) col = gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = HeadMeshFactory.CreateCollider(headRadius);
            col.convex = true;

            baseTexture = FaceArt.DrawBaseFace(textureSize, FaceRadiusUv).ToTexture("FaceBase", false);

            Texture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture.name = "FacePaint";
            Texture.wrapMode = TextureWrapMode.Clamp;
            Texture.filterMode = FilterMode.Bilinear;
            Texture.useMipMap = true;
            Texture.autoGenerateMips = true;
            Texture.Create();

            var mat = GetComponent<MeshRenderer>().material; // свой экземпляр материала для этой головы
            mat.mainTexture = Texture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            Clear();
        }

        /// <summary>Вернуть чистое лицо.</summary>
        public void Clear()
        {
            Graphics.Blit(baseTexture, Texture);
            Grid.Clear();
        }

        /// <summary>Мировая точка попадания → координата холста. false, если попали в затылок.</summary>
        public bool TryGetUv(Vector3 worldPoint, out Vector2 uv)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            uv = new Vector2(local.x / faceSize + 0.5f, local.y / faceSize + 0.5f);
            return local.z > 0.04f;
        }

        /// <summary>Поставить штамп: картинка в RenderTexture и диск в логической сетке.</summary>
        public void StampUv(Vector2 uv, float radiusMeters, PaintColor color, BrushKind brush, byte owner, float angleDeg)
        {
            Initialize();
            Texture2D tex = GetBrush(brush, color);
            float half = radiusMeters / faceSize * textureSize * FaceArt.BrushScale(brush);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = Texture;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, textureSize, 0, textureSize); // y вверх, как у uv
            GL.MultMatrix(Matrix4x4.TRS(new Vector3(uv.x * textureSize, uv.y * textureSize, 0f), Quaternion.Euler(0f, 0f, angleDeg), Vector3.one));
            // при оси y вверх картинка кисти рисуется перевёрнутой, поэтому берём её с отрицательной высотой
            Graphics.DrawTexture(new Rect(-half, -half, half * 2f, half * 2f), tex, new Rect(0f, 1f, 1f, -1f), 0, 0, 0, 0);
            GL.PopMatrix();
            RenderTexture.active = prev;

            Grid.StampDisc(uv, radiusMeters / faceSize, color, owner);
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

        /// <summary>Копия текущего лица в обычную текстуру (для проверки и сохранения).</summary>
        public Texture2D ReadBack()
        {
            var t = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = Texture;
            t.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            return t;
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
            if (baseTexture != null) Destroy(baseTexture);
            foreach (var b in brushes.Values) Destroy(b);
        }
    }
}
