using UnityEngine;
using UnityEngine.InputSystem;

namespace MakeupSniper
{
    /// <summary>
    /// Поворот головы Модели: ±35° по горизонтали и ±20° по вертикали (как в GDD).
    /// В режиме Модели голову крутит мышь. Иначе голова может медленно качаться сама,
    /// чтобы в одиночном тесте стрелку было во что целиться.
    /// </summary>
    public class HeadRig : MonoBehaviour
    {
        public float yawLimit = 35f;
        public float pitchLimit = 20f;
        public float mouseSensitivity = 0.12f;
        [Tooltip("Голова качается сама, когда ею не управляет Модель")]
        public bool idleSway = true;

        /// <summary>Голову крутит мышь (режим Модели).</summary>
        public bool ModelControlled { get; set; }
        /// <summary>Идёт стрельба, покачивание разрешено.</summary>
        public bool SwayActive { get; set; }

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        float targetYaw, targetPitch, manualYaw, manualPitch, clock;

        public void ResetPose()
        {
            targetYaw = targetPitch = manualYaw = manualPitch = 0f;
        }

        /// <summary>Сдвиг взгляда от мыши (в пикселях).</summary>
        public void AddLook(Vector2 delta)
        {
            manualYaw = Mathf.Clamp(manualYaw + delta.x * mouseSensitivity, -yawLimit, yawLimit);
            manualPitch = Mathf.Clamp(manualPitch - delta.y * mouseSensitivity, -pitchLimit, pitchLimit);
        }

        void Update()
        {
            clock += Time.deltaTime;
            if (ModelControlled)
            {
                var mouse = Mouse.current;
                if (mouse != null) AddLook(mouse.delta.ReadValue());
                targetYaw = manualYaw;
                targetPitch = manualPitch;
            }
            else if (idleSway && SwayActive)
            {
                targetYaw = Mathf.Clamp(manualYaw + 22f * Mathf.Sin(clock * 0.55f) + 9f * Mathf.Sin(clock * 1.3f + 1f), -yawLimit, yawLimit);
                targetPitch = Mathf.Clamp(manualPitch + 9f * Mathf.Sin(clock * 0.8f + 2f), -pitchLimit, pitchLimit);
            }
            else
            {
                targetYaw = manualYaw;
                targetPitch = manualPitch;
            }

            float k = Mathf.Min(1f, Time.deltaTime * 6f);
            Yaw = Mathf.Lerp(Yaw, targetYaw, k);
            Pitch = Mathf.Lerp(Pitch, targetPitch, k);
            transform.localRotation = Quaternion.Euler(Pitch, Yaw, 0f);
        }
    }
}
