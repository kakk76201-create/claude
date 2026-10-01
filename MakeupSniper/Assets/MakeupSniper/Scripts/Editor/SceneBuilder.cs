using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MakeupSniper.EditorTools
{
    /// <summary>
    /// Собирает сцену недели 1 и все ассеты (материалы, стволы, референсы) кодом.
    /// Меню Unity: Makeup Sniper → Пересобрать сцену недели 1.
    /// Так сцену можно восстановить одной кнопкой, если что-то сломалось руками.
    /// </summary>
    public static class SceneBuilder
    {
        public const string Root = "Assets/MakeupSniper";
        public const string ScenePath = Root + "/Scenes/Week1.unity";

        [MenuItem("Makeup Sniper/Пересобрать сцену недели 1")]
        public static void Build()
        {
            foreach (var f in new[] { "Scenes", "Materials", "Data", "Meshes" }) EnsureFolder(Root, f);

            // ---------- материалы ----------
            Material floorMat = Mat("Floor", new Color(0.91f, 0.84f, 0.78f));
            Material wallMint = Mat("WallMint", new Color(0.84f, 0.94f, 0.89f));
            Material wallPeach = Mat("WallPeach", new Color(0.97f, 0.86f, 0.78f));
            Material ceilingMat = Mat("Ceiling", new Color(1f, 0.97f, 0.95f));
            Material tapeMat = Mat("Tape", new Color(0.95f, 0.79f, 0.30f));
            Material chairMat = Mat("Chair", new Color(0.96f, 0.65f, 0.75f));
            Material metalMat = Mat("Metal", new Color(0.54f, 0.54f, 0.58f), 0.5f);
            Material apronMat = Mat("Apron", new Color(0.62f, 0.85f, 0.96f));
            Material skinMat = Mat("Skin", new Color(0.96f, 0.86f, 0.82f));
            Material headMat = Mat("Head", Color.white, 0.2f);
            Material sofaMat = Mat("Sofa", new Color(0.72f, 0.85f, 0.96f));
            Material plantMat = Mat("Plant", new Color(0.56f, 0.81f, 0.60f));
            Material potMat = Mat("Pot", new Color(0.85f, 0.65f, 0.48f));
            Material lampMat = Mat("Lamp", new Color(1f, 0.85f, 0.48f));
            Material darkMat = Mat("Dark", new Color(0.2f, 0.2f, 0.22f));
            Material tubeMat = Mat("LipstickTube", new Color(0.95f, 0.79f, 0.65f), 0.6f);
            Material redMat = Mat("PaintRed", (Color)PaintColors.ToColor(PaintColor.Red), 0.6f);
            Material pinkMat = Mat("PaintPink", (Color)PaintColors.ToColor(PaintColor.Pink), 0.3f);
            Material blackMat = Mat("PaintBlack", (Color)PaintColors.ToColor(PaintColor.Black), 0.4f);

            // ---------- меши головы (как ассеты, чтобы голова была видна и в редакторе) ----------
            Mesh headVisual = MeshAsset("HeadVisual", HeadMeshFactory.CreateVisual(0.3f, 0.7f));
            Mesh headCollider = MeshAsset("HeadCollider", HeadMeshFactory.CreateCollider(0.3f));

            // ---------- стволы ----------
            var lipstick = Asset<WeaponDef>(Root + "/Data/Weapon_Lipstick.asset");
            lipstick.displayName = "Помада-снайперка"; lipstick.kind = WeaponKind.Hitscan; lipstick.color = PaintColor.Red;
            lipstick.hasAltShade = true; lipstick.altColor = PaintColor.Black; lipstick.brush = BrushKind.Kiss; lipstick.altBrush = BrushKind.Smudge;
            lipstick.spotRadius = 0.015f; lipstick.reload = 1.5f; lipstick.canZoom = true; lipstick.zoomFov = 15f; lipstick.spreadDeg = 0.35f; lipstick.spreadZoomDeg = 0.08f;
            EditorUtility.SetDirty(lipstick);

            var bazooka = Asset<WeaponDef>(Root + "/Data/Weapon_Bazooka.asset");
            bazooka.displayName = "Румяна-базука"; bazooka.kind = WeaponKind.Projectile; bazooka.color = PaintColor.Pink;
            bazooka.hasAltShade = false; bazooka.brush = BrushKind.Blush; bazooka.spotRadius = 0.05f; bazooka.reload = 3f; bazooka.canZoom = false;
            bazooka.spreadDeg = 0.8f; bazooka.spreadZoomDeg = 0.8f; bazooka.projectileSpeed = 24f;
            EditorUtility.SetDirty(bazooka);

            // ---------- референсы ----------
            Vector2 nose = new Vector2(0.50f, 0.47f), noseR = new Vector2(0.055f, 0.05f);
            Vector2 lips = new Vector2(0.50f, 0.33f), lipsR = new Vector2(0.10f, 0.045f);
            Vector2 cheekL = new Vector2(0.69f, 0.43f), cheekR = new Vector2(0.31f, 0.43f), cheekRad = new Vector2(0.08f, 0.07f);

            var clown = Asset<ReferenceData>(Root + "/Data/Reference_Clown.asset");
            clown.displayName = "Клоун";
            clown.zones = new[]
            {
                Zone("Нос", nose, noseR, PaintColor.Red, ZoneStyle.Solid),
                Zone("Губы", lips, lipsR, PaintColor.Red, ZoneStyle.Lips),
                Zone("Левая щека", cheekL, cheekRad, PaintColor.Pink, ZoneStyle.Soft),
                Zone("Правая щека", cheekR, cheekRad, PaintColor.Pink, ZoneStyle.Soft)
            };
            EditorUtility.SetDirty(clown);

            var panda = Asset<ReferenceData>(Root + "/Data/Reference_Panda.asset");
            panda.displayName = "Панда";
            panda.zones = new[]
            {
                Zone("Левый глаз", FaceArt.EyeLeftPos, FaceArt.EyeRadius, PaintColor.Black, ZoneStyle.Solid),
                Zone("Правый глаз", FaceArt.EyeRightPos, FaceArt.EyeRadius, PaintColor.Black, ZoneStyle.Solid),
                Zone("Нос", nose, noseR, PaintColor.Black, ZoneStyle.Solid)
            };
            EditorUtility.SetDirty(panda);

            var kiss = Asset<ReferenceData>(Root + "/Data/Reference_Kiss.asset");
            kiss.displayName = "След поцелуя";
            kiss.zones = new[]
            {
                Zone("Губы", lips, lipsR, PaintColor.Red, ZoneStyle.Lips),
                Zone("Левая щека", cheekL, cheekRad, PaintColor.Red, ZoneStyle.Kiss)
            };
            EditorUtility.SetDirty(kiss);

            // ---------- сцена ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.53f, 0.55f);

            var sun = new GameObject("Sun", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 0.7f; light.shadows = LightShadows.Soft; light.color = new Color(1f, 0.98f, 0.95f);
            sun.AddComponent<LoftLighting>();
            sun.transform.rotation = Quaternion.Euler(50f, 160f, 0f);

            // лофт-«коробка»
            var loft = new GameObject("Loft").transform;
            Prim(PrimitiveType.Cube, "Floor", loft, new Vector3(0f, -0.1f, 9f), new Vector3(10f, 0.2f, 28f), floorMat);
            Prim(PrimitiveType.Cube, "WallBack", loft, new Vector3(0f, 1.9f, -4f), new Vector3(10f, 3.8f, 0.2f), wallMint).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "WallFar", loft, new Vector3(0f, 1.9f, 22f), new Vector3(10f, 3.8f, 0.2f), wallMint).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "WallLeft", loft, new Vector3(-5f, 1.9f, 9f), new Vector3(0.2f, 3.8f, 28f), wallPeach).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "WallRight", loft, new Vector3(5f, 1.9f, 9f), new Vector3(0.2f, 3.8f, 28f), wallPeach).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Prim(PrimitiveType.Cube, "Ceiling", loft, new Vector3(0f, 3.9f, 9f), new Vector3(10f, 0.2f, 28f), ceilingMat).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // три линии стрельбы «малярным скотчем»
            float[] lines = { 3f, 8f, 15f };
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < lines.Length; i++)
            {
                Prim(PrimitiveType.Cube, "Line" + lines[i] + "m", loft, new Vector3(0f, 0.006f, lines[i]), new Vector3(8f, 0.012f, 0.09f), tapeMat, false);
                var label = new GameObject("Label" + lines[i] + "m", typeof(MeshRenderer), typeof(TextMesh));
                label.transform.SetParent(loft, false);
                label.transform.position = new Vector3(-2.2f, 0.35f, lines[i] - 0.25f);
                label.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var tm = label.GetComponent<TextMesh>();
                tm.font = font; tm.text = lines[i].ToString("0") + " м  ×" + (i + 1); tm.fontSize = 64; tm.characterSize = 0.05f;
                tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(0.23f, 0.14f, 0.19f);
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            // немного мебели для масштаба
            Prim(PrimitiveType.Cube, "SofaSeat", loft, new Vector3(3.6f, 0.25f, 9f), new Vector3(1.8f, 0.5f, 0.8f), sofaMat);
            Prim(PrimitiveType.Cube, "SofaBack", loft, new Vector3(3.6f, 0.8f, 9.35f), new Vector3(1.8f, 0.6f, 0.25f), sofaMat);
            Prim(PrimitiveType.Cylinder, "Pot", loft, new Vector3(-3.8f, 0.2f, 2f), new Vector3(0.4f, 0.2f, 0.4f), potMat);
            Prim(PrimitiveType.Sphere, "Plant", loft, new Vector3(-3.8f, 0.85f, 2f), new Vector3(0.9f, 0.9f, 0.9f), plantMat);
            Prim(PrimitiveType.Cylinder, "LampPole", loft, new Vector3(3.9f, 0.85f, -2.5f), new Vector3(0.04f, 0.85f, 0.04f), darkMat);
            Prim(PrimitiveType.Cylinder, "LampShade", loft, new Vector3(3.9f, 1.8f, -2.5f), new Vector3(0.6f, 0.18f, 0.6f), lampMat);
            Prim(PrimitiveType.Cube, "Window1", loft, new Vector3(-4.88f, 2f, 6f), new Vector3(0.05f, 1.6f, 3f), ceilingMat, false);
            Prim(PrimitiveType.Cube, "Window2", loft, new Vector3(-4.88f, 2f, 12f), new Vector3(0.05f, 1.6f, 3f), ceilingMat, false);

            // кресло и Модель
            var model = new GameObject("Model").transform;
            Prim(PrimitiveType.Cube, "ChairSeat", model, new Vector3(0f, 0.5f, 0f), new Vector3(0.62f, 0.1f, 0.62f), chairMat);
            Prim(PrimitiveType.Cube, "ChairBack", model, new Vector3(0f, 0.9f, -0.3f), new Vector3(0.62f, 0.7f, 0.1f), chairMat);
            Prim(PrimitiveType.Cylinder, "ChairPole", model, new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.225f, 0.08f), metalMat);
            Prim(PrimitiveType.Cylinder, "ChairBase", model, new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.02f, 0.7f), metalMat);
            Prim(PrimitiveType.Sphere, "Body", model, new Vector3(0f, 0.92f, 0.05f), new Vector3(0.68f, 0.71f, 0.58f), apronMat);

            var pivot = new GameObject("HeadPivot");
            pivot.transform.SetParent(model, false);
            pivot.transform.position = new Vector3(0f, 1.38f, 0f);
            var headRig = pivot.AddComponent<HeadRig>();

            var headGo = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            headGo.transform.SetParent(pivot.transform, false);
            headGo.transform.localScale = new Vector3(1f, 1.15f, 1f); // «яйцо»
            headGo.GetComponent<MeshFilter>().sharedMesh = headVisual;
            headGo.GetComponent<MeshRenderer>().sharedMaterial = headMat;
            var hc = headGo.GetComponent<MeshCollider>(); hc.sharedMesh = headCollider; hc.convex = true;
            var paint = headGo.AddComponent<PaintSurface>();
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, s < 0 ? "EarRight" : "EarLeft", headGo.transform, Vector3.zero, new Vector3(0.07f, 0.12f, 0.12f), skinMat, false).transform.localPosition = new Vector3(s * 0.3f, 0.02f, 0f);

            // стрелок
            var rig = new GameObject("ShooterRig");
            rig.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 3f), Quaternion.Euler(0f, 180f, 0f));
            var camGo = new GameObject("ShooterCamera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            var shooterCam = SetupCamera(camGo.GetComponent<Camera>(), 60f);

            var viewModel = new GameObject("ViewModel").transform;
            viewModel.SetParent(camGo.transform, false);
            viewModel.localPosition = new Vector3(0.32f, -0.28f, 0.55f);
            viewModel.localRotation = Quaternion.Euler(0f, -4f, 0f);
            LocalPrim(PrimitiveType.Cylinder, "Tube", viewModel, Vector3.zero, new Vector3(0.07f, 0.21f, 0.07f), tubeMat);
            LocalPrim(PrimitiveType.Cylinder, "Tip", viewModel, new Vector3(0f, 0f, 0.27f), new Vector3(0.05f, 0.06f, 0.05f), redMat);
            LocalPrim(PrimitiveType.Cylinder, "Scope", viewModel, new Vector3(0f, 0.05f, -0.02f), new Vector3(0.04f, 0.1f, 0.04f), darkMat);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(camGo.transform, false);
            muzzle.localPosition = new Vector3(0.32f, -0.24f, 0.9f);

            var weapons = rig.AddComponent<WeaponSystem>();
            weapons.aimCamera = shooterCam; weapons.muzzle = muzzle; weapons.weapons = new[] { lipstick, bazooka };
            weapons.redMaterial = redMat; weapons.pinkMaterial = pinkMat; weapons.blackMaterial = blackMat;
            var shooter = rig.AddComponent<ShooterController>();
            shooter.cam = shooterCam; shooter.weapons = weapons; shooter.viewModel = viewModel;

            // камера Модели: чуть впереди лица, смотрит на стрелков, своего лица не видит
            var modelCamGo = new GameObject("ModelCamera", typeof(Camera));
            modelCamGo.transform.SetParent(pivot.transform, false); // камера поворачивается вместе с головой
            modelCamGo.transform.localPosition = new Vector3(0f, 0.04f, 0.5f);
            var modelCam = SetupCamera(modelCamGo.GetComponent<Camera>(), 70f);
            modelCam.enabled = false;

            // камера раскрытия: крупный план лица
            var revealCamGo = new GameObject("RevealCamera", typeof(Camera));
            revealCamGo.transform.SetPositionAndRotation(new Vector3(0f, 1.42f, 1.3f), Quaternion.Euler(0f, 180f, 0f));
            var revealCam = SetupCamera(revealCamGo.GetComponent<Camera>(), 40f);
            revealCam.enabled = false;

            // управление раундом
            var game = new GameObject("Game");
            var ui = game.AddComponent<GameUI>();
            var sfx = game.AddComponent<Sfx>();
            var rm = game.AddComponent<RoundManager>();
            rm.references = new[] { clown, panda, kiss };
            rm.face = paint; rm.head = headRig; rm.shooter = shooter; rm.weapons = weapons;
            rm.shooterCamera = shooterCam; rm.modelCamera = modelCam; rm.revealCamera = revealCam; rm.ui = ui; rm.sfx = sfx;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.DeleteAsset("Assets/Scenes"); // сцена-пример из шаблона
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MakeupSniper] Сцена недели 1 собрана: " + ScenePath);
        }

        // ---------- помощники ----------

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static Material Mat(string name, Color color, float smoothness = 0.12f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.Exception("Не найден шейдер URP Lit. Проверь, что пакет Universal RP установлен.");
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh MeshAsset(string name, Mesh fresh)
        {
            string path = Root + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // обновляем на месте, чтобы не менялся идентификатор ассета
                EditorUtility.CopySerialized(fresh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(fresh);
                return existing;
            }
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        static T Asset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            return a;
        }

        static ZoneTarget Zone(string name, Vector2 center, Vector2 radius, PaintColor color, ZoneStyle style)
        {
            return new ZoneTarget { name = name, center = center, radius = radius, color = color, style = style };
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // деталь оружия в руках: цилиндр, повёрнутый вдоль взгляда, без коллайдера
        static void LocalPrim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static Camera SetupCamera(Camera cam, float fov)
        {
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.95f, 0.89f, 0.92f);
            return cam;
        }
    }

    /// <summary>При открытии проекта сам открывает сцену недели 1, если открыта пустая.</summary>
    [InitializeOnLoad]
    static class AutoOpenScene
    {
        static AutoOpenScene()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool("MakeupSniper.AutoOpened", false)) return;
                SessionState.SetBool("MakeupSniper.AutoOpened", true);
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && File.Exists(SceneBuilder.ScenePath))
                    EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            };
        }
    }
}
