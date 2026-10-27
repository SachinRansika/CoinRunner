#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

// Builds the whole Coin Runner level (tilemap, player, coins, spikes, goal, UI)
// the first time the project is opened. Re-run any time from:
//     Coin Runner > Build Level (rebuild everything)
[InitializeOnLoad]
public static class CoinRunnerBuilder
{
    const string ScenePath = "Assets/Scenes/CoinRunner.unity";

    static CoinRunnerBuilder()
    {
        EditorApplication.update += AutoRun;
    }

    static void AutoRun()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;

        EditorApplication.update -= AutoRun;

        if (File.Exists(ScenePath))
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
            return;
        }
        Build();
    }

    [MenuItem("Coin Runner/Build Level (rebuild everything)")]
    public static void Build()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("Coin Runner",
                "TextMeshPro needs its essential resources first.\n\n" +
                "1) Menu: Window > TextMeshPro > Import TMP Essential Resources\n" +
                "2) Then: Coin Runner > Build Level (rebuild everything)", "OK");
            return;
        }

        EnsureInputHandling();

        foreach (string f in new[] { "Assets/Scenes", "Assets/Prefabs", "Assets/Tiles", "Assets/Sprites", "Assets/Palettes" })
            Directory.CreateDirectory(f);

        // ---- tags + layer ----
        foreach (string tag in new[] { "Coin", "Hazard", "Goal" })
            if (!InternalEditorUtility.tags.Contains(tag)) InternalEditorUtility.AddTag(tag);
        int groundLayer = EnsureLayer("Ground");

        // ---- sprites ----
        Sprite square   = MakeSprite("Square",   32, 32, (x, y) => 1f);
        Sprite circle   = MakeSprite("Circle",   32, 32, (x, y) => Mathf.Clamp01(16f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) + 0.5f));
        Sprite triangle = MakeSprite("Triangle", 32, 32, (x, y) =>
        {
            float half = 16f * (1f - (y + 0.5f) / 32f);
            return Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - 16f) + 0.5f);
        });
        Sprite capsule  = MakeSprite("Capsule",  32, 64, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f, d;
            if (py < 16f)      d = Vector2.Distance(new Vector2(px, py), new Vector2(16f, 16f));
            else if (py > 48f) d = Vector2.Distance(new Vector2(px, py), new Vector2(16f, 48f));
            else               d = Mathf.Abs(px - 16f);
            return Mathf.Clamp01(16f - d + 0.5f);
        });

        // ---- tile + physics material ----
        AssetDatabase.DeleteAsset("Assets/Tiles/GroundTile.asset");
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = square;
        tile.colliderType = Tile.ColliderType.Grid;
        AssetDatabase.CreateAsset(tile, "Assets/Tiles/GroundTile.asset");

        AssetDatabase.DeleteAsset("Assets/Tiles/NoFriction.physicsMaterial2D");
        PhysicsMaterial2D noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(noFriction, "Assets/Tiles/NoFriction.physicsMaterial2D");

        // ---- new scene ----
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Grid > Ground (Tilemap) ----
        GameObject grid = new GameObject("Grid");
        grid.AddComponent<Grid>();
        GameObject groundGO = new GameObject("Ground");
        groundGO.transform.SetParent(grid.transform);
        Tilemap tm = groundGO.AddComponent<Tilemap>();
        groundGO.AddComponent<TilemapRenderer>();
        tm.color = new Color(0.36f, 0.55f, 0.25f);

        Fill(tm, tile, -10, 6, -5, -4);   // start ground
        Fill(tm, tile, 9, 22, -5, -4);    // second ground (gap = 7..8)
        Fill(tm, tile, 2, 5, -1, -1);     // low platform
        Fill(tm, tile, 12, 15, 1, 1);     // high platform
        Fill(tm, tile, -10, -10, -3, 2);  // left wall

        groundGO.AddComponent<TilemapCollider2D>();
        groundGO.layer = groundLayer;

        // ---- Player ----
        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(-6f, -2f, 0f);
        SpriteRenderer psr = player.AddComponent<SpriteRenderer>();
        psr.sprite = capsule;
        psr.color = new Color(0.2f, 0.8f, 0.3f);
        psr.sortingOrder = 3;

        CapsuleCollider2D pcol = player.AddComponent<CapsuleCollider2D>();
        pcol.direction = CapsuleDirection2D.Vertical;
        pcol.size = new Vector2(1f, 2f);
        pcol.sharedMaterial = noFriction;

        Rigidbody2D prb = player.AddComponent<Rigidbody2D>();
        prb.bodyType = RigidbodyType2D.Dynamic;
        prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        prb.interpolation = RigidbodyInterpolation2D.Interpolate;
        prb.constraints = RigidbodyConstraints2D.FreezeRotation;

        GameObject feet = new GameObject("GroundCheck");
        feet.transform.SetParent(player.transform, false);
        feet.transform.localPosition = new Vector3(0f, -1f, 0f);

        PlayerMovement pm = player.AddComponent<PlayerMovement>();
        pm.groundCheck = feet.transform;
        pm.groundLayer = 1 << groundLayer;
        player.AddComponent<PlayerInteractions>();

        // ---- Main Camera ----
        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(-6f, -1f, -10f);
        Camera cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.53f, 0.81f, 0.92f);
        camGO.AddComponent<AudioListener>();
        CameraFollow cf = camGO.AddComponent<CameraFollow>();
        cf.target = player.transform;

        // ---- Prefabs: Coin, Spike ----
        GameObject coinPrefab = SavePrefab("Coin", () =>
        {
            GameObject g = new GameObject("Coin");
            g.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = circle; sr.color = new Color(1f, 0.85f, 0.1f); sr.sortingOrder = 1;
            CircleCollider2D c = g.AddComponent<CircleCollider2D>();
            c.radius = 0.5f; c.isTrigger = true;
            g.tag = "Coin";
            return g;
        });
        GameObject spikePrefab = SavePrefab("Spike", () =>
        {
            GameObject g = new GameObject("Spike");
            SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = triangle; sr.color = new Color(0.9f, 0.1f, 0.1f); sr.sortingOrder = 1;
            PolygonCollider2D pc = g.AddComponent<PolygonCollider2D>();
            pc.SetPath(0, new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0f, 0.5f) });
            pc.isTrigger = false;
            g.tag = "Hazard";
            return g;
        });

        Vector2[] coinPositions =
        {
            new Vector2(3, 0), new Vector2(4, 0),        // above low platform
            new Vector2(7.5f, -2),                       // above the gap
            new Vector2(13, 2), new Vector2(14, 2),      // above high platform
            new Vector2(18, -3)                          // near the end
        };
        foreach (Vector2 p in coinPositions) Place(coinPrefab, p);

        foreach (float x in new[] { 11f, 16f, 19f }) Place(spikePrefab, new Vector2(x, -2.5f));

        // ---- Goal ----
        GameObject goal = new GameObject("Goal");
        goal.transform.position = new Vector3(21f, -2f, 0f);
        goal.transform.localScale = new Vector3(0.3f, 2f, 1f);
        SpriteRenderer gsr = goal.AddComponent<SpriteRenderer>();
        gsr.sprite = square; gsr.color = Color.white; gsr.sortingOrder = 1;
        BoxCollider2D gcol = goal.AddComponent<BoxCollider2D>();
        gcol.size = Vector2.one; gcol.isTrigger = true;
        goal.tag = "Goal";

        // ---- UI ----
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        TextMeshProUGUI coinText = MakeText("CoinText", canvasGO.transform, "Coins: 0", 50, new Color(0.1f, 0.1f, 0.1f), TextAlignmentOptions.Left);
        RectTransform crt = coinText.rectTransform;
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0f, 1f);
        crt.anchoredPosition = new Vector2(30f, -20f);
        crt.sizeDelta = new Vector2(500f, 80f);

        GameObject panel = new GameObject("WinPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 200f / 255f);

        TextMeshProUGUI title = MakeText("WinText", panel.transform, "LEVEL COMPLETE!", 110, Color.white, TextAlignmentOptions.Center);
        title.rectTransform.anchoredPosition = new Vector2(0f, 120f);
        title.rectTransform.sizeDelta = new Vector2(1400f, 200f);

        GameObject btnGO = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(panel.transform, false);
        RectTransform brt = btnGO.GetComponent<RectTransform>();
        brt.anchoredPosition = new Vector2(0f, -100f);
        brt.sizeDelta = new Vector2(400f, 100f);
        Image bimg = btnGO.GetComponent<Image>();
        bimg.color = new Color(0.95f, 0.95f, 0.95f);
        Button button = btnGO.GetComponent<Button>();
        button.targetGraphic = bimg;

        TextMeshProUGUI btnText = MakeText("Text (TMP)", btnGO.transform, "Play Again", 48, new Color(0.1f, 0.1f, 0.1f), TextAlignmentOptions.Center);
        RectTransform btrt = btnText.rectTransform;
        btrt.anchorMin = Vector2.zero; btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero; btrt.offsetMax = Vector2.zero;

        // ---- GameManager ----
        GameObject gmGO = new GameObject("GameManager");
        GameManager gm = gmGO.AddComponent<GameManager>();
        gm.coinText = coinText;
        gm.winPanel = panel;
        UnityEventTools.AddPersistentListener(button.onClick, gm.RestartGame);
        panel.SetActive(false);

        // ---- save ----
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Coin Runner level built. Press Play!  Arrows/A-D = run, Space = jump.");
    }

    // ------------------------------------------------------------------ helpers

    static void Fill(Tilemap tm, TileBase tile, int x0, int x1, int y0, int y1)
    {
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                tm.SetTile(new Vector3Int(x, y, 0), tile);
    }

    static void Place(GameObject prefab, Vector2 pos)
    {
        GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        g.transform.position = new Vector3(pos.x, pos.y, 0f);
    }

    static GameObject SavePrefab(string name, Func<GameObject> make)
    {
        GameObject temp = make();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, "Assets/Prefabs/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(temp);
        return prefab;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        return t;
    }

    static Sprite MakeSprite(string name, int w, int h, Func<int, int, float> alpha)
    {
        string path = "Assets/Sprites/" + name + ".png";
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color(1f, 1f, 1f, alpha(x, y));
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 32f;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.filterMode = FilterMode.Bilinear;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing != -1) return existing;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        Debug.LogError("No free user layer for '" + layerName + "'. Create it manually.");
        return 0;
    }

    // The lab uses the classic Input class. If the project is set to
    // "Input System Package (New)" only, switch it to "Both".
    static void EnsureInputHandling()
    {
        try
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            SerializedObject ps = new SerializedObject(assets[0]);
            SerializedProperty p = ps.FindProperty("activeInputHandler");
            if (p != null && p.intValue == 1)
            {
                p.intValue = 2; // Both
                ps.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Coin Runner",
                    "Active Input Handling was switched to 'Both'.\nPlease restart Unity when asked.", "OK");
            }
        }
        catch (Exception e) { Debug.LogWarning("Could not check input handling: " + e.Message); }
    }
}
#endif
