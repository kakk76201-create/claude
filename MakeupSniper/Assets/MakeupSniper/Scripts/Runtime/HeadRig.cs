using UnityEngine;

namespace MakeupSniper
{
    /// <summary>
    /// Поза головы Модели: поворот ±35° по горизонтали и ±20° по вертикали (как в GDD),
    /// уворот (резкий наклон в сторону) и ладонь-щит перед лицом.
    /// Кто задаёт позу: сама Модель (мышью на своём компьютере), сеть (у остальных) или бот (покачивание).
    /// </summary>
    public class HeadRig : MonoBehaviour
    {
        public enum Source { Local, Remote, Bot }

        public float yawLimit = 35f;
        public float pitchLimit = 20f;
        [Tooltip("Ладонь-щит (дочерний объект)")]
        public Transform palm;
        [Tooltip("Цвет краски на ладони")]
        public Renderer palmRenderer;

        public Source source = Source.Bot;
        /// <summary>Бот качает головой только во время стрельбы.</summary>
        public bool SwayActive { get; set; }
        public bool idleSway = true;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float TargetYaw { get; private set; }
        public float TargetPitch { get; private set; }
        /// <summary>0..1 — насколько голова сейчас наклонена в увороте.</summary>
        public float DodgeAmount { get; private set; }
        public bool PalmUp { get; set; }
        public float PalmAmount { get; private set; }

        Vector3 basePos;
        float clock, dodgeTimer;
        bool baseSet;
        Vector3 palmRest, palmUpPos;
        Material palmMat;

        void Awake()
        {
            CaptureBase();
            if (palm != null)
            {
                palmUpPos = palm.localPosition;
                palmRest = palmUpPos + new Vector3(0.05f, -0.55f, -0.1f);
                palm.localPosition = palmRest;
            }
            if (palmRenderer != null) palmMat = palmRenderer.material;
        }

        public void CaptureBase()
        {
            if (baseSet) return;
            baseSet = true;
            basePos = transform.localPosition;
        }

        public void ResetPose()
        {
            TargetYaw = TargetPitch = 0f;
            dodgeTimer = 0f;
            PalmUp = false;
        }

        /// <summary>Модель двигает мышью (пиксели × чувствительность).</summary>
        public void AddLook(Vector2 delta)
        {
            TargetYaw = Mathf.Clamp(TargetYaw + delta.x, -yawLimit, yawLimit);
            TargetPitch = Mathf.Clamp(TargetPitch - delta.y, -pitchLimit, pitchLimit);
        }

        /// <summary>Поза пришла по сети.</summary>
        public void SetTarget(float yaw, float pitch)
        {
            TargetYaw = Mathf.Clamp(yaw, -yawLimit, yawLimit);
            TargetPitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
        }

        public void Dodge(float seconds)
        {
            dodgeTimer = seconds;
        }

        public void SetPalmColor(PaintColor c)
        {
            if (palmMat == null) return;
            Color col = c == PaintColor.None ? new Color(0.96f, 0.86f, 0.82f) : (Color)PaintColors.ToColor(c);
            if (palmMat.HasProperty("_BaseColor")) palmMat.SetColor("_BaseColor", col);
            else palmMat.color = col;
        }

        /// <summary>Мгновенно поставить позу (для проверки попаданий на сервере).</summary>
        public void ApplyInstant(float yaw, float pitch, float dodge)
        {
            Yaw = yaw; Pitch = pitch; DodgeAmount = dodge;
            ApplyTransform();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            if (source == Source.Bot)
            {
                if (idleSway && SwayActive)
                {
                    TargetYaw = Mathf.Clamp(22f * Mathf.Sin(clock * 0.55f) + 9f * Mathf.Sin(clock * 1.3f + 1f), -yawLimit, yawLimit);
                    TargetPitch = Mathf.Clamp(9f * Mathf.Sin(clock * 0.8f + 2f), -pitchLimit, pitchLimit);
                }
                else
                {
                    TargetYaw = Mathf.MoveTowards(TargetYaw, 0f, dt * 40f);
                    TargetPitch = Mathf.MoveTowards(TargetPitch, 0f, dt * 40f);
                }
            }

            float k = Mathf.Min(1f, dt * (source == Source.Local ? 25f : 10f));
            Yaw = Mathf.Lerp(Yaw, TargetYaw, k);
            Pitch = Mathf.Lerp(Pitch, TargetPitch, k);

            if (dodgeTimer > 0f) dodgeTimer -= dt;
            DodgeAmount = Mathf.MoveTowards(DodgeAmount, dodgeTimer > 0f ? 1f : 0f, dt * 12f);
            PalmAmount = Mathf.MoveTowards(PalmAmount, PalmUp ? 1f : 0f, dt * 9f);
            ApplyTransform();
        }

        void ApplyTransform()
        {
            CaptureBase();
            transform.localPosition = basePos + new Vector3(-0.24f * DodgeAmount, -0.05f * DodgeAmount, 0f);
            transform.localRotation = Quaternion.Euler(Pitch, Yaw, 28f * DodgeAmount);
            if (palm != null)
            {
                palm.localPosition = Vector3.Lerp(palmRest, palmUpPos, PalmAmount);
                bool visible = PalmAmount > 0.02f;
                if (palm.gameObject.activeSelf != visible) palm.gameObject.SetActive(visible);
            }
        }
    }
}
