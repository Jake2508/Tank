using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders the speed effects to a PNG so the look can be checked without entering play
/// mode. Purely a dev aid - it restores every setting it touches and never saves.
///
/// Two things a naive one-shot render cannot show, and how this gets around them:
///
/// Motion blur is built from the change between the previous frame's view matrix and
/// this one's, so a single Render() produces none at all. The camera is nudged between
/// two renders here and the second is the one that is read back.
///
/// A trail renderer only has geometry once its transform has actually moved over time,
/// which never happens outside play mode. The glow trails are fed a short path by hand
/// with AddPosition so there is something to look at.
///
/// Driven by environment variables so it can be scripted from a batch-mode run:
///   TANKEO_SPEED_PATH   output png (default ./speed_capture.png)
///   TANKEO_SPEED_W / _H render size (default 960x540)
///   TANKEO_SPEED_SCENE  scene to open (default Assets/Scenes/L_Woodlands.unity)
///   TANKEO_SPEED_DRIVE  speed fraction to pose: 0 parked, 1 top speed, 1.6 boosting
/// </summary>
public static class SpeedFeelCapture
{
    const string DefaultScene = "Assets/Scenes/L_Woodlands.unity";

    [MenuItem("Tools/Tankeo/23 - Capture Speed Feel")]
    public static void Capture()
    {
        string outPath = Env("TANKEO_SPEED_PATH",
                             Path.Combine(Directory.GetCurrentDirectory(), "speed_capture.png"));
        int width = EnvInt("TANKEO_SPEED_W", 960);
        int height = EnvInt("TANKEO_SPEED_H", 540);
        string scenePath = Env("TANKEO_SPEED_SCENE", DefaultScene);
        float drive = EnvFloat("TANKEO_SPEED_DRIVE", 1f);

        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var feel = Object.FindObjectOfType<SpeedFeel>();
        if (feel == null) { Debug.LogError("[Speed Capture] No SpeedFeel - run the builder first."); return; }

        var cam = PickCamera();
        if (cam == null) { Debug.LogError("[Speed Capture] No enabled camera."); return; }

        var prevTarget = cam.targetTexture;
        var prevPos = cam.transform.position;
        var prevFov = cam.fieldOfView;

        // Post-processing is the whole point here, so unlike the HUD capture it stays on.
        var camData = cam.GetUniversalAdditionalCameraData();
        bool prevPost = camData != null && camData.renderPostProcessing;
        if (camData != null) camData.renderPostProcessing = true;

        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 1 };
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            feel.EditorPose(drive);
            var trails = FeedTrails(drive);

            // SpeedFeel drives the virtual camera's lens, which only reaches the real
            // Camera when CinemachineBrain runs - and it does not outside play mode.
            // Copying it across is what makes the capture frame what the player sees.
            var vcam = Object.FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();
            if (vcam != null) cam.fieldOfView = vcam.m_Lens.FieldOfView;

            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();

            // First render seeds the previous-frame matrices; the nudge between the two
            // is what the blur is actually built from. Scaled by drive so a parked frame
            // has no camera motion to blur.
            //
            // The size of the nudge has to be one frame of real travel or the capture
            // lies: at top speed the tank covers tankSpeed/60 units per frame, and the
            // camera lags even that because of its own damping. Anything larger reports
            // a blur the game will never actually produce.
            const float TopSpeed = 15f;
            const float Fps = 60f;
            cam.Render();
            cam.transform.position = prevPos + cam.transform.right * (TopSpeed / Fps * drive);
            cam.Render();

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;

            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Debug.Log("[Speed Capture] Wrote " + outPath + " (" + width + "x" + height +
                      ", " + Path.GetFileName(scenePath) + ", drive=" + drive +
                      ", trails fed=" + trails + ")");
            Report(feel, cam);
        }
        finally
        {
            cam.transform.position = prevPos;
            // Restored explicitly: this is written from the virtual camera's lens above,
            // and leaving it wide would bake a captured pose into the scene the moment
            // anything saved it.
            cam.fieldOfView = prevFov;
            cam.targetTexture = prevTarget;
            if (camData != null) camData.renderPostProcessing = prevPost;

            feel.EditorPose(0f);

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    /// <summary>
    /// Logs what the volume stack actually resolved to, which is the only way to tell a
    /// setting that is landing from one that is merely authored. Pixel inspection cannot
    /// separate them: aberration only shows on high-contrast edges, and motion blur
    /// needs a real frame loop rather than two Render calls.
    /// </summary>
    static void Report(SpeedFeel feel, Camera cam)
    {
        var volume = feel.GetComponent<UnityEngine.Rendering.Volume>();

        // The volume layer mask, not the culling mask: reading the stack through the
        // wrong mask reports values the camera never actually renders with.
        var data = cam.GetUniversalAdditionalCameraData();
        int mask = data != null ? data.volumeLayerMask : ~0;
        UnityEngine.Rendering.VolumeManager.instance.Update(cam.transform, mask);
        var stack = UnityEngine.Rendering.VolumeManager.instance.stack;

        var blur = stack.GetComponent<MotionBlur>();
        var aberration = stack.GetComponent<ChromaticAberration>();
        var vignette = stack.GetComponent<Vignette>();

        var vcam = Object.FindObjectOfType<Cinemachine.CinemachineVirtualCamera>();

        Debug.Log(string.Format(
            "[Speed Capture] weight={0:0.00} vcamFov={1:0.0} | blur active={2} i={3:0.000} | " +
            "aberration i={4:0.000} | vignette i={5:0.000}",
            volume != null ? volume.weight : -1f,
            vcam != null ? vcam.m_Lens.FieldOfView : -1f,
            blur != null && blur.active, blur != null ? blur.intensity.value : -1f,
            aberration != null ? aberration.intensity.value : -1f,
            vignette != null ? vignette.intensity.value : -1f));
    }

    /// <summary>
    /// Lays a short arc of trail behind each glow renderer so the capture has something
    /// to show. Returns how many were fed, which is also the check that the builder
    /// actually created and wired them.
    /// </summary>
    static int FeedTrails(float drive)
    {
        int fed = 0;

        foreach (var trail in Object.FindObjectsOfType<TrailRenderer>(true))
        {
            if (!trail.name.EndsWith("-Glow")) continue;
            if (!trail.emitting) continue;                  // SpeedFeel decided it is too slow

            trail.Clear();
            var origin = trail.transform.position;
            var forward = trail.transform.forward;
            var right = trail.transform.right;

            // A gentle curve rather than a straight line, so the width taper reads.
            for (int i = 12; i >= 0; i--)
            {
                float t = i / 12f;
                trail.AddPosition(origin - forward * (t * 9f) + right * (t * t * 1.6f));
            }

            fed++;
        }

        return fed;
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

    static string Env(string key, string fallback)
    {
        var v = System.Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(v) ? fallback : v;
    }

    static int EnvInt(string key, int fallback)
    {
        return int.TryParse(System.Environment.GetEnvironmentVariable(key), out int v) && v > 0 ? v : fallback;
    }

    static float EnvFloat(string key, float fallback)
    {
        return float.TryParse(System.Environment.GetEnvironmentVariable(key), out float v) ? v : fallback;
    }
}
