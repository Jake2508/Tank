using System.Collections.Generic;
using System.IO;
using Tank;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the gameplay pass to the prefabs, the audio asset and the three play scenes:
/// reticle grounding, shell ballistics, tank feel, destructible trees and the audio
/// systems.
///
/// Edits everything in place - the existing prefabs and scene objects keep their
/// fileIDs, and nothing here creates a duplicate of anything. Idempotent, so it can be
/// re-run after any tuning change.
/// </summary>
public static class GameplayBuilder
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/L_Woodlands.unity",
        "Assets/Scenes/L_Desert.unity",
        "Assets/Scenes/L_Snowy.unity",
    };

    /// <summary>Index into AudioClipRefSO.weather, per scene above.</summary>
    static readonly int[] WeatherIndex = { 0, 1, 2 };

    const string Audio = "Assets/Audio/";
    const string TreeDir = "Assets/Prefabs/Trees/";
    const string TerrainDir = "Assets/Prefabs/Terrain";

    const string PlayerPrefab = "Assets/Prefabs/Model-Setup/Player.prefab";
    const string ReticlePrefab = "Assets/Prefabs/TankRecticle.prefab";
    const string SoundPrefab = "Assets/Prefabs/SoundManager.prefab";
    const string AudioAsset = "Assets/Scripts/ScriptableObjects/AudioClipsRefSO.asset";

    static readonly string[] ShellPrefabs =
    {
        "Assets/Prefabs/TankShell.prefab",
        "Assets/Prefabs/TankShellBig.prefab",
    };

    /// <summary>The layer the aim raycast filters to. Named "Level" in the tag manager.</summary>
    const int GroundLayer = 9;

    /// <summary>Trees and other world clutter. Never the ground.</summary>
    const int PropsLayer = 6;


    [MenuItem("Tools/Tankeo/15 - Build Gameplay")]
    public static void Build()
    {
        ProceduralSfx.Generate();

        WireAudioAsset();
        BuildTreePrefabs();
        BuildTerrainLayers();
        BuildPlayerPrefab();
        BuildShellPrefabs();
        BuildReticlePrefab();
        BuildSoundManagerPrefab();

        foreach (var path in Scenes) BuildScene(path);

        AssetDatabase.SaveAssets();
        Debug.Log("[Gameplay] Build complete.");
    }

    // =========================================================================
    // Audio asset
    // =========================================================================

    static void WireAudioAsset()
    {
        var asset = AssetDatabase.LoadAssetAtPath<AudioClipRefSO>(AudioAsset);
        if (asset == null) { Debug.LogError("[Gameplay] No AudioClipRefSO at " + AudioAsset); return; }

        var so = new SerializedObject(asset);

        SetClip(so, "buttonClicked", Audio + "ButtonClick.wav");
        SetClip(so, "Explosion", Audio + "Explosion Sound Effect.wav");
        SetClip(so, "bulletShot", Audio + "Bullet Shot.wav");
        SetClip(so, "coinPickup", Audio + "CoinPickup.wav");
        SetClip(so, "LevelUp", Audio + "Reward Sound.wav");

        // The menu keeps the chiptune bed it already had; runs get the fuller track.
        SetClip(so, "menuMusic", Audio + "8bit background.wav");
        SetClip(so, "gameplayMusic", Audio + "GameMusic.wav");
        SetClip(so, "engineLoop", Audio + "TankTracks.wav");

        SetClip(so, "treeImpact", ProceduralSfx.Dir + "sfx_tree_impact.wav");
        SetClip(so, "reloadReady", ProceduralSfx.Dir + "sfx_reload_ready.wav");
        SetClip(so, "uiMove", ProceduralSfx.Dir + "sfx_ui_move.wav");
        SetClip(so, "lowHull", ProceduralSfx.Dir + "sfx_low_hull.wav");

        // Order matches WeatherIndex above: Woodlands, Desert, Snowy.
        var weather = so.FindProperty("weather");
        string[] beds = { Audio + "Rainfall.wav", Audio + "SandStorm.wav", Audio + "SnowStorm.wav" };
        weather.arraySize = beds.Length;
        for (int i = 0; i < beds.Length; i++)
            weather.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(beds[i]);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        Debug.Log("[Gameplay] Audio clips wired.");
    }

    static void SetClip(SerializedObject so, string property, string path)
    {
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Gameplay] No audio field " + property); return; }

        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) { Debug.LogWarning("[Gameplay] Missing clip " + path); return; }

        prop.objectReferenceValue = clip;
    }

    // =========================================================================
    // Trees
    // =========================================================================

    /// <summary>
    /// Makes the trees rammable and shootable.
    ///
    /// Their only collider was a capsule on the *leaves*, with nothing at all on the
    /// trunk - so the tank was driving through the part of the tree it looked like it
    /// should hit. Each tree gets a trunk collider sized from the trunk mesh itself.
    /// </summary>
    static void BuildTreePrefabs()
    {
        foreach (var path in Directory.GetFiles(TreeDir, "*.prefab"))
        {
            string assetPath = path.Replace('\\', '/');
            var root = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                var tree = EnsureComponent<DestructibleTree>(root);
                Bind(tree, "destroyEffect",
                     AssetDatabase.LoadAssetAtPath<GameObject>(EffectsBuilder.Dir + "fx_tree_splinter.prefab"));

                // Props, not Default: keeps trees off the ground layer the aim raycast
                // filters to, so the reticle can never latch onto one.
                root.layer = PropsLayer;
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = PropsLayer;

                AddTrunkCollider(root);

                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                Debug.Log("[Gameplay] Tree ready: " + Path.GetFileName(assetPath));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        SoftenTreeMaterial();
    }

    /// <summary>
    /// Gives the tree a single collider, on the root, sized to its trunk.
    ///
    /// Two reasons it has to be the root rather than a child. Trees carry no Rigidbody,
    /// so Unity delivers OnCollisionEnter to the GameObject holding the collider - a
    /// capsule on the leaves child would notify the leaves, and DestructibleTree on the
    /// root would never hear the ram. And the collider that was there covered the
    /// canopy, not the trunk, so the tank was passing through the part of the tree it
    /// looked like it should hit.
    /// </summary>
    static void AddTrunkCollider(GameObject root)
    {
        // Already rebuilt on a previous run.
        if (root.GetComponent<CapsuleCollider>() != null) return;

        Bounds? trunk = null;

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            if (filter.name.IndexOf("Trunk", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            // Into the tree root's own space, so the collider is right whatever local
            // offsets the model carries.
            var local = filter.sharedMesh.bounds;
            var centre = root.transform.InverseTransformPoint(filter.transform.TransformPoint(local.center));
            var size = Vector3.Scale(local.size, filter.transform.lossyScale);

            var b = new Bounds(centre, size);
            if (trunk.HasValue) { var t = trunk.Value; t.Encapsulate(b); trunk = t; }
            else trunk = b;
        }

        if (!trunk.HasValue) return;

        // Drop the old canopy capsule now that the trunk is covered, so the tree has one
        // collider and every contact routes to the root. Its physic material is carried
        // over rather than lost.
        PhysicMaterial material = null;
        foreach (var child in root.GetComponentsInChildren<Collider>(true))
        {
            if (child.gameObject == root) continue;
            if (material == null) material = child.sharedMaterial;
            Object.DestroyImmediate(child);
        }

        var bounds = trunk.Value;
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.sharedMaterial = material;
        capsule.direction = 1;                                     // upright
        capsule.height = Mathf.Max(0.5f, bounds.size.y);
        // Slightly inside the visual trunk: a collider wider than the art makes the tank
        // stop against thin air.
        capsule.radius = Mathf.Max(0.15f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
        capsule.center = bounds.center;
    }

    /// <summary>
    /// The tree physic material had bounciness 0.8, which pinged a 500kg tank off a
    /// trunk. Trees are destroyed on contact now, so all it did was throw the player.
    /// </summary>
    static void SoftenTreeMaterial()
    {
        const string path = "Assets/Materials/Tree/P_Tree.physicMaterial";
        var material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if (material == null) return;

        if (Mathf.Approximately(material.bounciness, 0f) &&
            Mathf.Approximately(material.dynamicFriction, 0.6f)) return;

        material.bounciness = 0f;
        material.dynamicFriction = 0.6f;
        material.staticFriction = 0.6f;
        EditorUtility.SetDirty(material);
    }

    /// <summary>
    /// Puts the terrain's own mesh onto the Level layer.
    ///
    /// The aim raycast has always been masked to Level, but nothing in the project was
    /// ever on it - the floors are all layer Default. That mask was being discarded by
    /// a bad Raycast overload, so it never showed; with the overload fixed the mask has
    /// to actually match the ground. Only the node carrying the MeshCollider moves, not
    /// its children, or every prop on the tile would become an aim target.
    /// </summary>
    static void BuildTerrainLayers()
    {
        if (!Directory.Exists(TerrainDir)) return;

        int moved = 0;

        foreach (var path in Directory.GetFiles(TerrainDir, "*.prefab", SearchOption.AllDirectories))
        {
            string assetPath = path.Replace('\\', '/');
            var root = PrefabUtility.LoadPrefabContents(assetPath);

            try
            {
                var collider = root.GetComponent<MeshCollider>();
                if (collider == null || root.layer == GroundLayer) continue;

                root.layer = GroundLayer;
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                moved++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log("[Gameplay] Ground tiles moved to the Level layer: " + moved);
    }

    // =========================================================================
    // Player
    // =========================================================================

    static void BuildPlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);

        try
        {
            ApplyPlayerSettings(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            Debug.Log("[Gameplay] Player prefab updated.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Everything the player tank needs, applied to a root that may be the prefab or a
    /// scene copy of it.
    ///
    /// It has to work on both, because the Player is only a prefab instance in
    /// Woodlands - in Desert and Snowy it was unpacked at some point, so those two
    /// scenes inherit nothing from the prefab. Editing only the asset silently left two
    /// thirds of the game without interpolation, continuous collision, lean or engine
    /// audio, which is exactly the kind of divergence that reads as "one level is
    /// broken".
    /// </summary>
    static void ApplyPlayerSettings(GameObject root)
    {
        var body = root.GetComponent<Rigidbody>();
        if (body != null)
        {
            // Movement is driven from FixedUpdate; without interpolation the hull
            // visibly steps between physics ticks at any framerate above 50.
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // A fast, heavy tank on Discrete passes through thin colliders - which is
            // part of why it went through tree trunks rather than hitting them.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        var tank = root.GetComponent<TankController>();

        // The visual model, not the Rigidbody transform: lean must never touch the
        // physics rotation or the terrain alignment fights it every frame.
        Transform visual = FindVisualBody(root.transform);
        if (visual != null && tank != null)
        {
            var lean = EnsureComponent<TankBodyLean>(visual.gameObject);
            Bind(lean, "body", visual);
            Bind(lean, "tank", tank);
        }

        var engine = EnsureComponent<EngineAudio>(root);
        var source = EnsureComponent<AudioSource>(root);
        source.playOnAwake = false;
        source.loop = true;
        Bind(engine, "tank", tank);
        Bind(engine, "clips", AssetDatabase.LoadAssetAtPath<AudioClipRefSO>(AudioAsset));

        BuildGunFx(root, tank);
        BuildDrift(root, tank);
        BuildTurnFx(root, tank);

        // The old read-only meter is superseded by DriftBoost, which charges and spends.
        // By name: the class is gone, and this only has to catch older scenes.
        foreach (var behaviour in root.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null || behaviour.GetType().Name != "DriftMeter") continue;
            Object.DestroyImmediate(behaviour);
        }

        // The reticle is a separate scene object the tank points at, and in the unpacked
        // scenes it is a plain copy too.
        if (tank != null && tank.reticleTransform != null)
        {
            var reticle = EnsureComponent<TankReticle>(tank.reticleTransform.gameObject);
            var so = new SerializedObject(reticle);
            var mask = so.FindProperty("groundMask");
            if (mask != null) mask.intValue = 1 << GroundLayer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Muzzle flash, smoke and the barrel's recoil slide.</summary>
    static void BuildGunFx(GameObject root, Tank.TankController tank)
    {
        var fx = EnsureComponent<Tank.TankGunFx>(root);
        Bind(fx, "tank", tank);
        Bind(fx, "muzzleFlash",
             AssetDatabase.LoadAssetAtPath<GameObject>(EffectsBuilder.Dir + "fx_muzzle_flash.prefab"));
        Bind(fx, "muzzleSmoke",
             AssetDatabase.LoadAssetAtPath<GameObject>(EffectsBuilder.Dir + "fx_muzzle_smoke.prefab"));

        // The barrel hangs off the turret in the Tank model.
        if (tank != null && tank.turretTransform != null)
        {
            var barrel = tank.turretTransform.Find("Barrel");
            if (barrel != null) Bind(fx, "barrel", barrel);
        }
    }

    /// <summary>
    /// The drift meter and its boost, plus the two looping effects it switches on and
    /// off. Both are instantiated as children so they follow the tank; unlike the
    /// one-shot bursts they are meant to be dragged along by it.
    /// </summary>
    static void BuildDrift(GameObject root, Tank.TankController tank)
    {
        var drift = EnsureComponent<Tank.DriftBoost>(root);
        Bind(drift, "tank", tank);
        Bind(drift, "body", root.GetComponent<Rigidbody>());

        Bind(drift, "driftSparks", AttachEffect(root, "DriftSparks", "fx_drift_sparks",
                                                new Vector3(0f, 0.2f, -1.1f), new Vector3(60f, 0f, 0f)));
        Bind(drift, "boostAura", AttachEffect(root, "BoostAura", "fx_boost_aura",
                                              new Vector3(0f, 0.4f, -1.4f), new Vector3(75f, 0f, 0f)));

        if (tank != null && tank.trails != null)
            BindArray(drift, "driftTrails", tank.trails);
    }

    /// <summary>
    /// Sparks off each track, scaled by how hard that side is scrubbing.
    ///
    /// Separate from the drift sparks on purpose: those are a binary "this corner is
    /// charging the meter", so every turn below the drift threshold had no feedback at
    /// all. These are continuous, so ordinary cornering produces something too.
    ///
    /// The Animator hook is deliberately left unbound - it is a place for a track-bend
    /// clip to be dropped in later, and TankTurnFx checks the parameters exist before
    /// setting them so an empty slot costs nothing.
    /// </summary>
    static void BuildTurnFx(GameObject root, Tank.TankController tank)
    {
        var fx = EnsureComponent<Tank.TankTurnFx>(root);
        Bind(fx, "tank", tank);

        // Index 0 is the left track; TankTurnFx relies on that order to decide which
        // side is the outer one through a turn.
        var left = AttachEffect(root, "TurnSparks_L", "fx_drift_sparks",
                                new Vector3(-0.85f, 0.15f, -0.35f), new Vector3(70f, 0f, 0f));
        var right = AttachEffect(root, "TurnSparks_R", "fx_drift_sparks",
                                 new Vector3(0.85f, 0.15f, -0.35f), new Vector3(70f, 0f, 0f));

        BindArray(fx, "trackSparks", new[] { left, right });
    }

    /// <summary>
    /// Instantiates a looping effect under the tank once, reusing it if it is already
    /// there so re-running the builder does not stack copies.
    /// </summary>
    static ParticleSystem AttachEffect(GameObject root, string name, string prefab,
                                       Vector3 localPosition, Vector3 localEuler)
    {
        var existing = root.transform.Find(name);
        if (existing == null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(EffectsBuilder.Dir + prefab + ".prefab");
            if (asset == null) return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.name = name;
            instance.transform.SetParent(root.transform, false);
            existing = instance.transform;
        }

        existing.localPosition = localPosition;
        existing.localEulerAngles = localEuler;

        var particles = existing.GetComponentInChildren<ParticleSystem>(true);
        if (particles != null)
        {
            // Local space so the wake stays pinned to the tank rather than being left
            // behind - the opposite of what the one-shot bursts want.
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.enabled = false;
        }

        return particles;
    }

    /// <summary>
    /// The nested Tank model. Matched by name rather than index so re-ordering the
    /// prefab's children cannot silently point the lean at a particle system.
    /// </summary>
    static Transform FindVisualBody(Transform root)
    {
        foreach (Transform child in root)
            if (child.name == "Tank") return child;

        // Fall back to whatever holds the turret, which is inside the model either way.
        var tank = root.GetComponent<TankController>();
        if (tank != null && tank.turretTransform != null && tank.turretTransform.parent != null)
            return tank.turretTransform.parent;

        return null;
    }

    static void BuildShellPrefabs()
    {
        foreach (var path in ShellPrefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    // A shell is the fastest thing in the game; on Discrete it steps
                    // straight through trees and thin geometry between physics ticks.
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    // Baked off in the asset, not flipped at runtime. The shell falls at
                    // its own multiplied rate applied in TankShell.FixedUpdate; leaving
                    // this on in the prefab is what let a shell fall at world gravity
                    // against a trajectory solved for twelve times that.
                    body.useGravity = false;
                }

                var shell = root.GetComponent<TankShell>();
                if (shell != null)
                {
                    var trail = root.GetComponentInChildren<TrailRenderer>(true);
                    if (trail != null)
                    {
                        Bind(shell, "trail", trail);
                        TuneTrail(trail);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Gameplay] Shell updated: " + Path.GetFileName(path));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    /// <summary>
    /// A short streak behind the shell, so a fast round still reads as travelling rather
    /// than teleporting.
    ///
    /// Deliberately brief: at ~50 units per second a half-second trail would draw a line
    /// across most of the screen and turn every shot into a laser. A tenth of a second
    /// leaves just enough to follow the arc with your eye.
    /// </summary>
    static void TuneTrail(TrailRenderer trail)
    {
        // Longer and wider than the first pass, and yellow rather than cream. The shell
        // has to stay legible while it crosses a dark enemy, and a thin cream streak
        // disappeared against everything except the terrain.
        trail.time = 0.18f;
        trail.minVertexDistance = 0.1f;
        trail.autodestruct = false;
        trail.emitting = true;

        // Tapers to nothing at the tail rather than ending on a hard edge.
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(1f, 0f));

        var cream = new Color32(244, 238, 220, 255);
        var yellow = new Color32(233, 201, 63, 255);

        // Hot cream at the round, cooling to yellow down the tail.
        trail.colorGradient = new Gradient
        {
            colorKeys = new[] { new GradientColorKey(cream, 0f), new GradientColorKey(yellow, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) },
        };
    }

    static void BuildReticlePrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(ReticlePrefab);
        if (root == null) return;

        try
        {
            var reticle = EnsureComponent<TankReticle>(root);

            var so = new SerializedObject(reticle);
            var mask = so.FindProperty("groundMask");
            if (mask != null) mask.intValue = 1 << GroundLayer;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ReticlePrefab);
            Debug.Log("[Gameplay] Reticle updated.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================================
    // Sound manager
    // =========================================================================

    static void BuildSoundManagerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(SoundPrefab);
        if (root == null) return;

        try
        {
            // Two components on this prefab pointed at scripts that no longer exist.
            int removed = 0;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            if (removed > 0) Debug.Log("[Gameplay] Removed " + removed + " missing scripts from SoundManager.");

            var clips = AssetDatabase.LoadAssetAtPath<AudioClipRefSO>(AudioAsset);

            var director = EnsureComponent<MusicDirector>(root);
            Bind(director, "clips", clips);

            // The old BackgroundMusic source played the chiptune bed once on awake with
            // looping off, so the game fell silent a couple of minutes in and never
            // recovered. MusicDirector owns music now, so this one is stood down.
            var background = root.transform.Find("BackgroundMusic");
            if (background != null)
            {
                var source = background.GetComponent<AudioSource>();
                if (source != null)
                {
                    source.playOnAwake = false;
                    source.Stop();
                }
                background.gameObject.SetActive(false);
            }

            // levelUp was never assigned, so the level-up sting has never played.
            var sound = root.GetComponent<SoundManager>();
            if (sound != null)
            {
                var so = new SerializedObject(sound);
                var levelUp = so.FindProperty("levelUp");

                if (levelUp != null && levelUp.objectReferenceValue == null)
                {
                    var go = root.transform.Find("LevelUpSound");
                    if (go == null)
                    {
                        var created = new GameObject("LevelUpSound");
                        created.transform.SetParent(root.transform, false);
                        go = created.transform;
                    }

                    var source = EnsureComponent<AudioSource>(go.gameObject);
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    source.ignoreListenerPause = true;
                    if (clips != null) source.clip = clips.LevelUp;

                    levelUp.objectReferenceValue = source;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, SoundPrefab);
            Debug.Log("[Gameplay] SoundManager updated.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // =========================================================================
    // Scenes
    // =========================================================================

    static void BuildScene(string scenePath)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        int index = System.Array.IndexOf(Scenes, scenePath);
        int weather = index >= 0 && index < WeatherIndex.Length ? WeatherIndex[index] : 0;

        var go = FindRoot(scene, "WeatherAmbience");
        if (go == null)
        {
            go = new GameObject("WeatherAmbience");
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        }

        var source = EnsureComponent<AudioSource>(go);
        source.playOnAwake = false;
        source.loop = true;

        var ambience = EnsureComponent<WeatherAmbience>(go);
        Bind(ambience, "clips", AssetDatabase.LoadAssetAtPath<AudioClipRefSO>(AudioAsset));

        var so = new SerializedObject(ambience);
        so.FindProperty("weatherIndex").intValue = weather;
        so.ApplyModifiedPropertiesWithoutUndo();

        BuildGameFeel(scene);

        // Only Woodlands keeps the Player as a prefab instance; the other two were
        // unpacked, so the prefab pass never reaches them and they have to be fixed
        // where they sit.
        int players = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tank in root.GetComponentsInChildren<Tank.TankController>(true))
            {
                ApplyPlayerSettings(tank.gameObject);
                players++;
            }
        }

        // Scene-placed terrain tiles predate the prefab layer fix.
        int moved = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.gameObject.layer == GroundLayer) continue;
                if (collider.GetComponentInParent<DestructibleTree>() != null) continue;
                if (!collider.name.StartsWith("Floor") && !collider.name.StartsWith("Des-Floor")) continue;

                collider.gameObject.layer = GroundLayer;
                moved++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Gameplay] Scene ready: " + Path.GetFileName(scenePath) +
                  " (weather " + weather + ", " + moved + " tiles re-layered, " +
                  players + " player(s) configured)");
    }

    /// <summary>
    /// The hitstop and camera-shake host, plus the listener that lets the shake reach
    /// the camera.
    ///
    /// Cinemachine drives the camera transform every frame, so nudging that transform
    /// directly would simply be overwritten. Impulse is the supported route: a source
    /// raises a signal at a world position and a listener on the virtual camera applies
    /// it, which also gives distance falloff for free - a kill across the map shakes
    /// less than one under the tracks.
    /// </summary>
    static void BuildGameFeel(Scene scene)
    {
        var go = FindRoot(scene, "GameFeel");
        if (go == null)
        {
            go = new GameObject("GameFeel");
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        }

        var feel = EnsureComponent<GameFeel>(go);
        var source = EnsureComponent<Cinemachine.CinemachineImpulseSource>(go);

        var definition = source.m_ImpulseDefinition;
        definition.m_ImpulseShape = Cinemachine.CinemachineImpulseDefinition.ImpulseShapes.Bump;
        definition.m_ImpulseDuration = 0.22f;
        definition.m_ImpulseType = Cinemachine.CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
        // Roughly a screen and a half at this camera height: far enough that off-screen
        // kills still register faintly, close enough that they do not all feel the same.
        definition.m_DissipationDistance = 45f;
        definition.m_DissipationRate = 0.25f;
        source.m_ImpulseDefinition = definition;

        Bind(feel, "impulse", source);

        int listeners = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var vcam in root.GetComponentsInChildren<Cinemachine.CinemachineVirtualCamera>(true))
            {
                // AddExtension is idempotent in Cinemachine, but check anyway so a
                // rebuild does not stack duplicates.
                var existing = vcam.GetComponent<Cinemachine.CinemachineImpulseListener>();
                if (existing == null)
                {
                    existing = vcam.gameObject.AddComponent<Cinemachine.CinemachineImpulseListener>();
                    vcam.AddExtension(existing);
                }

                existing.m_ChannelMask = 1;
                existing.m_Gain = 1f;
                // The shake moves the camera, not the world it is looking at.
                existing.m_Use2DDistance = false;
                listeners++;
            }
        }

        Debug.Log("[Gameplay] GameFeel ready in " + scene.name + " (" + listeners + " camera listener(s))");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

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

    static void Bind(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Gameplay] No property " + property + " on " + target); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindArray<T>(Object target, string property, T[] values) where T : Object
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        if (list == null) { Debug.LogError("[Gameplay] No property " + property + " on " + target); return; }

        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
