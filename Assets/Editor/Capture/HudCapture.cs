using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the in-game HUD over its own map to a PNG so the layout can be checked
/// against the design mock without opening the editor. Purely a dev aid - it
/// restores every setting it touches and never saves the scene.
///
/// Driven by environment variables so it can be scripted from a batch-mode run:
///   TANKEO_HUD_PATH    output png (default ./hud_capture.png)
///   TANKEO_HUD_W / _H  render size (default 960x540)
///   TANKEO_HUD_SCENE   scene to open (default Assets/Scenes/L_Woodlands.unity)
///   TANKEO_HUD_STATE   base (default) | low | full | empty
/// </summary>
public static class HudCapture
{
    const string DefaultScene = "Assets/Scenes/L_Woodlands.unity";

    [MenuItem("Tools/Tankeo/9 - Capture Game HUD")]
    public static void Capture()
    {
        string outPath = Env("TANKEO_HUD_PATH",
                             Path.Combine(Directory.GetCurrentDirectory(), "hud_capture.png"));
        int width = EnvInt("TANKEO_HUD_W", 960);
        int height = EnvInt("TANKEO_HUD_H", 540);
        string scenePath = Env("TANKEO_HUD_SCENE", DefaultScene);
        string state = Env("TANKEO_HUD_STATE", "base");

        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var hudGo = Find(scene, "HudCanvas");
        if (hudGo == null) { Debug.LogError("[HUD Capture] No HudCanvas - run the builder first."); return; }
        var canvas = hudGo.GetComponent<Canvas>();

        var cam = PickCamera();
        if (cam == null) { Debug.LogError("[HUD Capture] No enabled camera."); return; }

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

        // The pause / upgrade / game-over screens share the other canvas and are left
        // active in two of the three scenes; this capture is about the HUD.
        var others = new System.Collections.Generic.List<GameObject>();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go == hudGo || !go.activeSelf || go.GetComponent<Canvas>() == null) continue;
            others.Add(go);
            go.SetActive(false);
        }

        var hud = hudGo.GetComponent<HudController>();
        var group = hudGo.GetComponent<CanvasGroup>();
        float prevAlpha = group != null ? group.alpha : 1f;
        if (group != null) group.alpha = 1f;

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
            Pose(hud, state);

            // Nothing ticks in batch mode, so settle the content-sized plates by hand
            // now that the canvas knows the render size.
            foreach (var layout in hudGo.GetComponentsInChildren<RectTransform>(true))
                LayoutRebuilder.MarkLayoutForRebuild(layout);
            Canvas.ForceUpdateCanvases();

            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[HUD Capture] Wrote " + outPath + " (" + width + "x" + height +
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

            if (group != null) group.alpha = prevAlpha;
            foreach (var go in others) go.SetActive(true);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    /// <summary>
    /// The two frames of the mock, plus the two hull extremes from the QA list.
    /// </summary>
    static void Pose(HudController hud, string state)
    {
        if (hud == null) return;

        switch (state)
        {
            case "low":     // low-hull alarm, at the top of its pulse
                hud.EditorPose(14, 100, 0.62f, 5, 3, 27, 41, new[] { 3, 2, 1, 0 }, true, 1f, 1f);
                break;
            case "full":    // run start: full hull, nothing unlocked, clock at zero
                hud.EditorPose(100, 100, 0f, 1, 0, 0, 0, new[] { 0, 0, 0, 0 }, false, 0f, 1f);
                break;
            case "empty":   // the frame before the game-over screen takes over
                hud.EditorPose(0, 100, 0.9f, 12, 59, 59, 999, new[] { 3, 3, 3, 3 }, false, 0f, 1f);
                break;
            default:        // healthy mid-run read
                hud.EditorPose(72, 100, 0.41f, 3, 1, 42, 17, new[] { 2, 1, 0, 0 }, false, 0.34f, 1f);
                break;
        }
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
