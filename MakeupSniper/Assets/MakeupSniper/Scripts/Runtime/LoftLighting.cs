using UnityEngine;
using UnityEngine.Rendering;

namespace MakeupSniper
{
    /// <summary>
    /// Ровный заполняющий свет для пастельной картинки. Сцена собирается кодом и не запекает освещение,
    /// поэтому окружающий свет задаём явно при запуске.
    /// </summary>
    public class LoftLighting : MonoBehaviour
    {
        public Color ambientColor = new Color(0.56f, 0.53f, 0.55f);

        void Awake() { Apply(); }

        public void Apply()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambientColor.linear);
            RenderSettings.ambientProbe = sh;
        }
    }
}
