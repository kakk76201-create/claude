using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Ссылки на всё, что есть в сцене: лица, головы, камеры, декорации мест, стволы.
    /// Заполняется сборщиком сцены (SceneBuilder), в игре только читается.
    /// </summary>
    public class World : MonoBehaviour
    {
        public static World Instance { get; private set; }
        /// <summary>Слой мишеней игроков: по ним попадают выстрелы, но капсулы ходьбы сквозь них проходят.</summary>
        public const int HitboxLayer = 8;

        [Header("Лица и головы: 0 — Модель, 1 — жених")]
        public PaintSurface[] faces = new PaintSurface[0];
        public HeadRig[] heads = new HeadRig[0];
        [Tooltip("Кресло и тело жениха (только на свадьбе)")]
        public GameObject groomRoot;
        [Tooltip("Фартук Модели в кресле — красится в цвет игрока")]
        public Renderer modelApron;
        [Tooltip("Глаза Модели: сюда ставится камера, когда ты в кресле")]
        public Transform modelEye;
        public Collider palmCollider;

        [Header("Стрелки")]
        public Transform[] shooterSpawns = new Transform[0];
        [Tooltip("Ближе этой границы к креслу подходить нельзя (z, метры)")]
        public float closestLine = 2.6f;
        public float farthestLine = 20.5f;
        public float roomHalfWidth = 4.6f;

        [Header("Камеры")]
        public Camera mainCamera;
        public Camera mirrorCamera;
        public RenderTexture mirrorTexture;
        public Transform revealPose;
        public Transform revealPoseWide;
        public Transform menuPivot;

        [Header("Данные")]
        public WeaponDef[] weapons = new WeaponDef[0];
        public LocationDef[] locations = new LocationDef[0];

        [Header("Места")]
        [Tooltip("Декорации мест в том же порядке, что и locations")]
        public GameObject[] locationProps = new GameObject[0];
        public Renderer[] tintedWalls = new Renderer[0];
        public KidsGimmick kids;
        [Tooltip("Девушка на свидании")]
        public WitnessGimmick dateWitnesses;
        [Tooltip("Гости на свадьбе")]
        public WitnessGimmick weddingWitnesses;

        /// <summary>Свидетели текущего места (или null).</summary>
        public WitnessGimmick Witnesses { get; private set; }

        [Header("Сервисы")]
        public Sfx sfx;
        public Fx fx;

        void Awake()
        {
            Instance = this;
            Physics.IgnoreLayerCollision(HitboxLayer, 0, true);
            Physics.IgnoreLayerCollision(HitboxLayer, HitboxLayer, true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public PaintSurface Face(int i) { return i >= 0 && i < faces.Length ? faces[i] : null; }
        public HeadRig Head(int i) { return i >= 0 && i < heads.Length ? heads[i] : null; }

        /// <summary>Показать декорации места и включить его помеху.</summary>
        public void ShowLocation(int index)
        {
            for (int i = 0; i < locationProps.Length; i++)
                if (locationProps[i] != null) locationProps[i].SetActive(i == index);
            LocationDef def = index >= 0 && index < locations.Length ? locations[index] : null;
            Gimmick g = def != null ? def.gimmick : Gimmick.None;
            if (groomRoot != null) groomRoot.SetActive(g == Gimmick.Wedding);
            if (kids != null) kids.gameObject.SetActive(g == Gimmick.Kids);
            if (dateWitnesses != null) dateWitnesses.gameObject.SetActive(g == Gimmick.Witness);
            if (weddingWitnesses != null) weddingWitnesses.gameObject.SetActive(g == Gimmick.Wedding);
            Witnesses = g == Gimmick.Witness ? dateWitnesses : (g == Gimmick.Wedding ? weddingWitnesses : null);
            if (def != null)
                foreach (var r in tintedWalls)
                    if (r != null) r.material.SetColor("_BaseColor", def.wallColor);
        }

        /// <summary>Множитель очков по дистанции: линия 3 м — ×1, 8 м — ×2, 15 м — ×3.</summary>
        public static int LineMultiplier(float z)
        {
            if (z >= 14f) return 3;
            if (z >= 7.5f) return 2;
            return 1;
        }

        public static string LineName(float z)
        {
            if (z >= 14f) return "15 м";
            if (z >= 7.5f) return "8 м";
            return "3 м";
        }
    }
}
