using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace MakeupSniper
{
    /// <summary>
    /// Сохраняет полароид раунда вертикальной картинкой 1080×1920 (под TikTok/Reels) в «Изображения/MakeupSniper».
    /// Рисуется отдельной невидимой камерой, на экран игрока не влияет.
    /// </summary>
    public class PolaroidSaver : MonoBehaviour
    {
        const int Width = 1080, Height = 1920;
        const int Layer = 31;

        /// <summary>Для автотестов: своя папка, чтобы не засорять «Изображения».</summary>
        public static string FolderOverride;

        public static string Folder
        {
            get { return FolderOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "MakeupSniper"); }
        }

        /// <summary>Сохранять ли полароиды: в обычной игре да, в тестах — только если указана своя папка.</summary>
        public static bool Enabled { get { return FolderOverride != null || !Application.isBatchMode; } }

        Camera cam;
        Canvas canvas;
        RawImage[] refImg = new RawImage[2], resImg = new RawImage[2];
        Text[] refLabel = new Text[2], resLabel = new Text[2];
        Text title, stamp, headline, footer, stars;
        bool built;

        void Build()
        {
            if (built) return;
            built = true;
            var camGo = new GameObject("PolaroidCamera", typeof(Camera));
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = new Vector3(0f, -100f, 0f);
            cam = camGo.GetComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.97f, 0.91f, 0.93f);
            cam.cullingMask = 1 << Layer;
            cam.orthographic = true;

            var cgo = new GameObject("PolaroidCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cgo.layer = Layer;
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height);
            scaler.matchWidthOrHeight = 1f;
            Transform root = cgo.transform;

            var tc = new Vector2(0.5f, 1f);
            title = UiKit.Label("Title", root, "MAKEUP SNIPER", 96, TextAnchor.MiddleCenter, UiKit.Accent, tc, new Vector2(0f, -60f), new Vector2(1000f, 120f));
            title.fontStyle = FontStyle.Bold;
            var frame = UiKit.Box("Frame", root, Color.white, tc, new Vector2(0f, -200f), new Vector2(960f, 1580f));
            for (int f = 0; f < 2; f++)
            {
                refImg[f] = UiKit.Picture("Ref" + f, frame.transform, tc, Vector2.zero, new Vector2(600f, 600f));
                resImg[f] = UiKit.Picture("Res" + f, frame.transform, tc, Vector2.zero, new Vector2(600f, 600f));
                refLabel[f] = UiKit.Label("RefL" + f, frame.transform, "РЕФЕРЕНС", 40, TextAnchor.MiddleCenter, UiKit.Ink, tc, Vector2.zero, new Vector2(600f, 54f));
                resLabel[f] = UiKit.Label("ResL" + f, frame.transform, "РЕЗУЛЬТАТ", 40, TextAnchor.MiddleCenter, UiKit.Ink, tc, Vector2.zero, new Vector2(600f, 54f));
                refLabel[f].fontStyle = resLabel[f].fontStyle = FontStyle.BoldAndItalic;
            }
            var stampBox = UiKit.Box("Stamp", frame.transform, new Color(1f, 1f, 1f, 0.95f), new Vector2(1f, 1f), new Vector2(40f, -560f), new Vector2(330f, 160f));
            stampBox.transform.localRotation = Quaternion.Euler(0f, 0f, -9f);
            UiKit.AddOutline(stampBox, UiKit.Accent, 6f);
            stamp = UiKit.Label("Pct", stampBox.transform, "", 120, TextAnchor.MiddleCenter, UiKit.Accent, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330f, 160f));
            stamp.fontStyle = FontStyle.Bold;
            headline = UiKit.Label("Headline", frame.transform, "", 44, TextAnchor.UpperCenter, UiKit.Ink, tc, new Vector2(0f, -1305f), new Vector2(900f, 160f));
            headline.resizeTextForBestFit = true; headline.resizeTextMinSize = 28; headline.resizeTextMaxSize = 44;
            headline.verticalOverflow = VerticalWrapMode.Truncate;
            headline.fontStyle = FontStyle.Bold;
            stars = UiKit.Label("Stars", frame.transform, "", 70, TextAnchor.MiddleCenter, UiKit.Gold, tc, new Vector2(0f, -1475f), new Vector2(600f, 90f));
            footer = UiKit.Label("Footer", root, "", 34, TextAnchor.MiddleCenter, UiKit.Muted, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1000f, 105f));
            SetLayerRecursive(cgo, Layer);
            cgo.SetActive(false);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        /// <summary>Сохранить полароид. Возвращает путь к файлу или null.</summary>
        public string Save(RevealPacket p, ReferenceSet set, Texture[] results, Func<ReferenceData, Texture2D> preview, string locationName)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || set == null) return null;
            try
            {
                Build();
                int faces = Mathf.Clamp(set.faces.Length, 1, 2);
                for (int f = 0; f < 2; f++)
                {
                    bool on = f < faces;
                    refImg[f].gameObject.SetActive(on); resImg[f].gameObject.SetActive(on);
                    refLabel[f].gameObject.SetActive(on); resLabel[f].gameObject.SetActive(on);
                    if (!on) continue;
                    refImg[f].texture = preview(set.faces[f]);
                    resImg[f].texture = f < results.Length ? results[f] : null;
                    int fm = p.faceMatch != null && f < p.faceMatch.Length ? p.faceMatch[f] : p.match;
                    refLabel[f].text = faces > 1 ? "РЕФЕРЕНС: " + set.faces[f].displayName.ToUpperInvariant() : "РЕФЕРЕНС";
                    resLabel[f].text = "РЕЗУЛЬТАТ " + fm + "%";
                    if (faces == 1)
                    {
                        // один над другим
                        Place(refImg[f], new Vector2(0f, -30f), 560f);
                        Place(refLabel[f], new Vector2(0f, -600f));
                        Place(resImg[f], new Vector2(0f, -670f), 560f);
                        Place(resLabel[f], new Vector2(0f, -1240f));
                    }
                    else
                    {
                        // свадьба: два столбца (невеста и жених)
                        float x = f == 0 ? -235f : 235f;
                        Place(refImg[f], new Vector2(x, -60f), 440f);
                        Place(refLabel[f], new Vector2(x, -505f));
                        Place(resImg[f], new Vector2(x, -600f), 440f);
                        Place(resLabel[f], new Vector2(x, -1045f));
                    }
                }
                stamp.text = p.match + "%";
                stars.text = UiKit.Stars(p.stars);
                headline.text = p.headline + "\nМодель: " + p.modelName;
                footer.text = locationName + " · " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "\nmakeup sniper — прототип";

                var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                canvas.gameObject.SetActive(true);
                cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                cam.Render();
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                canvas.gameObject.SetActive(false);
                rt.Release();
                Destroy(rt);

                Directory.CreateDirectory(Folder);
                string file = Path.Combine(Folder, "polaroid_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + p.match + ".png");
                File.WriteAllBytes(file, tex.EncodeToPNG());
                Destroy(tex);
                return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MakeupSniper] Полароид не сохранился: " + e.Message);
                if (canvas != null) canvas.gameObject.SetActive(false);
                return null;
            }
        }

        static void Place(Graphic g, Vector2 pos, float size = -1f)
        {
            var rt = g.rectTransform;
            rt.anchoredPosition = pos;
            if (size > 0f) rt.sizeDelta = new Vector2(size, size);
        }
    }
}
