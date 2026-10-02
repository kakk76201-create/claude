using System.Collections.Generic;
using System.IO;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using MakeupSniper.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MakeupSniper.EditorTools
{
    /// <summary>
    /// Собирает игру кодом: сцену, места, человечков, стволы, образы и сеть.
    /// Меню Unity: Makeup Sniper → Пересобрать сцену. Правки сцены вносить сюда, а не руками.
    /// </summary>
    public static class SceneBuilder
    {
        public const string Root = "Assets/MakeupSniper";
        public const string ScenePath = Root + "/Scenes/Game.unity";
        const string OldScenePath = Root + "/Scenes/Week1.unity";

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material brushMat;

        [MenuItem("Makeup Sniper/Пересобрать сцену")]
        public static void Build()
        {
            foreach (var f in new[] { "Scenes", "Materials", "Data", "Meshes", "Prefabs" }) EnsureFolder(Root, f);
            mats.Clear();

            // ---------- материалы ----------
            Mat("Floor", new Color(0.91f, 0.84f, 0.78f));
            Mat("WallMint", new Color(0.84f, 0.94f, 0.89f));
            Mat("WallPeach", new Color(0.97f, 0.86f, 0.78f));
            Mat("Ceiling", new Color(1f, 0.97f, 0.95f));
            Mat("Tape", new Color(0.95f, 0.79f, 0.30f));
            Mat("Chair", new Color(0.96f, 0.65f, 0.75f));
            Mat("Metal", new Color(0.54f, 0.54f, 0.58f), 0.5f);
            Mat("Apron", new Color(0.62f, 0.85f, 0.96f));
            Mat("Skin", new Color(0.96f, 0.86f, 0.82f));
            Mat("Head", Color.white, 0.2f);
            Mat("Sofa", new Color(0.72f, 0.85f, 0.96f));
            Mat("Plant", new Color(0.56f, 0.81f, 0.60f));
            Mat("Pot", new Color(0.85f, 0.65f, 0.48f));
            Mat("Lamp", new Color(1f, 0.85f, 0.48f));
            Mat("Dark", new Color(0.2f, 0.2f, 0.22f));
            Mat("White", new Color(0.98f, 0.97f, 0.97f));
            Mat("Wood", new Color(0.72f, 0.52f, 0.38f));
            Mat("Suit", new Color(0.25f, 0.26f, 0.32f));
            Mat("Dress", new Color(0.98f, 0.62f, 0.74f));
            Mat("Hair", new Color(0.35f, 0.23f, 0.18f));
            Mat("Gold", new Color(0.95f, 0.78f, 0.35f), 0.6f);
            Mat("Flame", new Color(1f, 0.7f, 0.25f));
            Mat("BalloonA", new Color(0.36f, 0.62f, 0.95f));
            Mat("BalloonB", new Color(0.98f, 0.78f, 0.25f));
            Mat("BalloonC", new Color(0.55f, 0.85f, 0.45f));
            Mat("BalloonD", new Color(0.92f, 0.35f, 0.55f));
            Mat("KidShirtA", new Color(1f, 0.82f, 0.4f));
            Mat("KidShirtB", new Color(0.56f, 0.83f, 0.96f));
            Mat("KidShirtC", new Color(0.72f, 0.91f, 0.58f));
            Mat("LipstickTube", new Color(0.95f, 0.79f, 0.65f), 0.6f);
            Mat("PaintRed", (Color)PaintColors.ToColor(PaintColor.Red), 0.6f);
            Mat("PaintPink", (Color)PaintColors.ToColor(PaintColor.Pink), 0.3f);
            Mat("PaintBlack", (Color)PaintColors.ToColor(PaintColor.Black), 0.4f);
            Mat("Foundation", new Color(0.93f, 0.82f, 0.72f), 0.3f);
            Material eraserMat = ShaderMaterial("EraserBlit", "Hidden/MakeupSniper/EraserBlit");
            brushMat = ShaderMaterial("BrushBlit", "Hidden/MakeupSniper/BrushBlit");
            RenderTexture mirrorRt = MirrorTexture();

            // ---------- меши головы ----------
            Mesh headVisual = MeshAsset("HeadVisual", HeadMeshFactory.CreateVisual(0.3f, 0.7f));
            Mesh headCollider = MeshAsset("HeadCollider", HeadMeshFactory.CreateCollider(0.3f));

            // ---------- стволы ----------
            var weapons = BuildWeapons();
            // ---------- образы и места ----------
            var locations = BuildLocations();

            // ---------- префабы ----------
            NameHitboxLayer();
            NetworkObject playerPrefab = BuildPlayerPrefab(weapons);
            NetworkObject matchPrefab = BuildMatchPrefab();
            RefreshDefaultPrefabs();

            // ---------- сцена ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.53f, 0.55f);

            var sun = new GameObject("Sun", typeof(Light));
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 0.7f; light.shadows = LightShadows.Soft; light.color = new Color(1f, 0.98f, 0.95f);
            sun.transform.rotation = Quaternion.Euler(50f, 160f, 0f);
            sun.AddComponent<LoftLighting>();

            var worldGo = new GameObject("World");
            var world = worldGo.AddComponent<World>();
            world.weapons = weapons;
            world.locations = locations;

            BuildRoom(worldGo.transform, world);
            BuildModel(worldGo.transform, world, headVisual, headCollider, eraserMat);
            BuildGroom(worldGo.transform, world, headVisual, headCollider, eraserMat);
            BuildLocationProps(worldGo.transform, world, locations);

            // точки появления стрелков на линии 3 м
            var spawns = new List<Transform>();
            float[] xs = { -1.2f, 1.2f, -0.4f, 0.4f };
            for (int i = 0; i < xs.Length; i++)
            {
                var s = new GameObject("Spawn" + (i + 1)).transform;
                s.SetParent(worldGo.transform, false);
                s.position = new Vector3(xs[i], 0.05f, 3.3f + 0.25f * (i / 2));
                spawns.Add(s);
            }
            world.shooterSpawns = spawns.ToArray();

            // камеры
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 2.4f, 6.5f), Quaternion.Euler(10f, 180f, 0f));
            var cam = camGo.GetComponent<Camera>();
            SetupCamera(cam, 60f);
            world.mainCamera = cam;

            var mirrorGo = new GameObject("MirrorCamera", typeof(Camera));
            mirrorGo.transform.SetPositionAndRotation(new Vector3(0f, 1.40f, 1.05f), Quaternion.Euler(0f, 180f, 0f));
            var mirrorCam = mirrorGo.GetComponent<Camera>();
            SetupCamera(mirrorCam, 34f);
            mirrorCam.targetTexture = mirrorRt;
            mirrorCam.enabled = false;
            world.mirrorCamera = mirrorCam;
            world.mirrorTexture = mirrorRt;

            world.revealPose = Pose(worldGo.transform, "RevealPose", new Vector3(0f, 1.42f, 1.3f), new Vector3(0f, 180f, 0f));
            world.revealPoseWide = Pose(worldGo.transform, "RevealPoseWide", new Vector3(0.58f, 1.45f, 2.3f), new Vector3(2f, 180f, 0f));
            world.menuPivot = Pose(worldGo.transform, "MenuPivot", new Vector3(0f, 1.3f, 0f), Vector3.zero);

            // игра: интерфейс, управление, звук, эффекты
            var game = new GameObject("Game");
            game.AddComponent<GameUI>();
            game.AddComponent<LocalController>();
            world.sfx = game.AddComponent<Sfx>();
            var fx = game.AddComponent<Fx>();
            fx.redMaterial = mats["PaintRed"]; fx.pinkMaterial = mats["PaintPink"]; fx.blackMaterial = mats["PaintBlack"]; fx.skinMaterial = mats["Foundation"];
            world.fx = fx;
            game.AddComponent<AutoTest>();

            BuildNetwork(playerPrefab, matchPrefab);

            world.ShowLocation(0);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists(OldScenePath)) AssetDatabase.DeleteAsset(OldScenePath);
            if (AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.DeleteAsset("Assets/Scenes");

            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.forceSingleInstance = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MakeupSniper] Сцена собрана: " + ScenePath);
        }

        // ================= стволы =================

        static WeaponDef[] BuildWeapons()
        {
            var lipstick = Asset<WeaponDef>(Root + "/Data/Weapon_1_Lipstick.asset");
            lipstick.displayName = "Помада-снайперка"; lipstick.shortName = "помада"; lipstick.kind = WeaponKind.Hitscan; lipstick.color = PaintColor.Red;
            lipstick.eraser = false; lipstick.brush = BrushKind.Kiss; lipstick.spotRadius = 0.015f; lipstick.stripeLength = 0f;
            lipstick.reload = 1.5f; lipstick.charges = -1; lipstick.canZoom = true; lipstick.zoomFov = 15f; lipstick.spreadDeg = 0.35f; lipstick.spreadZoomDeg = 0.06f;
            lipstick.splattersPlayers = false; lipstick.bodyColor = new Color(0.95f, 0.79f, 0.65f);
            EditorUtility.SetDirty(lipstick);

            var mascara = Asset<WeaponDef>(Root + "/Data/Weapon_2_Mascara.asset");
            mascara.displayName = "Тушь-арбалет"; mascara.shortName = "тушь"; mascara.kind = WeaponKind.Projectile; mascara.color = PaintColor.Black;
            mascara.eraser = false; mascara.brush = BrushKind.Stripe; mascara.spotRadius = 0.009f; mascara.stripeLength = 0.06f;
            mascara.reload = 1.5f; mascara.charges = -1; mascara.canZoom = false; mascara.spreadDeg = 0.4f; mascara.spreadZoomDeg = 0.4f;
            mascara.projectileSpeed = 45f; mascara.gravityScale = 1f; mascara.projectileSize = 0.07f; mascara.splattersPlayers = true;
            mascara.bodyColor = new Color(0.25f, 0.2f, 0.25f);
            EditorUtility.SetDirty(mascara);

            var bazooka = Asset<WeaponDef>(Root + "/Data/Weapon_3_Bazooka.asset");
            bazooka.displayName = "Румяна-базука"; bazooka.shortName = "румяна"; bazooka.kind = WeaponKind.Projectile; bazooka.color = PaintColor.Pink;
            bazooka.eraser = false; bazooka.brush = BrushKind.Blush; bazooka.spotRadius = 0.05f; bazooka.stripeLength = 0f;
            bazooka.reload = 3f; bazooka.charges = -1; bazooka.canZoom = false; bazooka.spreadDeg = 0.8f; bazooka.spreadZoomDeg = 0.8f;
            bazooka.projectileSpeed = 24f; bazooka.gravityScale = 1f; bazooka.projectileSize = 0.16f; bazooka.splattersPlayers = true;
            bazooka.bodyColor = new Color(1f, 0.56f, 0.69f);
            EditorUtility.SetDirty(bazooka);

            var eraser = Asset<WeaponDef>(Root + "/Data/Weapon_4_Foundation.asset");
            eraser.displayName = "Тональник-ластик"; eraser.shortName = "ластик"; eraser.kind = WeaponKind.Projectile; eraser.color = PaintColor.None;
            eraser.eraser = true; eraser.brush = BrushKind.Eraser; eraser.spotRadius = 0.1f; eraser.stripeLength = 0f;
            eraser.reload = 2f; eraser.charges = 3; eraser.canZoom = false; eraser.spreadDeg = 0.5f; eraser.spreadZoomDeg = 0.5f;
            eraser.projectileSpeed = 30f; eraser.gravityScale = 0.6f; eraser.projectileSize = 0.12f; eraser.splattersPlayers = false;
            eraser.bodyColor = new Color(0.93f, 0.82f, 0.72f);
            EditorUtility.SetDirty(eraser);

            return new[] { lipstick, mascara, bazooka, eraser };
        }

        // ================= образы и места =================

        static readonly Vector2 Nose = new Vector2(0.50f, 0.47f), NoseR = new Vector2(0.055f, 0.05f);
        static readonly Vector2 Lips = new Vector2(0.50f, 0.33f), LipsR = new Vector2(0.10f, 0.045f);
        static readonly Vector2 CheekL = new Vector2(0.69f, 0.43f), CheekR = new Vector2(0.31f, 0.43f), CheekRad = new Vector2(0.08f, 0.07f);
        static readonly Vector2 EyeL = new Vector2(0.61f, 0.60f), EyeR = new Vector2(0.39f, 0.60f), EyeRad = new Vector2(0.07f, 0.06f);

        static ZoneTarget Z(string name, Vector2 c, Vector2 r, PaintColor color, ZoneStyle style, float required = 0.7f)
        {
            return new ZoneTarget { name = name, center = c, radius = r, color = color, style = style, required = required };
        }

        static ReferenceData Ref(string file, string name, string[] taboo, ZoneTarget[] zones, ZoneTarget[] dirt = null)
        {
            var r = Asset<ReferenceData>(Root + "/Data/Ref_" + file + ".asset");
            r.displayName = name;
            r.taboo = taboo;
            r.zones = zones;
            r.startDirt = dirt ?? new ZoneTarget[0];
            EditorUtility.SetDirty(r);
            return r;
        }

        static ReferenceSet Set(params ReferenceData[] faces) { return new ReferenceSet { faces = faces }; }

        static LocationDef[] BuildLocations()
        {
            var clown = Ref("Clown", "Клоун", new[] { "клоун", "нос", "красный", "розовый", "цирк" }, new[]
            {
                Z("Нос", Nose, NoseR, PaintColor.Red, ZoneStyle.Solid), Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips),
                Z("Левая щека", CheekL, CheekRad, PaintColor.Pink, ZoneStyle.Soft), Z("Правая щека", CheekR, CheekRad, PaintColor.Pink, ZoneStyle.Soft)
            });
            var panda = Ref("Panda", "Панда", new[] { "панда", "медведь", "глаз", "чёрный", "бамбук" }, new[]
            {
                Z("Левый глаз", EyeL, EyeRad, PaintColor.Black, ZoneStyle.Solid), Z("Правый глаз", EyeR, EyeRad, PaintColor.Black, ZoneStyle.Solid),
                Z("Нос", Nose, NoseR, PaintColor.Black, ZoneStyle.Solid)
            });
            var kiss = Ref("Kiss", "След поцелуя", new[] { "поцелуй", "губы", "щека", "красный", "чмок" }, new[]
            {
                Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips), Z("Левая щека", CheekL, CheekRad, PaintColor.Red, ZoneStyle.Kiss)
            });
            var pirate = Ref("Pirate", "Пират", new[] { "пират", "повязка", "глаз", "ром", "нос" }, new[]
            {
                Z("Левый глаз", EyeL, EyeRad * 1.1f, PaintColor.Black, ZoneStyle.Patch), Z("Нос", Nose, NoseR, PaintColor.Red, ZoneStyle.Solid)
            });
            var granny = Ref("Granny", "Бабушкин румянец", new[] { "бабушка", "щёки", "румяна", "губы", "пирожки" }, new[]
            {
                Z("Левая щека", CheekL, CheekRad * 1.35f, PaintColor.Pink, ZoneStyle.Soft), Z("Правая щека", CheekR, CheekRad * 1.35f, PaintColor.Pink, ZoneStyle.Soft),
                Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips)
            });
            var cat = Ref("Cat", "Кот с усами", new[] { "кот", "кошка", "усы", "мяу", "нос" }, new[]
            {
                Z("Нос", Nose, NoseR, PaintColor.Black, ZoneStyle.Solid),
                Z("Усы слева", new Vector2(0.73f, 0.42f), new Vector2(0.11f, 0.06f), PaintColor.Black, ZoneStyle.Whiskers, 0.3f),
                Z("Усы справа", new Vector2(0.27f, 0.42f), new Vector2(0.11f, 0.06f), PaintColor.Black, ZoneStyle.Whiskers, 0.3f)
            });
            var mime = Ref("Mime", "Мим", new[] { "мим", "губы", "слеза", "чёрный", "грусть" }, new[]
            {
                Z("Губы", Lips, LipsR, PaintColor.Black, ZoneStyle.Lips), Z("Слеза под левым глазом", new Vector2(0.62f, 0.49f), new Vector2(0.03f, 0.045f), PaintColor.Black, ZoneStyle.Tear, 0.5f)
            });
            var freckles = Ref("Freckles", "Веснушки", new[] { "веснушки", "точки", "щёки", "солнце", "нос" }, new[]
            {
                Z("Веснушки слева", new Vector2(0.67f, 0.46f), new Vector2(0.10f, 0.08f), PaintColor.Black, ZoneStyle.Freckles, 0.2f),
                Z("Веснушки справа", new Vector2(0.33f, 0.46f), new Vector2(0.10f, 0.08f), PaintColor.Black, ZoneStyle.Freckles, 0.2f),
                Z("Нос (обгорел)", Nose, NoseR, PaintColor.Pink, ZoneStyle.Soft)
            });
            var tiger = Ref("Tiger", "Тигр", new[] { "тигр", "полоски", "лоб", "кот", "рыжий" }, new[]
            {
                Z("Лоб в полоску", new Vector2(0.5f, 0.76f), new Vector2(0.14f, 0.055f), PaintColor.Black, ZoneStyle.Stripes, 0.35f),
                Z("Левая щека в полоску", CheekL, new Vector2(0.09f, 0.07f), PaintColor.Black, ZoneStyle.Stripes, 0.35f),
                Z("Правая щека в полоску", CheekR, new Vector2(0.09f, 0.07f), PaintColor.Black, ZoneStyle.Stripes, 0.35f),
                Z("Нос", Nose, NoseR, PaintColor.Pink, ZoneStyle.Solid)
            });

            var hickey = Ref("Hickey", "Спрятать засос", new[] { "засос", "шея", "стереть", "тональник", "пятно" },
                new[] { Z("Засос — стереть", new Vector2(0.64f, 0.21f), new Vector2(0.075f, 0.065f), PaintColor.None, ZoneStyle.Solid) },
                new[] { Z("Засос", new Vector2(0.64f, 0.21f), new Vector2(0.045f, 0.045f), PaintColor.Red, ZoneStyle.Soft) });
            var exKiss = Ref("ExKiss", "Стереть чужой поцелуй", new[] { "поцелуй", "щека", "стереть", "бывшая", "помада" },
                new[] { Z("Поцелуй на щеке — стереть", CheekL, new Vector2(0.09f, 0.08f), PaintColor.None, ZoneStyle.Solid) },
                new[] { Z("Чужой поцелуй", CheekL, new Vector2(0.06f, 0.06f), PaintColor.Red, ZoneStyle.Kiss) });
            var frameUp = Ref("FrameUp", "Подставить: чужая помада", new[] { "поцелуй", "губы", "помада", "щека", "измена" }, new[]
            {
                Z("Правая щека", CheekR, CheekRad, PaintColor.Red, ZoneStyle.Kiss), Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips)
            });
            var shy = Ref("Shy", "Смущение", new[] { "смущение", "щёки", "розовый", "краснеет", "стесняется" }, new[]
            {
                Z("Левая щека", CheekL, CheekRad * 1.3f, PaintColor.Pink, ZoneStyle.Soft), Z("Правая щека", CheekR, CheekRad * 1.3f, PaintColor.Pink, ZoneStyle.Soft),
                Z("Кончик носа", Nose, NoseR * 0.9f, PaintColor.Pink, ZoneStyle.Soft)
            });

            var bride = Ref("Bride", "Невеста", new[] { "невеста", "свадьба", "губы", "щёки", "ресницы" }, new[]
            {
                Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips),
                Z("Левая щека", CheekL, CheekRad, PaintColor.Pink, ZoneStyle.Soft), Z("Правая щека", CheekR, CheekRad, PaintColor.Pink, ZoneStyle.Soft),
                Z("Ресницы слева", new Vector2(0.61f, 0.645f), new Vector2(0.065f, 0.028f), PaintColor.Black, ZoneStyle.Stripes, 0.45f),
                Z("Ресницы справа", new Vector2(0.39f, 0.645f), new Vector2(0.065f, 0.028f), PaintColor.Black, ZoneStyle.Stripes, 0.45f)
            });
            var groomCake = Ref("GroomCake", "Жених после торта", new[] { "жених", "вино", "торт", "стереть", "нос" },
                new[]
                {
                    Z("Вино на губах — стереть", Lips, new Vector2(0.10f, 0.075f), PaintColor.None, ZoneStyle.Solid),
                    Z("Крем на носу — стереть", Nose, new Vector2(0.07f, 0.065f), PaintColor.None, ZoneStyle.Solid)
                },
                new[]
                {
                    Z("Вино", Lips, new Vector2(0.055f, 0.055f), PaintColor.Red, ZoneStyle.Soft),
                    Z("Крем", Nose, new Vector2(0.045f, 0.045f), PaintColor.Pink, ZoneStyle.Solid)
                });
            var bride2 = Ref("Bride2", "Невеста после девичника", new[] { "синяк", "глаз", "губы", "девичник", "невеста" }, new[]
            {
                Z("Синяк под левым глазом", EyeL, new Vector2(0.08f, 0.07f), PaintColor.Black, ZoneStyle.Solid), Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips)
            });
            var groom2 = Ref("Groom2", "Жених с пощёчиной", new[] { "пощёчина", "щека", "губы", "жених", "ладонь" }, new[]
            {
                Z("След пощёчины", CheekL, new Vector2(0.085f, 0.075f), PaintColor.Red, ZoneStyle.Solid), Z("Губы", Lips, LipsR, PaintColor.Red, ZoneStyle.Lips)
            });

            var loft = Asset<LocationDef>(Root + "/Data/Location_1_Loft.asset");
            loft.displayName = "Лофт"; loft.seconds = 60f; loft.gimmick = Gimmick.None; loft.propRoot = "Loft";
            loft.description = "Дом бригады. Тренировка без помех: красим друг друга как хотим.";
            loft.wallColor = new Color(0.97f, 0.86f, 0.78f);
            loft.pool = new[] { Set(clown), Set(panda), Set(kiss), Set(pirate), Set(granny), Set(cat), Set(mime), Set(freckles) };
            EditorUtility.SetDirty(loft);

            var party = Asset<LocationDef>(Root + "/Data/Location_2_Party.asset");
            party.displayName = "Детский праздник"; party.seconds = 60f; party.gimmick = Gimmick.Kids; party.propRoot = "Party";
            party.description = "Дети носятся перед креслом и ловят выстрелы. Попал в ребёнка — минус 5 секунд и минус очки.";
            party.wallColor = new Color(1f, 0.93f, 0.72f);
            party.pool = new[] { Set(clown), Set(panda), Set(cat), Set(tiger) };
            EditorUtility.SetDirty(party);

            var date = Asset<LocationDef>(Root + "/Data/Location_3_Date.asset");
            date.displayName = "Ресторан: свидание"; date.seconds = 60f; date.gimmick = Gimmick.Witness; date.propRoot = "Date";
            date.description = "Его девушка иногда оборачивается. Пока она смотрит — не стреляй, иначе заметит! Заказы: спрятать засос, стереть чужой поцелуй… или подставить.";
            date.wallColor = new Color(0.86f, 0.76f, 0.72f);
            date.pool = new[] { Set(hickey), Set(exKiss), Set(frameUp), Set(shy) };
            EditorUtility.SetDirty(date);

            var wedding = Asset<LocationDef>(Root + "/Data/Location_4_Wedding.asset");
            wedding.displayName = "Свадьба (босс)"; wedding.seconds = 90f; wedding.gimmick = Gimmick.Wedding; wedding.propRoot = "Wedding";
            wedding.description = "Невеста и жених сразу, 90 секунд. Двое гостей оборачиваются. Накрасить невесту, стереть с жениха вино и торт.";
            wedding.wallColor = new Color(1f, 0.94f, 0.96f);
            wedding.pool = new[] { Set(bride, groomCake), Set(bride2, groom2) };
            EditorUtility.SetDirty(wedding);

            return new[] { loft, party, date, wedding };
        }

        // ================= комната =================

        static void BuildRoom(Transform parent, World world)
        {
            var loft = new GameObject("Room").transform;
            loft.SetParent(parent, false);
            Prim(PrimitiveType.Cube, "Floor", loft, new Vector3(0f, -0.1f, 9f), new Vector3(10f, 0.2f, 28f), "Floor");
            NoShadow(Prim(PrimitiveType.Cube, "WallBack", loft, new Vector3(0f, 1.9f, -4f), new Vector3(10f, 3.8f, 0.2f), "WallMint"));
            NoShadow(Prim(PrimitiveType.Cube, "WallFar", loft, new Vector3(0f, 1.9f, 22f), new Vector3(10f, 3.8f, 0.2f), "WallMint"));
            var wl = NoShadow(Prim(PrimitiveType.Cube, "WallLeft", loft, new Vector3(-5f, 1.9f, 9f), new Vector3(0.2f, 3.8f, 28f), "WallPeach"));
            var wr = NoShadow(Prim(PrimitiveType.Cube, "WallRight", loft, new Vector3(5f, 1.9f, 9f), new Vector3(0.2f, 3.8f, 28f), "WallPeach"));
            NoShadow(Prim(PrimitiveType.Cube, "Ceiling", loft, new Vector3(0f, 3.9f, 9f), new Vector3(10f, 0.2f, 28f), "Ceiling"));
            world.tintedWalls = new[] { wl.GetComponent<Renderer>(), wr.GetComponent<Renderer>() };
            Prim(PrimitiveType.Cube, "Window1", loft, new Vector3(-4.88f, 2f, 6f), new Vector3(0.05f, 1.6f, 3f), "Ceiling", false);
            Prim(PrimitiveType.Cube, "Window2", loft, new Vector3(-4.88f, 2f, 12f), new Vector3(0.05f, 1.6f, 3f), "Ceiling", false);
            Prim(PrimitiveType.Cube, "Window3", loft, new Vector3(4.88f, 2f, 15f), new Vector3(0.05f, 1.6f, 3f), "Ceiling", false);

            float[] lines = { 3f, 8f, 15f };
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < lines.Length; i++)
            {
                Prim(PrimitiveType.Cube, "Line" + lines[i] + "m", loft, new Vector3(0f, 0.006f, lines[i]), new Vector3(9f, 0.012f, 0.09f), "Tape", false);
                foreach (float x in new[] { -2.6f, 2.6f })
                {
                    var label = new GameObject("Label" + lines[i] + "m", typeof(MeshRenderer), typeof(TextMesh));
                    label.transform.SetParent(loft, false);
                    label.transform.position = new Vector3(x, 0.02f, lines[i] + 0.35f);
                    label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
                    var tm = label.GetComponent<TextMesh>();
                    tm.font = font; tm.text = lines[i].ToString("0") + " м  ×" + (i + 1); tm.fontSize = 64; tm.characterSize = 0.045f;
                    tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(0.23f, 0.14f, 0.19f);
                    label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
            }
        }

        // ================= Модель в кресле =================

        static void BuildModel(Transform parent, World world, Mesh headVisual, Mesh headCollider, Material eraserMat)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            Chair(model, Vector3.zero, "Chair");
            var body = Prim(PrimitiveType.Sphere, "Body", model, new Vector3(0f, 0.92f, 0.05f), new Vector3(0.68f, 0.71f, 0.58f), "Apron");
            world.modelApron = body.GetComponent<Renderer>();

            var pivot = new GameObject("HeadPivot");
            pivot.transform.SetParent(model, false);
            pivot.transform.position = new Vector3(0f, 1.38f, 0f);
            var rig = pivot.AddComponent<HeadRig>();
            rig.source = HeadRig.Source.Bot;

            PaintSurface face = Head(pivot.transform, "Head", headVisual, headCollider, eraserMat, 0);

            // ладонь-щит перед лицом
            var palm = Prim(PrimitiveType.Sphere, "Palm", pivot.transform, Vector3.zero, new Vector3(0.24f, 0.28f, 0.06f), "Skin");
            palm.transform.localPosition = new Vector3(0.02f, -0.02f, 0.42f);
            for (int i = 0; i < 4; i++)
            {
                var finger = Prim(PrimitiveType.Sphere, "Finger" + i, palm.transform, Vector3.zero, Vector3.one, "Skin", false);
                finger.transform.localPosition = new Vector3(-0.33f + i * 0.22f, 0.62f, 0f);
                finger.transform.localScale = new Vector3(0.2f, 0.55f, 1f);
            }
            rig.palm = palm.transform;
            rig.palmRenderer = palm.GetComponent<Renderer>();
            world.palmCollider = palm.GetComponent<Collider>();

            var eye = new GameObject("ModelEye").transform;
            eye.SetParent(pivot.transform, false);
            eye.localPosition = new Vector3(0f, 0.05f, 0.34f);
            world.modelEye = eye;

            world.faces = new[] { face, null };
            world.heads = new[] { rig, null };
        }

        static PaintSurface Head(Transform pivot, string name, Mesh headVisual, Mesh headCollider, Material eraserMat, byte index)
        {
            var headGo = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            headGo.transform.SetParent(pivot, false);
            headGo.transform.localScale = new Vector3(1f, 1.15f, 1f); // «яйцо»
            headGo.GetComponent<MeshFilter>().sharedMesh = headVisual;
            headGo.GetComponent<MeshRenderer>().sharedMaterial = mats["Head"];
            var hc = headGo.GetComponent<MeshCollider>(); hc.sharedMesh = headCollider; hc.convex = true;
            var paint = headGo.AddComponent<PaintSurface>();
            paint.faceIndex = index;
            paint.eraserMaterial = eraserMat;
            paint.brushMaterial = brushMat;
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, s < 0 ? "EarRight" : "EarLeft", headGo.transform, Vector3.zero, new Vector3(0.07f, 0.12f, 0.12f), "Skin", false).transform.localPosition = new Vector3(s * 0.3f, 0.02f, 0f);
            return paint;
        }

        static void Chair(Transform parent, Vector3 at, string mat)
        {
            Prim(PrimitiveType.Cube, "ChairSeat", parent, at + new Vector3(0f, 0.5f, 0f), new Vector3(0.62f, 0.1f, 0.62f), mat);
            Prim(PrimitiveType.Cube, "ChairBack", parent, at + new Vector3(0f, 0.9f, -0.3f), new Vector3(0.62f, 0.7f, 0.1f), mat);
            Prim(PrimitiveType.Cylinder, "ChairPole", parent, at + new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.225f, 0.08f), "Metal");
            Prim(PrimitiveType.Cylinder, "ChairBase", parent, at + new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.02f, 0.7f), "Metal");
        }

        // ================= жених (свадьба) =================

        static void BuildGroom(Transform parent, World world, Mesh headVisual, Mesh headCollider, Material eraserMat)
        {
            var groom = new GameObject("Groom").transform;
            groom.SetParent(parent, false);
            Vector3 at = new Vector3(1.15f, 0f, 0f);
            Chair(groom, at, "White");
            Prim(PrimitiveType.Sphere, "Body", groom, at + new Vector3(0f, 0.92f, 0.05f), new Vector3(0.68f, 0.71f, 0.58f), "Suit");
            Prim(PrimitiveType.Cube, "BowTie", groom, at + new Vector3(0f, 1.17f, 0.3f), new Vector3(0.16f, 0.07f, 0.04f), "Dark", false);
            var pivot = new GameObject("GroomHeadPivot");
            pivot.transform.SetParent(groom, false);
            pivot.transform.position = at + new Vector3(0f, 1.38f, 0f);
            var rig = pivot.AddComponent<HeadRig>();
            rig.source = HeadRig.Source.Bot;
            rig.idleSway = false;
            PaintSurface face = Head(pivot.transform, "GroomHead", headVisual, headCollider, eraserMat, 1);
            world.groomRoot = groom.gameObject;
            world.faces = new[] { world.faces[0], face };
            world.heads = new[] { world.heads[0], rig };
        }

        // ================= декорации мест =================

        static void BuildLocationProps(Transform parent, World world, LocationDef[] locations)
        {
            var roots = new GameObject[locations.Length];
            for (int i = 0; i < locations.Length; i++)
            {
                var r = new GameObject(locations[i].propRoot);
                r.transform.SetParent(parent, false);
                roots[i] = r;
            }
            world.locationProps = roots;

            // лофт: диван, цветок, лампа
            Transform loft = roots[0].transform;
            Prim(PrimitiveType.Cube, "SofaSeat", loft, new Vector3(3.6f, 0.25f, 9f), new Vector3(1.8f, 0.5f, 0.8f), "Sofa");
            Prim(PrimitiveType.Cube, "SofaBack", loft, new Vector3(3.6f, 0.8f, 9.35f), new Vector3(1.8f, 0.6f, 0.25f), "Sofa");
            Prim(PrimitiveType.Cylinder, "Pot", loft, new Vector3(-3.8f, 0.2f, 2f), new Vector3(0.4f, 0.2f, 0.4f), "Pot");
            Prim(PrimitiveType.Sphere, "Plant", loft, new Vector3(-3.8f, 0.85f, 2f), new Vector3(0.9f, 0.9f, 0.9f), "Plant");
            Prim(PrimitiveType.Cylinder, "LampPole", loft, new Vector3(3.9f, 0.85f, -2.5f), new Vector3(0.04f, 0.85f, 0.04f), "Dark");
            Prim(PrimitiveType.Cylinder, "LampShade", loft, new Vector3(3.9f, 1.8f, -2.5f), new Vector3(0.6f, 0.18f, 0.6f), "Lamp");

            // детский праздник: шарики, гирлянда, торт; дети бегают
            Transform party = roots[1].transform;
            string[] balloon = { "BalloonA", "BalloonB", "BalloonC", "BalloonD" };
            for (int i = 0; i < 10; i++)
            {
                float x = -4.3f + (i % 2) * 8.6f, z = -2.5f + (i / 2) * 1.6f;
                Prim(PrimitiveType.Sphere, "Balloon" + i, party, new Vector3(x + 0.1f * (i % 3), 2.2f + 0.3f * (i % 3), z), new Vector3(0.45f, 0.55f, 0.45f), balloon[i % 4], false);
                Prim(PrimitiveType.Cylinder, "String" + i, party, new Vector3(x + 0.1f * (i % 3), 1.2f + 0.15f * (i % 3), z), new Vector3(0.01f, 0.8f, 0.01f), "White", false);
            }
            for (int i = 0; i < 18; i++)
                Prim(PrimitiveType.Cube, "Flag" + i, party, new Vector3(-4.2f + i * 0.5f, 3.2f - 0.15f * Mathf.Sin(i * 0.7f), -3.85f), new Vector3(0.28f, 0.32f, 0.02f), balloon[i % 4], false);
            Prim(PrimitiveType.Cylinder, "PartyTable", party, new Vector3(-3.2f, 0.4f, 0.5f), new Vector3(1.2f, 0.4f, 1.2f), "White");
            Prim(PrimitiveType.Cylinder, "Cake", party, new Vector3(-3.2f, 0.95f, 0.5f), new Vector3(0.6f, 0.15f, 0.6f), "Dress");
            Prim(PrimitiveType.Cylinder, "CakeTop", party, new Vector3(-3.2f, 1.18f, 0.5f), new Vector3(0.4f, 0.08f, 0.4f), "White");
            var kidsRoot = new GameObject("Kids");
            kidsRoot.transform.SetParent(parent, false);
            var kids = kidsRoot.AddComponent<KidsGimmick>();
            var kidList = new List<Transform>();
            string[] shirts = { "KidShirtA", "KidShirtB", "KidShirtC" };
            for (int i = 0; i < 3; i++)
            {
                var kid = new GameObject("Kid" + i);
                kid.transform.SetParent(kidsRoot.transform, false);
                kid.transform.localPosition = GimmickMath.KidPosition(1, i, 0f);
                var cap = kid.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0f, 0.55f, 0f); cap.radius = 0.24f; cap.height = 1.1f;
                Prim(PrimitiveType.Sphere, "Body", kid.transform, Vector3.zero, new Vector3(0.42f, 0.52f, 0.38f), shirts[i], false).transform.localPosition = new Vector3(0f, 0.42f, 0f);
                Prim(PrimitiveType.Sphere, "Head", kid.transform, Vector3.zero, new Vector3(0.32f, 0.34f, 0.32f), "Skin", false).transform.localPosition = new Vector3(0f, 0.86f, 0f);
                Prim(PrimitiveType.Cylinder, "Hat", kid.transform, Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), balloon[(i + 1) % 4], false).transform.localPosition = new Vector3(0f, 1.06f, 0f);
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Sphere, "Eye", kid.transform, Vector3.zero, new Vector3(0.04f, 0.05f, 0.03f), "Dark", false).transform.localPosition = new Vector3(s * 0.06f, 0.89f, 0.15f);
                Prim(PrimitiveType.Sphere, "Balloon", kid.transform, Vector3.zero, new Vector3(0.3f, 0.36f, 0.3f), balloon[i % 4], false).transform.localPosition = new Vector3(0.22f, 1.5f, -0.05f);
                kidList.Add(kid.transform);
            }
            kids.kids = kidList.ToArray();
            world.kids = kids;

            // ресторан: столик со свечой, девушка напротив
            Transform date = roots[2].transform;
            Table(date, new Vector3(0.72f, 0f, 0.45f), true, 0.6f);
            Table(date, new Vector3(-3.4f, 0f, 3.5f), false);
            Table(date, new Vector3(3.4f, 0f, 5.5f), false);
            var dateW = new GameObject("DateWitnesses");
            dateW.transform.SetParent(parent, false);
            var dw = dateW.AddComponent<WitnessGimmick>();
            Transform girlHead;
            // девушка сидит напротив парня через столик и смотрит в телефон, иногда поднимает глаза
            var girl = Witness(dateW.transform, "Girlfriend", new Vector3(1.35f, 0f, 0.45f), -90f, "Dress", "Hair", out girlHead);
            var phone = Prim(PrimitiveType.Cube, "Phone", girl.transform, Vector3.zero, new Vector3(0.09f, 0.16f, 0.015f), "Dark", false);
            phone.transform.localPosition = new Vector3(0.12f, 1.05f, 0.3f);
            phone.transform.localRotation = Quaternion.Euler(-50f, 0f, 0f);
            dw.roots = new[] { girl };
            dw.heads = new[] { girlHead };
            dw.awayYaw = 15f;
            dw.awayPitch = 38f;
            world.dateWitnesses = dw;

            // свадьба: арка с цветами, дорожка, торт, гости
            Transform wed = roots[3].transform;
            Prim(PrimitiveType.Cube, "ArchLeft", wed, new Vector3(-1.2f, 1.4f, -1.2f), new Vector3(0.18f, 2.8f, 0.18f), "White");
            Prim(PrimitiveType.Cube, "ArchRight", wed, new Vector3(2.35f, 1.4f, -1.2f), new Vector3(0.18f, 2.8f, 0.18f), "White");
            Prim(PrimitiveType.Cube, "ArchTop", wed, new Vector3(0.58f, 2.85f, -1.2f), new Vector3(3.8f, 0.2f, 0.2f), "White");
            for (int i = 0; i < 16; i++)
                Prim(PrimitiveType.Sphere, "Flower" + i, wed, new Vector3(-1.2f + i * 0.237f, 2.95f + 0.08f * Mathf.Sin(i * 1.3f), -1.1f), Vector3.one * 0.22f, i % 2 == 0 ? "Dress" : "White", false);
            Prim(PrimitiveType.Cube, "Carpet", wed, new Vector3(0.58f, 0.01f, 9f), new Vector3(1.4f, 0.02f, 18f), "White", false);
            Prim(PrimitiveType.Cylinder, "WeddingTable", wed, new Vector3(-3.5f, 0.4f, 0.8f), new Vector3(1.0f, 0.4f, 1.0f), "White");
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "CakeTier" + i, wed, new Vector3(-3.5f, 0.9f + i * 0.22f, 0.8f), new Vector3(0.7f - i * 0.18f, 0.11f, 0.7f - i * 0.18f), "White");
            var wedW = new GameObject("WeddingGuests");
            wedW.transform.SetParent(parent, false);
            var ww = wedW.AddComponent<WitnessGimmick>();
            Transform h1, h2;
            var g1 = Witness(wedW.transform, "GuestMom", new Vector3(-2.3f, 0f, 1.6f), 0f, "Dress", "Hair", out h1);
            var g2 = Witness(wedW.transform, "GuestUncle", new Vector3(2.9f, 0f, 1.7f), 0f, "Suit", "Dark", out h2);
            ww.roots = new[] { g1, g2 };
            ww.heads = new[] { h1, h2 };
            ww.awayYaw = 0f;
            world.weddingWitnesses = ww;
        }

        static void Table(Transform parent, Vector3 at, bool candle, float size = 0.8f)
        {
            Prim(PrimitiveType.Cylinder, "TableTop", parent, at + new Vector3(0f, 0.75f, 0f), new Vector3(size, 0.03f, size), "Wood");
            Prim(PrimitiveType.Cylinder, "TableLeg", parent, at + new Vector3(0f, 0.37f, 0f), new Vector3(0.08f, 0.37f, 0.08f), "Dark");
            Prim(PrimitiveType.Cylinder, "Glass", parent, at + new Vector3(0.18f, 0.86f, 0.1f), new Vector3(0.07f, 0.08f, 0.07f), "PaintRed", false);
            if (!candle) return;
            Prim(PrimitiveType.Cylinder, "Candle", parent, at + new Vector3(-0.1f, 0.85f, -0.05f), new Vector3(0.05f, 0.08f, 0.05f), "White", false);
            Prim(PrimitiveType.Sphere, "Flame", parent, at + new Vector3(-0.1f, 0.96f, -0.05f), new Vector3(0.03f, 0.05f, 0.03f), "Flame", false);
        }

        /// <summary>Свидетель: сидит на стуле лицом к стрелкам, голова поворачивается к креслу.</summary>
        static GameObject Witness(Transform parent, string name, Vector3 at, float yaw, string clothes, string hair, out Transform head)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.85f, 0f); cap.radius = 0.3f; cap.height = 1.75f;
            Prim(PrimitiveType.Cube, "Seat", root.transform, Vector3.zero, new Vector3(0.5f, 0.08f, 0.5f), "Wood", false).transform.localPosition = new Vector3(0f, 0.45f, 0f);
            Prim(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, new Vector3(0.58f, 0.7f, 0.5f), clothes, false).transform.localPosition = new Vector3(0f, 0.85f, 0f);
            var pivot = new GameObject("HeadPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.42f, 0f);
            Prim(PrimitiveType.Sphere, "Head", pivot, Vector3.zero, new Vector3(0.42f, 0.46f, 0.42f), "Skin", false);
            Prim(PrimitiveType.Sphere, "Hair", pivot, Vector3.zero, new Vector3(0.46f, 0.4f, 0.46f), hair, false).transform.localPosition = new Vector3(0f, 0.08f, -0.04f);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, "Eye", pivot, Vector3.zero, new Vector3(0.05f, 0.06f, 0.03f), "Dark", false).transform.localPosition = new Vector3(s * 0.08f, 0.03f, 0.19f);
            head = pivot;
            return root;
        }

        // ================= человечек игрока =================

        static NetworkObject BuildPlayerPrefab(WeaponDef[] weapons)
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f); cc.height = 1.8f; cc.radius = 0.3f; cc.stepOffset = 0.3f; cc.skinWidth = 0.04f;
            var nob = root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            var so = new SerializedObject(nt);
            SetProp(so, "_synchronizeScale", false);
            SetProp(so, "_clientAuthoritative", true);
            SetProp(so, "_sendToOwner", false);
            so.ApplyModifiedPropertiesWithoutUndo();

            var body = Prim(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, new Vector3(0.62f, 0.78f, 0.52f), "Apron", false);
            body.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            var head = Prim(PrimitiveType.Sphere, "Head", root.transform, Vector3.zero, new Vector3(0.46f, 0.52f, 0.46f), "Skin", false);
            head.transform.localPosition = new Vector3(0f, 1.42f, 0f);
            var eyes = new List<Renderer>();
            foreach (float s in new[] { -1f, 1f })
            {
                var eye = Prim(PrimitiveType.Sphere, "Eye", head.transform, Vector3.zero, new Vector3(0.12f, 0.14f, 0.08f), "Dark", false);
                eye.transform.localPosition = new Vector3(s * 0.18f, 0.08f, 0.44f);
                eyes.Add(eye.GetComponent<Renderer>());
            }
            var splat = Prim(PrimitiveType.Sphere, "SplatMark", head.transform, Vector3.zero, new Vector3(0.75f, 0.6f, 0.2f), "PaintPink", false);
            splat.transform.localPosition = new Vector3(0.05f, -0.05f, 0.45f);
            splat.SetActive(false);

            var anchor = new GameObject("CameraAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 1.55f, 0.05f);

            var holder = new GameObject("WeaponHolder").transform;
            holder.SetParent(root.transform, false);
            holder.localPosition = new Vector3(0.26f, 1.3f, 0f);
            var models = new GameObject[weapons.Length];
            models[0] = WeaponModel(holder, "Lipstick", new[]
            {
                P(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.35f), new Vector3(0.07f, 0.21f, 0.07f), "LipstickTube"),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.62f), new Vector3(0.05f, 0.06f, 0.05f), "PaintRed"),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0.33f), new Vector3(0.04f, 0.1f, 0.04f), "Dark"),
            });
            models[1] = WeaponModel(holder, "Crossbow", new[]
            {
                P(PrimitiveType.Cube, new Vector3(0f, 0f, 0.4f), new Vector3(0.06f, 0.07f, 0.5f), "Dark"),
                P(PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.6f), new Vector3(0.5f, 0.03f, 0.04f), "PaintPink"),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0.45f), new Vector3(0.025f, 0.2f, 0.025f), "PaintBlack"),
            });
            models[2] = WeaponModel(holder, "Bazooka", new[]
            {
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.35f), new Vector3(0.15f, 0.32f, 0.15f), "PaintPink"),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0.68f), new Vector3(0.18f, 0.03f, 0.18f), "White"),
            });
            models[3] = WeaponModel(holder, "Foundation", new[]
            {
                P(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.35f), new Vector3(0.11f, 0.18f, 0.11f), "Foundation"),
                P(PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.56f), new Vector3(0.05f, 0.04f, 0.05f), "White"),
            });

            var labelGo = new GameObject("NameLabel", typeof(MeshRenderer), typeof(TextMesh));
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tm = labelGo.GetComponent<TextMesh>();
            tm.font = font; tm.text = "Игрок"; tm.fontSize = 64; tm.characterSize = 0.022f; tm.anchor = TextAnchor.MiddleCenter;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            // мишень для попаданий: отдельная капсула на слое 8, с капсулами ходьбы не сталкивается
            var hitGo = new GameObject("HitBox");
            hitGo.layer = World.HitboxLayer;
            hitGo.transform.SetParent(root.transform, false);
            var hit = hitGo.AddComponent<CapsuleCollider>();
            hit.center = new Vector3(0f, 0.9f, 0f); hit.radius = 0.32f; hit.height = 1.8f;

            var agent = root.AddComponent<PlayerAgent>();
            agent.hitBox = hit;
            agent.cameraAnchor = anchor;
            agent.weaponHolder = holder;
            agent.weaponModels = models;
            agent.tintRenderers = new[] { body.GetComponent<Renderer>() };
            var hide = new List<Renderer> { body.GetComponent<Renderer>(), head.GetComponent<Renderer>() };
            hide.AddRange(eyes);
            agent.bodyRenderers = hide.ToArray();
            agent.nameLabel = tm;
            agent.splatMark = splat;

            string path = Root + "/Prefabs/Player.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        struct PartSpec { public PrimitiveType type; public Vector3 pos, scale; public string mat; }
        static PartSpec P(PrimitiveType t, Vector3 pos, Vector3 scale, string mat) { return new PartSpec { type = t, pos = pos, scale = scale, mat = mat }; }

        static GameObject WeaponModel(Transform holder, string name, PartSpec[] parts)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            foreach (var p in parts)
            {
                var part = Prim(p.type, "Part", go.transform, Vector3.zero, p.scale, p.mat, false);
                part.transform.localPosition = p.pos;
                if (p.type == PrimitiveType.Cylinder) part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return go;
        }

        static NetworkObject BuildMatchPrefab()
        {
            var go = new GameObject("Match");
            go.AddComponent<NetworkObject>();
            go.AddComponent<Match>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/Match.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<NetworkObject>();
        }

        static void RefreshDefaultPrefabs()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorApplication.ExecuteMenuItem("Tools/Fish-Networking/Utility/Refresh Default Prefabs"))
                Debug.LogWarning("[MakeupSniper] Не нашлось меню FishNet для обновления списка префабов");
            AssetDatabase.SaveAssets();
        }

        // ================= сеть =================

        static void NameHitboxLayer()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager == null || tagManager.Length == 0) return;
            var so = new SerializedObject(tagManager[0]);
            var layers = so.FindProperty("layers");
            if (layers == null || layers.arraySize <= World.HitboxLayer) return;
            layers.GetArrayElementAtIndex(World.HitboxLayer).stringValue = "Hitbox";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildNetwork(NetworkObject playerPrefab, NetworkObject matchPrefab)
        {
            var go = new GameObject("Network");
            var nm = go.AddComponent<NetworkManager>();
            var tm = go.AddComponent<TransportManager>();
            var tugboat = go.AddComponent<Tugboat>();
            var steam = go.AddComponent<FishySteamworks.FishySteamworks>();
            var multipass = go.AddComponent<Multipass>();

            var dpo = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");
            var nmSo = new SerializedObject(nm);
            SetProp(nmSo, "_spawnablePrefabs", dpo);
            SetProp(nmSo, "_dontDestroyOnLoad", false);
            SetProp(nmSo, "_runInBackground", true);
            nmSo.ApplyModifiedPropertiesWithoutUndo();

            var tmSo = new SerializedObject(tm);
            SetProp(tmSo, "Transport", multipass);
            tmSo.ApplyModifiedPropertiesWithoutUndo();

            var mpSo = new SerializedObject(multipass);
            var list = mpSo.FindProperty("_transports");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(NetSession.TugboatIndex).objectReferenceValue = tugboat;
            list.GetArrayElementAtIndex(NetSession.SteamIndex).objectReferenceValue = steam;
            SetProp(mpSo, "GlobalServerActions", false);
            mpSo.ApplyModifiedPropertiesWithoutUndo();

            var stSo = new SerializedObject(steam);
            SetProp(stSo, "_peerToPeer", true);
            SetProp(stSo, "_maximumClients", 8);
            stSo.ApplyModifiedPropertiesWithoutUndo();

            var tbSo = new SerializedObject(tugboat);
            SetProp(tbSo, "_maximumClients", NetSession.MaxPlayers);
            tbSo.ApplyModifiedPropertiesWithoutUndo();

            var steamService = go.AddComponent<SteamService>();
            var session = go.AddComponent<NetSession>();
            session.network = nm; session.multipass = multipass; session.tugboat = tugboat; session.steamTransport = steam; session.steam = steamService;
            var spawner = go.AddComponent<NetSpawner>();
            spawner.network = nm; spawner.playerPrefab = playerPrefab; spawner.matchPrefab = matchPrefab;
        }

        static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Debug.LogWarning("[MakeupSniper] Нет поля " + name + " у " + so.targetObject.GetType().Name); return; }
            if (value is bool) p.boolValue = (bool)value;
            else if (value is int) p.intValue = (int)value;
            else if (value is float) p.floatValue = (float)value;
            else if (value is Object || value == null) p.objectReferenceValue = (Object)value;
        }

        // ================= помощники =================

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
            mats[name] = m;
            return m;
        }

        static Material ShaderMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new System.Exception("Не найден шейдер " + shaderName);
            string path = Root + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        static RenderTexture MirrorTexture()
        {
            string path = Root + "/Materials/MirrorView.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt == null)
            {
                rt = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
                rt.name = "MirrorView";
                AssetDatabase.CreateAsset(rt, path);
            }
            return rt;
        }

        static Mesh MeshAsset(string name, Mesh fresh)
        {
            string path = Root + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
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

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mats[mat];
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject NoShadow(GameObject go)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static Transform Pose(Transform parent, string name, Vector3 pos, Vector3 euler)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            return t;
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

    /// <summary>При открытии проекта сам открывает игровую сцену, если открыта пустая.</summary>
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
