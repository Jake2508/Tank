using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the level-select screen to a PNG so the layout can be checked against
/// the design mock without opening the editor. Purely a dev aid - it restores every
/// setting it touches and never saves the scene.
///
/// Driven by environment variables so it can be scripted from a batch-mode run:
///   TANKEO_LS_PATH     output png (default ./level_select_capture.png)
///   TANKEO_LS_W / _H   render size (default 960x540)
///   TANKEO_LS_SELECT   card to pose as selected, by node name or index
///                      (default 0 = Woodlands, "none" for the resting state)
/// </summary>
public static class LevelSelectCapture
{
    const string ScenePath = "Assets/Scenes/L_MainMenu.unity";

    [MenuItem("Tools/Tankeo/5 - Capture Level Select")]
    public static void Capture()
    {
        string outPath = Env("TANKEO_LS_PATH",
                             Path.Combine(Directory.GetCurrentDirectory(), "level_select_capture.png"));
        int width = EnvInt("TANKEO_LS_W", 960);
        int height = EnvInt("TANKEO_LS_H", 540);
        string select = Env("TANKEO_LS_SELECT", "0");

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = Find(scene, "Canvas");
        if (canvasGo == null) { Debug.LogError("[LS Capture] No Canvas."); return; }
        var canvas = canvasGo.GetComponent<Canvas>();

        var cam = PickCamera();
        if (cam == null) { Debug.LogError("[LS Capture] No enabled camera."); return; }

        var levelSelect = canvasGo.transform.Find("LevelSelectUI");
        if (levelSelect == null) { Debug.LogError("[LS Capture] No LevelSelectUI."); return; }

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

        var menu = canvasGo.transform.Find("MainMenuUI");
        bool menuWasOn = menu != null && menu.gameObject.activeSelf;
        if (menu != null) menu.gameObject.SetActive(false);

        bool levelSelectWasOn = levelSelect.gameObject.activeSelf;
        levelSelect.gameObject.SetActive(true);

        var group = levelSelect.GetComponent<CanvasGroup>();
        float prevAlpha = group != null ? group.alpha : 1f;
        if (group != null) group.alpha = 1f;

        var poses = PoseCards(levelSelect, select);

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            cam.targetTexture = rt;

            Canvas.ForceUpdateCanvases();

            // ScaleToFitWidth normally runs on Update; in batch mode nothing ticks,
            // so settle it by hand now the canvas knows the render size.
            foreach (var fit in levelSelect.GetComponentsInChildren<ScaleToFitWidth>(true)) fit.Apply();
            foreach (var layout in levelSelect.GetComponentsInChildren<RectTransform>(true))
                LayoutRebuilder.MarkLayoutForRebuild(layout);
            Canvas.ForceUpdateCanvases();

            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[LS Capture] Wrote " + outPath + " (" + width + "x" + height + ", select=" + select + ")");
        }
        finally
        {
            cam.targetTexture = prevTarget;
            if (camData != null) camData.renderPostProcessing = prevPost;
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            canvas.planeDistance = prevPlane;

            RestoreCards(poses);
            if (group != null) group.alpha = prevAlpha;
            levelSelect.gameObject.SetActive(levelSelectWasOn);
            if (menu != null) menu.gameObject.SetActive(menuWasOn);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    struct CardPose
    {
        public RectTransform card;
        public Vector2 home;
        public Image thumbKeyline, plateBody, badgeKeyline;
        public Color thumbHome, plateHome, badgeHome, tierHome;
        public TextMeshProUGUI tierWord;
        public CanvasGroup group;
        public float alphaHome;
    }

    /// <summary>
    /// LevelCard only runs in play mode, so drive its editor pose directly and keep
    /// enough state to put everything back afterwards.
    /// </summary>
    static List<CardPose> PoseCards(Transform levelSelect, string select)
    {
        var poses = new List<CardPose>();
        var cards = levelSelect.GetComponentsInChildren<LevelCard>(true);

        int selectedIndex = -1;
        if (!string.IsNullOrEmpty(select) && select != "none")
        {
            if (!int.TryParse(select, out selectedIndex))
            {
                selectedIndex = -1;
                for (int i = 0; i < cards.Length; i++)
                    if (cards[i].name == select) { selectedIndex = i; break; }
                if (selectedIndex < 0) Debug.LogWarning("[LS Capture] No card named " + select);
            }
        }

        for (int i = 0; i < cards.Length; i++)
        {
            var so = new SerializedObject(cards[i]);
            var pose = new CardPose
            {
                card = (RectTransform)cards[i].transform,
                thumbKeyline = so.FindProperty("thumbKeyline").objectReferenceValue as Image,
                plateBody = so.FindProperty("plateBody").objectReferenceValue as Image,
                badgeKeyline = so.FindProperty("badgeKeyline").objectReferenceValue as Image,
                tierWord = so.FindProperty("tierWord").objectReferenceValue as TextMeshProUGUI,
                group = cards[i].GetComponent<CanvasGroup>(),
            };
            pose.home = pose.card.anchoredPosition;
            if (pose.thumbKeyline) pose.thumbHome = pose.thumbKeyline.color;
            if (pose.plateBody) pose.plateHome = pose.plateBody.color;
            if (pose.badgeKeyline) pose.badgeHome = pose.badgeKeyline.color;
            if (pose.tierWord) pose.tierHome = pose.tierWord.color;
            if (pose.group) pose.alphaHome = pose.group.alpha;
            poses.Add(pose);

            cards[i].EditorPose(i == selectedIndex);
        }

        return poses;
    }

    static void RestoreCards(List<CardPose> poses)
    {
        foreach (var pose in poses)
        {
            if (pose.card) pose.card.anchoredPosition = pose.home;
            if (pose.thumbKeyline) pose.thumbKeyline.color = pose.thumbHome;
            if (pose.plateBody) pose.plateBody.color = pose.plateHome;
            if (pose.badgeKeyline) pose.badgeKeyline.color = pose.badgeHome;
            if (pose.tierWord) pose.tierWord.color = pose.tierHome;
            if (pose.group) pose.group.alpha = pose.alphaHome;
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
