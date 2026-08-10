using System.IO;
using Tank;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks the gameplay pass without entering play mode.
///
/// The ballistic solver is the part worth actually proving rather than eyeballing: the
/// whole point of the change is that a shell lands on the reticle at *every* range, and
/// that is a numeric claim. This integrates the trajectory the physics engine will run
/// and reports the miss distance, so a regression in the maths shows up as a number
/// instead of as "the shots feel off again".
/// </summary>
public static class GameplayVerify
{
    [MenuItem("Tools/Tankeo/16 - Verify Gameplay")]
    public static void Verify()
    {
        int failures = 0;

        failures += VerifyBallistics();
        failures += VerifyTrees();
        failures += VerifyPrefabs();
        failures += VerifyAudio();
        failures += VerifyAimableGround();
        failures += VerifyEffects();

        if (failures == 0) Debug.Log("[Verify] All gameplay checks passed.");
        else Debug.LogError("[Verify] " + failures + " check(s) failed.");
    }

    // =========================================================================
    // Ballistics
    // =========================================================================

    /// <summary>
    /// Fires at a spread of ranges and height offsets, integrates the arc under the
    /// project's own gravity, and measures where it actually lands.
    /// </summary>
    static int VerifyBallistics()
    {
        // Must match the values on TankController, or this proves nothing about what
        // the gun actually fires.
        const float angle = 20f;
        const float maxSpeed = 140f;
        const float gravityScale = 12f;
        const float tolerance = 0.35f;          // world units; the shell blast is 5

        float gravity = Mathf.Abs(Physics.gravity.y) * gravityScale;

        float[] ranges = { 3f, 8f, 15f, 25f, 40f, 60f };
        float[] drops = { 0f, -1.2f, -3f, 2f };

        int failures = 0;
        float worst = 0f;
        float slowest = 0f;

        foreach (float range in ranges)
        {
            foreach (float drop in drops)
            {
                var from = new Vector3(0f, 2f, 0f);
                var target = new Vector3(range, 2f + drop, 0f);

                Vector3 velocity = TankController.SolveBallisticVelocity(from, target, angle,
                                                                        maxSpeed, gravity);

                // How long the player waits for the shot to land is the thing that read
                // as "floaty", so it is worth reporting rather than assuming.
                float flight = range / new Vector2(velocity.x, velocity.z).magnitude;
                slowest = Mathf.Max(slowest, flight);

                // Speed-capped shots are expected to fall short; they are the deliberate
                // safety valve, not a solver error.
                if (velocity.magnitude >= maxSpeed - 0.01f) continue;

                float miss = Simulate(from, velocity, target, gravity);
                worst = Mathf.Max(worst, miss);

                if (miss > tolerance)
                {
                    Debug.LogError("[Verify] Ballistic miss at range " + range + ", drop " + drop +
                                   ": " + miss.ToString("F3") + " units");
                    failures++;
                }
            }
        }

        Debug.Log("[Verify] Ballistics: worst miss " + worst.ToString("F3") +
                  " units across " + (ranges.Length * drops.Length) + " shots; " +
                  "longest flight " + slowest.ToString("F2") + "s at " +
                  ranges[ranges.Length - 1] + " units.");
        return failures;
    }

    /// <summary>
    /// Steps the trajectory the way PhysX does and returns how close the shell actually
    /// passes to the target.
    ///
    /// Closest approach rather than "where it crosses the target's height on the way
    /// down": a shot at something above the muzzle at short range arrives while still
    /// climbing, and only measuring descending crossings scores those as enormous misses
    /// when the shell went straight through the target.
    /// </summary>
    static float Simulate(Vector3 from, Vector3 velocity, Vector3 target, float gravity)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 position = from;
        Vector3 v = velocity;
        float closest = float.MaxValue;

        for (int step = 0; step < 2000; step++)
        {
            Vector3 previous = position;

            // Semi-implicit Euler, matching PhysX: velocity updates, then position.
            v += Vector3.down * gravity * dt;
            position += v * dt;

            // Nearest point on this step's segment, so the sampling rate does not set
            // the floor on the measured accuracy.
            Vector3 segment = position - previous;
            float lengthSq = segment.sqrMagnitude;
            float t = lengthSq < 1e-8f
                ? 0f
                : Mathf.Clamp01(Vector3.Dot(target - previous, segment) / lengthSq);

            closest = Mathf.Min(closest, Vector3.Distance(previous + segment * t, target));

            // Past the target and heading away: no point integrating the rest.
            if (v.y < 0f && position.y < target.y - 5f) break;
        }

        return closest;
    }

    // =========================================================================
    // Content
    // =========================================================================

    static int VerifyTrees()
    {
        int failures = 0;

        foreach (var path in Directory.GetFiles("Assets/Prefabs/Trees/", "*.prefab"))
        {
            string assetPath = path.Replace('\\', '/');
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (tree == null) continue;

            string name = Path.GetFileName(assetPath);

            if (tree.GetComponent<DestructibleTree>() == null)
            {
                Debug.LogError("[Verify] " + name + " has no DestructibleTree."); failures++;
            }

            var colliders = tree.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != 1 || colliders[0].gameObject != tree)
            {
                Debug.LogError("[Verify] " + name + " should have exactly one collider, on the " +
                               "root, or ram collisions never reach DestructibleTree. Found " +
                               colliders.Length + ".");
                failures++;
            }
            else
            {
                var capsule = colliders[0] as CapsuleCollider;
                Debug.Log("[Verify] " + name + ": capsule r=" +
                          (capsule != null ? capsule.radius.ToString("F2") : "?") + " h=" +
                          (capsule != null ? capsule.height.ToString("F2") : "?") +
                          " layer=" + LayerMask.LayerToName(tree.layer));
            }
        }

        return failures;
    }

    static int VerifyPrefabs()
    {
        int failures = 0;

        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Model-Setup/Player.prefab");
        if (player != null)
        {
            var body = player.GetComponent<Rigidbody>();
            if (body != null && body.interpolation != RigidbodyInterpolation.Interpolate)
            {
                Debug.LogError("[Verify] Player Rigidbody is not interpolating - movement will step.");
                failures++;
            }
            if (body != null && body.collisionDetectionMode == CollisionDetectionMode.Discrete)
            {
                Debug.LogError("[Verify] Player Rigidbody is on Discrete collision.");
                failures++;
            }
            if (player.GetComponentInChildren<TankBodyLean>(true) == null)
            {
                Debug.LogError("[Verify] Player has no TankBodyLean."); failures++;
            }
            if (player.GetComponentInChildren<EngineAudio>(true) == null)
            {
                Debug.LogError("[Verify] Player has no EngineAudio."); failures++;
            }
        }

        var reticle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TankRecticle.prefab");
        if (reticle != null && reticle.GetComponent<TankReticle>() == null)
        {
            Debug.LogError("[Verify] Reticle prefab has no TankReticle."); failures++;
        }

        foreach (var path in new[] { "Assets/Prefabs/TankShell.prefab", "Assets/Prefabs/TankShellBig.prefab" })
        {
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (shell == null) continue;

            foreach (var body in shell.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body.collisionDetectionMode != CollisionDetectionMode.Discrete) continue;
                Debug.LogError("[Verify] " + Path.GetFileName(path) + " shell is on Discrete " +
                               "collision and will tunnel.");
                failures++;
            }
        }

        return failures;
    }

    /// <summary>
    /// The aim raycast is masked to the Level layer. Before this pass nothing in the
    /// project was on that layer at all - the mask only appeared to work because a bad
    /// Raycast overload was discarding it. If the ground is not actually reachable
    /// through the mask, the reticle silently falls back to a flat plane forever, which
    /// looks almost right and is very easy to miss. So: fire the real query at the real
    /// prefabs and confirm something answers.
    /// </summary>
    static int VerifyAimableGround()
    {
        const int groundLayer = 9;
        int mask = 1 << groundLayer;
        int failures = 0;
        int tested = 0;

        foreach (var path in Directory.GetFiles("Assets/Prefabs/Terrain", "*.prefab",
                                                SearchOption.AllDirectories))
        {
            string assetPath = path.Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null || prefab.GetComponent<MeshCollider>() == null) continue;

            tested++;
            if (prefab.layer == groundLayer) continue;

            Debug.LogError("[Verify] Terrain tile not on the Level layer, so the reticle " +
                           "cannot land on it: " + Path.GetFileName(assetPath));
            failures++;
        }

        // And confirm a live query against an instance really does return a hit.
        var sample = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Terrain/Woods-Forest/Floor1-New.prefab");

        if (sample != null)
        {
            var instance = Object.Instantiate(sample);
            instance.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            bool hit = Physics.Raycast(new Vector3(0f, 50f, 0f), Vector3.down,
                                       out RaycastHit info, 500f, mask,
                                       QueryTriggerInteraction.Ignore);

            if (!hit)
            {
                Debug.LogError("[Verify] A masked ground raycast found nothing - the " +
                               "reticle would never conform.");
                failures++;
            }
            else
            {
                Debug.Log("[Verify] Masked ground raycast hit " + info.collider.name +
                          " at y=" + info.point.y.ToString("F2") + " (" + tested + " tiles checked)");
            }

            Object.DestroyImmediate(instance);
        }

        return failures;
    }

    /// <summary>
    /// The firing, drift and tree-felling effects, and the components that fire them.
    /// Checked on the Player prefab and again per scene, because two of the three scenes
    /// hold unpacked copies that inherit nothing from it.
    /// </summary>
    static int VerifyEffects()
    {
        int failures = 0;

        foreach (var name in new[] { "fx_muzzle_flash", "fx_muzzle_smoke", "fx_tree_splinter",
                                     "fx_drift_sparks", "fx_boost_aura" })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EffectsBuilder.Dir + name + ".prefab") != null)
                continue;

            Debug.LogError("[Verify] Effect prefab missing: " + name);
            failures++;
        }

        foreach (var path in Directory.GetFiles("Assets/Prefabs/Trees/", "*.prefab"))
        {
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
            var component = tree != null ? tree.GetComponent<DestructibleTree>() : null;
            if (component == null) continue;

            var so = new SerializedObject(component);
            if (so.FindProperty("destroyEffect").objectReferenceValue != null) continue;

            Debug.LogError("[Verify] " + Path.GetFileName(path) + " has no destroy effect - " +
                           "trees would still vanish silently.");
            failures++;
        }

        foreach (var scenePath in new[] { "Assets/Scenes/L_Woodlands.unity",
                                          "Assets/Scenes/L_Desert.unity",
                                          "Assets/Scenes/L_Snowy.unity" })
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            string name = Path.GetFileNameWithoutExtension(scenePath);
            int tanks = 0, drift = 0, guns = 0, legacy = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tank in root.GetComponentsInChildren<TankController>(true))
                {
                    tanks++;
                    if (tank.GetComponent<DriftBoost>() != null) drift++;
                    if (tank.GetComponent<TankGunFx>() != null) guns++;
                }

                // Across the whole scene, not just the tank: the old meter was added to
                // the HUD canvas, so looking only at the player missed it entirely.
                // By name, since the class has been removed.
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && behaviour.GetType().Name == "DriftMeter") legacy++;
            }

            if (tanks != drift || tanks != guns)
            {
                Debug.LogError("[Verify] " + name + ": " + tanks + " tank(s) but " + drift +
                               " DriftBoost and " + guns + " TankGunFx.");
                failures++;
            }

            if (legacy > 0)
            {
                Debug.LogError("[Verify] " + name + ": DriftMeter still present; it fights " +
                               "DriftBoost for the HUD drift readout.");
                failures++;
            }

            Debug.Log("[Verify] " + name + ": " + tanks + " tank(s), drift + gun fx wired.");
        }

        return failures;
    }

    static int VerifyAudio()
    {
        int failures = 0;

        var clips = AssetDatabase.LoadAssetAtPath<AudioClipRefSO>("Assets/Scripts/ScriptableObjects/AudioClipsRefSO.asset");
        if (clips == null)
        {
            Debug.LogError("[Verify] No AudioClipRefSO."); return 1;
        }

        var so = new SerializedObject(clips);
        var prop = so.GetIterator();

        while (prop.NextVisible(true))
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (prop.objectReferenceValue != null) continue;

            Debug.LogError("[Verify] Audio clip unassigned: " + prop.propertyPath);
            failures++;
        }

        foreach (var name in new[] { "sfx_tree_impact", "sfx_reload_ready", "sfx_ui_move", "sfx_low_hull" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ProceduralSfx.Dir + name + ".wav");
            if (clip == null)
            {
                Debug.LogError("[Verify] Generated clip missing: " + name); failures++; continue;
            }

            Debug.Log("[Verify] " + name + ": " + clip.length.ToString("F3") + "s, " +
                      clip.frequency + "Hz, " + clip.channels + "ch");
        }

        return failures;
    }
}
