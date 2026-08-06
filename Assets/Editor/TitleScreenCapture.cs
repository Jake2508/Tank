using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders L_MainMenu to a PNG so the layout can be checked against the design mock
/// without opening the editor. Purely a dev aid - it restores every setting it
/// touches and never saves the scene.
///
/// Driven by environment variables so it can be scripted from a batch-mode run:
///   TANKEO_CAPTURE_PATH    output png (default ./title_screen_capture.png)
///   TANKEO_CAPTURE_W / _H  render size (default 960x540)
///   TANKEO_CAPTURE_SELECT  which slab to pose as selected (default PlayButton,
///                          "none" for the resting state)
/// </summary>
public static class TitleScreenCapture
{
    const string ScenePath = "Assets/Scenes/L_MainMenu.unity";

    [MenuItem("Tools/Tankeo/3 - Capture Title Screen")]
    public static void Capture()
    {
        string outPath = Env("TANKEO_CAPTURE_PATH",
                             Path.Combine(Directory.GetCurrentDirectory(), "title_screen_capture.png"));
        int width = EnvInt("TANKEO_CAPTURE_W", 960);
        int height = EnvInt("TANKEO_CAPTURE_H", 540);
        string select = Env("TANKEO_CAPTURE_SELECT", "PlayButton");

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = Find(scene, "Canvas");
        if (canvasGo == null) { Debug.LogError("[Capture] No Canvas."); return; }
        var canvas = canvasGo.GetComponent<Canvas>();

        var cam = PickCamera();
        if (cam == null) { Debug.LogError("[Capture] No enabled camera."); return; }

        // --- remember what we are about to trample -----------------------------
        var prevMode = canvas.renderMode;
        var prevCam = canvas.worldCamera;
        var prevPlane = canvas.planeDistance;
        var prevTarget = cam.targetTexture;

        // Capture puts the canvas in Screen Space - Camera, which would run the UI
        // through post-processing. In the shipping Overlay mode it never is, so turn
        // post off for the render or the colours read wrong.
        var camData = cam.GetUniversalAdditionalCameraData();
        bool prevPost = camData != null && camData.renderPostProcessing;
        if (camData != null) camData.renderPostProcessing = false;

        var levelSelect = canvasGo.transform.Find("LevelSelectUI");
        bool levelSelectWasOn = levelSelect != null && levelSelect.gameObject.activeSelf;
        if (levelSelect != null) levelSelect.gameObject.SetActive(false);

        var pose = PoseAsSelected(canvasGo.transform, select);

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            cam.targetTexture = rt;

            Canvas.ForceUpdateCanvases();
            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[Capture] Wrote " + outPath + " (" + width + "x" + height + ", select=" + select + ")");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            if (camData != null) camData.renderPostProcessing = prevPost;
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            canvas.planeDistance = prevPlane;
            if (levelSelect != null) levelSelect.gameObject.SetActive(levelSelectWasOn);
            RestorePose(pose);
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    struct Pose
    {
        public RectTransform slab, shadow;
        public Vector2 slabHome, shadowHome;
        public GameObject chevron;
        public Image body;
        public Color bodyHome;
    }

    /// <summary>
    /// MenuSlab only runs in play mode, so reproduce its selected state by hand -
    /// reading the offsets and hover tint straight off the serialized component.
    /// </summary>
    static Pose PoseAsSelected(Transform canvas, string slabName)
    {
        var pose = new Pose();
        if (string.IsNullOrEmpty(slabName) || slabName == "none") return pose;

        var slab = canvas.Find("MainMenuUI/" + slabName) as RectTransform;
        if (slab == null) { Debug.LogWarning("[Capture] No slab named " + slabName); return pose; }

        var menuSlab = slab.GetComponent<MenuSlab>();
        if (menuSlab == null) return pose;
        var so = new SerializedObject(menuSlab);

        pose.slab = slab;
        pose.slabHome = slab.anchoredPosition;
        slab.anchoredPosition = pose.slabHome + so.FindProperty("hoverOffset").vector2Value;

        pose.shadow = so.FindProperty("shadow").objectReferenceValue as RectTransform;
        if (pose.shadow != null)
        {
            pose.shadowHome = pose.shadow.anchoredPosition;
            pose.shadow.anchoredPosition = so.FindProperty("shadowHover").vector2Value;
        }

        pose.body = so.FindProperty("body").objectReferenceValue as Image;
        if (pose.body != null)
        {
            pose.bodyHome = pose.body.color;
            pose.body.color = so.FindProperty("hoverTint").colorValue;
        }

        pose.chevron = so.FindProperty("chevron").objectReferenceValue is RectTransform c ? c.gameObject : null;
        if (pose.chevron != null) pose.chevron.SetActive(true);

        return pose;
    }

    static void RestorePose(Pose pose)
    {
        if (pose.slab != null) pose.slab.anchoredPosition = pose.slabHome;
        if (pose.shadow != null) pose.shadow.anchoredPosition = pose.shadowHome;
        if (pose.body != null) pose.body.color = pose.bodyHome;
        if (pose.chevron != null) pose.chevron.SetActive(false);
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
