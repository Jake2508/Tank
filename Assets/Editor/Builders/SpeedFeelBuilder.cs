using Cinemachine;
using Tank;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires the speed-reactive effects: a post-processing Volume whose weight follows the
/// tank, the camera's field of view, and a pair of emissive trails off the tracks.
/// Idempotent - run it as often as you like.
///
/// The blur this replaces was already in every profile at full intensity and never
/// changed, so the game read as soft rather than as fast and paid for the pass even
/// while parked. Those overrides are switched off here and re-homed on a Volume that
/// SpeedFeel drives from zero.
///
/// Intensities are the restrained set: enough that top speed is unmistakable, not so
/// much that an enemy closing from off-centre disappears into the vignette. This is a
/// top-down dodging game and the corners of the screen carry real information.
/// </summary>
public static class SpeedFeelBuilder
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/L_Woodlands.unity",
        "Assets/Scenes/L_Desert.unity",
        "Assets/Scenes/L_Snowy.unity",
    };

    /// <summary>Every global profile the play scenes use, including the unused OLD one.</summary>
    static readonly string[] GlobalProfiles =
    {
        "Assets/Scenes/SampleScene/Global Volume Profile Woods.asset",
        "Assets/Scenes/SampleScene/Global Volume Profile Desert.asset",
        "Assets/Scenes/SampleScene/Global Volume Profile Snow.asset",
        "Assets/Scenes/SampleScene/Global Volume Profile OLD.asset",
    };

    const string SpeedProfile = "Assets/Scenes/SampleScene/Speed Volume Profile.asset";
    const string GlowMaterial = "Assets/Materials/FX_TrackGlow.mat";
    const string PlayerPrefab = "Assets/Prefabs/Model-Setup/Player.prefab";

    const string RigName = "SpeedFx";
    const string GlowSuffix = "-Glow";

    // ---- the full-speed look -------------------------------------------------
    // Motion blur is the one that has to stay modest: URP's is camera-motion only, so
    // it smears the tank along with the world, and at 960x540 it goes chunky quickly.
    const float BlurIntensity = 0.5f;
    const float BlurClamp = 0.04f;

    // Both of these were dialled back after measuring the capture. At 0.45 the
    // aberration visibly doubled the tree edges, and a vignette of 0.38 took the
    // corners to 46% of centre luminance - in a game where the thing about to kill you
    // arrives from off-centre, that is trading away the information the player needs.
    const float Aberration = 0.34f;
    const float VignetteIntensity = 0.29f;
    const float VignetteSmoothness = 0.5f;

    // ---- the scuff trails ----------------------------------------------------
    // Five seconds of marks at fifteen units a second is seventy-five units of trail
    // behind the tank at all times, which buries the map it is driving over.
    const float ScuffTime = 1.3f;
    const float GlowTime = 0.5f;
    const float GlowWidth = 0.42f;

    [MenuItem("Tools/Tankeo/22 - Build Speed Feel")]
    public static void Build()
    {
        var profile = EnsureSpeedProfile();
        var material = EnsureGlowMaterial();

        foreach (var path in GlobalProfiles) RetireConstantBlur(path);

        BuildPlayerTrails(material);
        foreach (var path in Scenes) BuildScene(path, profile);

        AssetDatabase.SaveAssets();
        Debug.Log("[SpeedFeel] Build complete in " + Scenes.Length + " scenes.");
    }

    // =========================================================================
    // Assets
    // =========================================================================

    /// <summary>
    /// The Volume the weight is driven on. Only the three overrides that react to speed
    /// live here - everything else stays on the per-map global profile, so a map can be
    /// graded without touching how speed feels.
    /// </summary>
    static VolumeProfile EnsureSpeedProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SpeedProfile);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, SpeedProfile);
        }

        var blur = Ensure<MotionBlur>(profile);
        blur.active = true;
        Set(blur.mode, MotionBlurMode.CameraOnly);
        Set(blur.quality, MotionBlurQuality.Low);          // WebGL, and it is a full-screen pass
        Set(blur.intensity, BlurIntensity);
        Set(blur.clamp, BlurClamp);

        var aberration = Ensure<ChromaticAberration>(profile);
        aberration.active = true;
        Set(aberration.intensity, Aberration);

        var vignette = Ensure<Vignette>(profile);
        vignette.active = true;
        Set(vignette.intensity, VignetteIntensity);
        Set(vignette.smoothness, VignetteSmoothness);

        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>
    /// Unlit and transparent, so the trail is self-lit and can carry a colour above 1.
    /// A Lit material would be shaded by the scene lighting and could never exceed the
    /// bloom threshold no matter what colour it was given.
    /// </summary>
    static Material EnsureGlowMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterial);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[SpeedFeel] URP Unlit shader not found.");
                return null;
            }

            material = new Material(shader) { name = "FX_TrackGlow" };
            AssetDatabase.CreateAsset(material, GlowMaterial);
        }

        // Additive: the trails lie on the ground and should brighten it rather than
        // paint over it, and additive never darkens the terrain underneath.
        material.SetFloat("_Surface", 1f);                 // transparent
        material.SetFloat("_Blend", 1f);                   // additive
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetColor("_BaseColor", Color.white);

        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Switches off the always-on motion blur the profiles shipped with. Left on, it
    /// blends with the speed Volume and the screen never returns to sharp.
    /// </summary>
    static void RetireConstantBlur(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null) return;
        if (!profile.TryGet(out MotionBlur blur) || !blur.active) return;

        blur.active = false;
        EditorUtility.SetDirty(profile);
    }

    // =========================================================================
    // The tank
    // =========================================================================

    /// <summary>
    /// Adds the glow trails to the prefab, so the scene that still uses an instance of
    /// it picks them up without an override. The other two scenes hold unpacked copies
    /// and are handled per-scene.
    /// </summary>
    static void BuildPlayerTrails(Material glow)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            if (Fit(root, glow)) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Tames the scuff trails, gives each one an emissive twin, and fills in the two
    /// trail fields that were left empty - TankController.trails and
    /// DriftBoost.driftTrails were both wired to nothing, so every line of trail code
    /// in either of them was dead and the marks simply drew forever.
    /// </summary>
    static bool Fit(GameObject root, Material glow)
    {
        var tank = root.GetComponentInChildren<TankController>(true);
        if (tank == null) return false;

        var scuffs = new System.Collections.Generic.List<TrailRenderer>();

        foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true))
        {
            if (trail.name.EndsWith(GlowSuffix)) continue;

            trail.time = ScuffTime;
            scuffs.Add(trail);
            EnsureGlowTrail(trail, glow);
        }

        if (scuffs.Count == 0) return false;

        var so = new SerializedObject(tank);
        Fill(so.FindProperty("trails"), scuffs);
        so.ApplyModifiedPropertiesWithoutUndo();

        var boost = root.GetComponentInChildren<DriftBoost>(true);
        if (boost != null)
        {
            var bso = new SerializedObject(boost);
            Fill(bso.FindProperty("driftTrails"), scuffs);
            bso.ApplyModifiedPropertiesWithoutUndo();
        }

        return true;
    }

    static TrailRenderer EnsureGlowTrail(TrailRenderer scuff, Material glow)
    {
        var parent = scuff.transform;
        var existing = parent.Find(scuff.name + GlowSuffix);

        var go = existing != null ? existing.gameObject
                                  : new GameObject(scuff.name + GlowSuffix, typeof(TrailRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var trail = go.GetComponent<TrailRenderer>();
        trail.time = GlowTime;                              // shorter than the scuff: a hot streak, not a stripe
        trail.minVertexDistance = 0.08f;
        trail.widthMultiplier = GlowWidth;
        trail.autodestruct = false;
        trail.emitting = false;                             // SpeedFeel switches it on
        trail.sharedMaterial = glow;
        trail.alignment = scuff.alignment;                  // whatever lies flat for the scuff lies flat here
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        // White, tapering out. The colour itself is driven per frame through a property
        // block; this gradient only shapes the fade along the trail's length.
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        return trail;
    }

    // =========================================================================
    // Scenes
    // =========================================================================

    static void BuildScene(string scenePath, VolumeProfile profile)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // The two scenes holding unpacked copies of the player need the same treatment
        // the prefab just had; in the third this finds the instance and no-ops.
        foreach (var tank in Object.FindObjectsOfType<TankController>(true))
            Fit(tank.transform.root.gameObject, AssetDatabase.LoadAssetAtPath<Material>(GlowMaterial));

        EnsureRig(scene, profile);
        BindFeel();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SpeedFeel] Rebuilt " + scenePath);
    }

    static GameObject EnsureRig(Scene scene, VolumeProfile profile)
    {
        var go = FindRoot(scene, RigName);
        if (go == null)
        {
            go = new GameObject(RigName);
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        }

        var volume = EnsureComponent<Volume>(go);
        volume.isGlobal = true;
        volume.sharedProfile = profile;
        // Above the per-map global Volume, which sits at the default priority, so these
        // three overrides win while it keeps the grade.
        volume.priority = 10f;
        volume.weight = 0f;

        EnsureComponent<SpeedFeel>(go);
        return go;
    }

    /// <summary>
    /// Fills in SpeedFeel's references from whatever is in the open scene. Run after
    /// both the tank pass and the rig pass, so every object it needs already exists.
    /// </summary>
    static void BindFeel()
    {
        var feel = Object.FindObjectOfType<SpeedFeel>();
        if (feel == null) return;

        var so = new SerializedObject(feel);

        var tank = Object.FindObjectOfType<TankController>();
        if (tank != null) so.FindProperty("tank").objectReferenceValue = tank;

        var volume = feel.GetComponent<Volume>();
        if (volume != null) so.FindProperty("speedVolume").objectReferenceValue = volume;

        var vcam = Object.FindObjectOfType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            so.FindProperty("vcam").objectReferenceValue = vcam;
            // Whatever the camera is authored at is the resting lens, so retuning the
            // shot does not silently change how much the speed pull adds.
            so.FindProperty("baseFov").floatValue = vcam.m_Lens.FieldOfView;
            so.FindProperty("topFov").floatValue = vcam.m_Lens.FieldOfView + 6f;
        }

        var glows = new System.Collections.Generic.List<TrailRenderer>();
        foreach (var trail in Object.FindObjectsOfType<TrailRenderer>(true))
            if (trail.name.EndsWith(GlowSuffix)) glows.Add(trail);

        if (glows.Count > 0) Fill(so.FindProperty("glowTrails"), glows);
        else Debug.LogWarning("[SpeedFeel] No glow trails found in the open scene.");

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Adds an override to the profile *and* to the profile's asset file.
    ///
    /// VolumeProfile.Add creates the component and puts it in the profile's list, but
    /// the component is a sub-asset and nothing writes it into the file - so a profile
    /// built purely in script saves as an empty one, the stack resolves to whatever the
    /// global profile says, and driving the weight appears to do nothing at all.
    /// </summary>
    static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component)) return component;

        component = profile.Add<T>(true);
        component.hideFlags = HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    static void Set<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Fill(SerializedProperty list, System.Collections.Generic.List<TrailRenderer> items)
    {
        if (list == null) return;

        list.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
