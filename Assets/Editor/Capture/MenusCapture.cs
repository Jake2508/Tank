using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders one of the menus over its own map to a PNG so the layout can be checked
/// against the design without opening the editor. Purely a dev aid - it restores every
/// setting it touches and never saves the scene.
///
/// Driven by environment variables so it can be scripted from a batch-mode run:
///   TANKEO_MENU_PATH   output png (default ./menu_capture.png)
///   TANKEO_MENU_W / _H render size (default 960x540)
///   TANKEO_MENU_SCENE  scene to open (default Assets/Scenes/L_Woodlands.unity)
///   TANKEO_MENU_STATE  upgrade | upgrade-mixed | upgrade-maxed | pause | pause-empty |
///                      gameover | gameover-empty | win
/// </summary>
public static class MenusCapture
{
    const string DefaultScene = "Assets/Scenes/L_Woodlands.unity";

    static readonly string[] AllStates =
    {
        "upgrade", "upgrade-mixed", "upgrade-maxed",
        "pause", "pause-empty",
        "gameover", "gameover-empty", "win",
    };

    /// <summary>
    /// Every state in one editor launch. Opening Unity costs far more than the renders
    /// do, so checking a layout change against all eight readings is one run, not eight.
    /// Writes to TANKEO_MENU_DIR (default ./menu_captures).
    /// </summary>
    [MenuItem("Tools/Tankeo/13 - Capture All Menu States")]
    public static void CaptureAll()
    {
        string dir = Env("TANKEO_MENU_DIR",
                         Path.Combine(Directory.GetCurrentDirectory(), "menu_captures"));
        Directory.CreateDirectory(dir);

        foreach (var state in AllStates)
            Capture(Path.Combine(dir, "menu_" + state + ".png"),
                    EnvInt("TANKEO_MENU_W", 960), EnvInt("TANKEO_MENU_H", 540),
                    Env("TANKEO_MENU_SCENE", DefaultScene), state);

        Debug.Log("[Menus Capture] Wrote " + AllStates.Length + " states to " + dir);
    }

    [MenuItem("Tools/Tankeo/12 - Capture Menus")]
    public static void Capture()
    {
        string outPath = Env("TANKEO_MENU_PATH",
                             Path.Combine(Directory.GetCurrentDirectory(), "menu_capture.png"));
        int width = EnvInt("TANKEO_MENU_W", 960);
        int height = EnvInt("TANKEO_MENU_H", 540);
        string scenePath = Env("TANKEO_MENU_SCENE", DefaultScene);
        string state = Env("TANKEO_MENU_STATE", "upgrade");

        Capture(outPath, width, height, scenePath, state);
    }

    static void Capture(string outPath, int width, int height, string scenePath, string state)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var menuGo = Find(scene, "MenuCanvas");
        if (menuGo == null) { Debug.LogError("[Menus Capture] No MenuCanvas - run the builder first."); return; }
        var canvas = menuGo.GetComponent<Canvas>();

        var cam = PickCamera();
        if (cam == null) { Debug.LogError("[Menus Capture] No enabled camera."); return; }

        // --- remember what we are about to trample -----------------------------
        var prevMode = canvas.renderMode;
        var prevCam = canvas.worldCamera;
        var prevPlane = canvas.planeDistance;
        var prevPixelPerfect = canvas.pixelPerfect;
        var prevTarget = cam.targetTexture;

        // Capture puts the canvas in Screen Space - Camera, which would run the UI
        // through post-processing. In the shipping Overlay mode it never is, so turn
        // post off for the render or the colours read wrong.
        var camData = cam.GetUniversalAdditionalCameraData();
        bool prevPost = camData != null && camData.renderPostProcessing;
        if (camData != null) camData.renderPostProcessing = false;

        var others = new List<GameObject>();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go == menuGo || !go.activeSelf || go.GetComponent<Canvas>() == null) continue;
            others.Add(go);
            go.SetActive(false);
        }

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            // Screen Space - Camera snaps against the game view's size, not the render
            // texture's, which shifts every rect by a fraction of a pixel here.
            canvas.pixelPerfect = false;
            cam.targetTexture = rt;

            Canvas.ForceUpdateCanvases();
            Pose(menuGo, state);

            // Nothing ticks in batch mode, so settle the content-sized rects by hand
            // now that the canvas knows the render size.
            foreach (var layout in menuGo.GetComponentsInChildren<RectTransform>(true))
                LayoutRebuilder.MarkLayoutForRebuild(layout);
            Canvas.ForceUpdateCanvases();

            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[Menus Capture] Wrote " + outPath + " (" + width + "x" + height +
                      ", " + Path.GetFileName(scenePath) + ", state=" + state + ")");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            if (camData != null) camData.renderPostProcessing = prevPost;
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            canvas.planeDistance = prevPlane;
            canvas.pixelPerfect = prevPixelPerfect;

            foreach (var go in others) go.SetActive(true);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    /// <summary>
    /// The readings worth eyeballing, including the two from the QA list that most
    /// often break a layout: an all-zero run and a fully maxed one.
    /// </summary>
    static void Pose(GameObject menuGo, string state)
    {
        var upgrade = menuGo.GetComponentInChildren<UpgradeScreen>(true);
        var pause = menuGo.GetComponentInChildren<PauseScreen>(true);
        var end = menuGo.GetComponentInChildren<EndScreen>(true);

        if (upgrade != null) upgrade.gameObject.SetActive(false);
        if (pause != null) pause.gameObject.SetActive(false);
        if (end != null) end.gameObject.SetActive(false);

        switch (state)
        {
            case "upgrade":                 // fresh run: everything at tier 0
                if (upgrade != null) upgrade.EditorPose(new[] { 0, 0, 0, 0 }, 1);
                break;

            case "upgrade-mixed":           // mid-run, and a card sitting at tier 2
                if (upgrade != null) upgrade.EditorPose(new[] { 2, 1, 0, 3 }, 1);
                break;

            case "upgrade-maxed":           // the focused card is the maxed one
                if (upgrade != null) upgrade.EditorPose(new[] { 3, 3, 2, 3 }, 0);
                break;

            case "pause":
                if (pause != null) pause.EditorPose(Stats(134f, 23, 4, new[] { 2, 1, 0, 0 }), 0);
                break;

            case "pause-empty":             // nothing has happened yet
                if (pause != null) pause.EditorPose(Empty(), 0);
                break;

            case "gameover":
                if (end != null) end.EditorPose(Stats(252f, 63, 7, new[] { 3, 2, 1, 2 }), false);
                break;

            case "gameover-empty":          // 00:00, no kills, level 1, no upgrades
                if (end != null) end.EditorPose(Empty(), false);
                break;

            case "win":
                if (end != null) end.EditorPose(Stats(600f, 148, 12, new[] { 3, 3, 3, 3 }), true);
                break;

            default:
                Debug.LogError("[Menus Capture] Unknown state: " + state);
                break;
        }
    }

    static RunStats Stats(float time, int kills, int level, int[] tiers)
    {
        return new RunStats
        {
            time = time, kills = kills, level = level, tiers = tiers,
            bronze = 41, silver = 12, gold = 3,
        };
    }

    /// <summary>A run where nothing at all has happened - every column at its zero.</summary>
    static RunStats Empty()
    {
        return new RunStats { time = 0f, kills = 0, level = 1, tiers = new[] { 0, 0, 0, 0 } };
    }

    static Camera PickCamera()
    {
        Camera best = null;
        foreach (var cam in Object.FindObjectsOfType<Camera>())
        {
            if (!cam.enabled || !cam.gameObject.activeInHierarchy) continue;
            if (best == null || cam.depth > best.depth) best = cam;
        }
        return best;
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static string Env(string key, string fallback)
    {
        var v = System.Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    static int EnvInt(string key, int fallback)
    {
        return int.TryParse(System.Environment.GetEnvironmentVariable(key), out int v) && v > 0 ? v : fallback;
    }
}
