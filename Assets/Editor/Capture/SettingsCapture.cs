using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the settings screen to PNGs so the layout can be checked without opening the
/// editor.
///
/// Captures at three aspect ratios by default, because two of the QA items are about
/// exactly that: the band must not leave a gap at 21:9, and the controls plate must not
/// overlap the band or run off the screen at 4:3. Those are invisible at 16:9.
/// </summary>
public static class SettingsCapture
{
    const string ScenePath = "Assets/Scenes/L_MainMenu.unity";

    struct Shape
    {
        public string name;
        public int width, height;
    }

    static readonly Shape[] Shapes =
    {
        new Shape { name = "16x9", width = 960,  height = 540 },
        new Shape { name = "4x3",  width = 800,  height = 600 },
        new Shape { name = "21x9", width = 1260, height = 540 },
    };

    [MenuItem("Tools/Tankeo/21 - Capture Settings")]
    public static void Capture()
    {
        string dir = Env("TANKEO_SETTINGS_DIR",
                         Path.Combine(Directory.GetCurrentDirectory(), "settings_captures"));
        Directory.CreateDirectory(dir);

        foreach (var shape in Shapes)
            Render(dir, shape, 0);

        // The focus states worth eyeballing: SFX row, and BACK.
        Render(dir, Shapes[0], 1, "_sfx");
        Render(dir, Shapes[0], 2, "_back");

        Debug.Log("[Settings Capture] Wrote to " + dir);
    }

    static void Render(string dir, Shape shape, int focusedIndex, string suffix = "")
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = Find(scene, "Canvas");
        if (canvasGo == null) { Debug.LogError("[Settings Capture] No Canvas."); return; }

        var settings = canvasGo.GetComponentInChildren<SettingsMenu>(true);
        if (settings == null) { Debug.LogError("[Settings Capture] No SettingsMenu - build it first."); return; }

        var canvas = canvasGo.GetComponent<Canvas>();
        var cam = PickCamera();
        if (cam == null) { Debug.LogError("[Settings Capture] No enabled camera."); return; }

        var prevMode = canvas.renderMode;
        var prevCam = canvas.worldCamera;
        var prevPlane = canvas.planeDistance;
        var prevPixelPerfect = canvas.pixelPerfect;
        var prevTarget = cam.targetTexture;

        var camData = cam.GetUniversalAdditionalCameraData();
        bool prevPost = camData != null && camData.renderPostProcessing;
        if (camData != null) camData.renderPostProcessing = false;

        // The title menu and level select share this canvas and are left active in the
        // scene. In the real flow MainMenuUI fades out as settings opens, so leaving
        // them visible here would show ghosting the player never sees.
        var hidden = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in canvasGo.transform)
        {
            if (!child.gameObject.activeSelf) continue;
            if (child.GetComponent<SettingsMenu>() != null) continue;

            hidden.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }

        var rt = new RenderTexture(shape.width, shape.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var tex = new Texture2D(shape.width, shape.height, TextureFormat.RGB24, false);

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.pixelPerfect = false;
            cam.targetTexture = rt;

            Canvas.ForceUpdateCanvases();
            // The real defaults, not literals - a capture showing values the game never
            // starts with is worse than no capture.
            settings.EditorPose(SettingsMenu.MusicDefault, SettingsMenu.SfxDefault, focusedIndex);

            // The canvas has only just learned the render size, so let the scaler and
            // the plate's own fit settle before reading anything.
            foreach (var layout in canvasGo.GetComponentsInChildren<RectTransform>(true))
                LayoutRebuilder.MarkLayoutForRebuild(layout);
            Canvas.ForceUpdateCanvases();
            settings.EditorPose(SettingsMenu.MusicDefault, SettingsMenu.SfxDefault, focusedIndex);

            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, shape.width, shape.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            string path = Path.Combine(dir, "settings_" + shape.name + suffix + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[Settings Capture] " + Path.GetFileName(path) +
                      " (" + shape.width + "x" + shape.height + ", focus " + focusedIndex + ")");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            if (camData != null) camData.renderPostProcessing = prevPost;
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            canvas.planeDistance = prevPlane;
            canvas.pixelPerfect = prevPixelPerfect;

            foreach (var go in hidden) go.SetActive(true);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
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
}
