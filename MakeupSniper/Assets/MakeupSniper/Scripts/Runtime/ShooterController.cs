using UnityEngine;
using UnityEngine.InputSystem;

namespace MakeupSniper
{
    /// <summary>
    /// Стрелок: стоит на одной из трёх линий (3/8/15 м), смотрит мышью, зум на правую кнопку,
    /// выстрел на левую. Клавиши: 1/2 — ствол, B — оттенок помады, F — следующая линия.
    /// </summary>
    public class ShooterController : MonoBehaviour
    {
        public Camera cam;
        public WeaponSystem weapons;
        public Transform viewModel;
        public float[] lineDistances = { 3f, 8f, 15f };
        public float eyeHeight = 1.6f;
        public float headHeight = 1.38f;
        public float sensitivity = 0.08f;
        public float normalFov = 60f;

        public int LineIndex { get; private set; }
        public bool Zoomed { get; private set; }
        public bool InputEnabled { get; set; }
        public int LineMultiplier { get { return LineIndex + 1; } }
        public float LineDistance { get { return lineDistances[LineIndex]; } }

        float yaw, pitch, kick, clock;
        Vector3 viewModelRest;

        void Awake()
        {
            if (viewModel != null) viewModelRest = viewModel.localPosition;
        }

        /// <summary>Встать на линию и навестись на голову Модели.</summary>
        public void SetLine(int index)
        {
            LineIndex = Mathf.Clamp(index, 0, lineDistances.Length - 1);
            float d = lineDistances[LineIndex];
            transform.position = new Vector3(0f, eyeHeight, d);
            yaw = 0f;
            pitch = Mathf.Atan2(eyeHeight - headHeight, d) * Mathf.Rad2Deg;
            ApplyRotation();
        }

        void ApplyRotation()
        {
            // стрелок стоит на +z и смотрит на Модель в начале координат, то есть вдоль −z
            transform.rotation = Quaternion.Euler(pitch, 180f + yaw, 0f);
        }

        void Update()
        {
            clock += Time.deltaTime;
            kick = Mathf.Lerp(kick, 0f, Mathf.Min(1f, Time.deltaTime * 10f));
            if (viewModel != null) viewModel.localPosition = viewModelRest + new Vector3(0f, 0f, -kick);

            bool wantZoom = false;
            if (InputEnabled && weapons != null)
            {
                var mouse = Mouse.current;
                var kb = Keyboard.current;
                if (mouse != null)
                {
                    wantZoom = weapons.Current.canZoom && mouse.rightButton.isPressed;
                    Vector2 d = mouse.delta.ReadValue() * sensitivity * (Zoomed ? 0.25f : 1f);
                    yaw = Mathf.Clamp(yaw + d.x, -70f, 70f);
                    pitch = Mathf.Clamp(pitch - d.y, -50f, 50f);
                    ApplyRotation();
                    if (mouse.leftButton.wasPressedThisFrame && weapons.TryFire(Zoomed)) kick = 0.08f;
                }
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) weapons.Select(0);
                    if (kb.digit2Key.wasPressedThisFrame) weapons.Select(1);
                    if (kb.bKey.wasPressedThisFrame) weapons.ToggleShade();
                    if (kb.fKey.wasPressedThisFrame) SetLine((LineIndex + 1) % lineDistances.Length);
                }
            }
            Zoomed = wantZoom;

            if (cam != null)
            {
                float targetFov = Zoomed ? weapons.Current.zoomFov : normalFov;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Mathf.Min(1f, Time.deltaTime * 12f));
                // лёгкое «дыхание» прицела; в зуме заметнее
                float sway = Zoomed ? 0.18f : 0.06f;
                cam.transform.localRotation = Quaternion.Euler(sway * Mathf.Cos(clock * 1.3f), sway * Mathf.Sin(clock * 1.7f), 0f);
            }
        }
    }
}
