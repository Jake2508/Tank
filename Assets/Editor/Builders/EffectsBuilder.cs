using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the particle prefabs for firing, drifting and felling trees.
///
/// Generated rather than hand-authored for the same reason the HUD art and the SFX are:
/// it is re-runnable, the parameters live in the repo as readable numbers, and every
/// effect is built from the same few parts so they read as one family.
///
/// Everything uses flat untextured quads and cubes in the game palette. That is a
/// deliberate match for the low-poly, flat-shaded look - soft glowy sprites would sit
/// on top of this art rather than in it.
///
/// One-shot systems set stopAction = Destroy, so nothing spawned in combat has to be
/// cleaned up by the code that spawned it.
/// </summary>
public static class EffectsBuilder
{
    public const string Dir = "Assets/Prefabs/Particles/Generated/";
    const string MaterialPath = Dir + "FX_Flat.mat";

    // The game palette, shared with the HUD and the menus.
    static readonly Color Cream = new Color32(244, 238, 220, 255);
    static readonly Color Yellow = new Color32(233, 201, 63, 255);
    static readonly Color Terracotta = new Color32(196, 85, 47, 255);
    static readonly Color Smoke = new Color32(120, 112, 100, 255);
    static readonly Color Bark = new Color32(104, 68, 44, 255);

    [MenuItem("Tools/Tankeo/18 - Build Effects")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);

        var material = EnsureMaterial();

        Save(MuzzleFlash(material), "fx_muzzle_flash");
        Save(MuzzleSmoke(material), "fx_muzzle_smoke");
        Save(TreeSplinter(material), "fx_tree_splinter");
        Save(DriftSparks(material), "fx_drift_sparks");
        Save(BoostAura(material), "fx_boost_aura");

        AssetDatabase.SaveAssets();
        Debug.Log("[FX] Effects built.");
    }

    static Material EnsureMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;

        // Sprites/Default rather than a URP particle shader: it reads vertex colour,
        // alpha blends, needs no texture, and exists in every project regardless of
        // pipeline - which keeps this script from depending on URP being installed.
        var shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = "FX_Flat" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    // =========================================================================
    // The effects
    // =========================================================================

    /// <summary>
    /// The shot leaving the barrel: one big cream flash for two frames, plus a spray of
    /// yellow sparks along the bore. Short on purpose - at a half-second reload anything
    /// longer is still on screen when the next round goes out.
    /// </summary>
    static GameObject MuzzleFlash(Material material)
    {
        var root = new GameObject("fx_muzzle_flash");

        var flash = Burst(root, "Flash", material, 1);
        var flashMain = flash.main;
        flashMain.startLifetime = 0.07f;
        flashMain.startSize = 2.2f;
        flashMain.startColor = Cream;
        flashMain.startSpeed = 0f;
        // Random roll, so repeated shots do not stamp an identical quad.
        flashMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Fade(flash, Cream, 1f, 0f);
        Grow(flash, 1f, 1.6f);

        var sparks = Burst(root, "Sparks", material, 10);
        var sparkMain = sparks.main;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(9f, 18f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(Yellow, Cream);
        sparkMain.gravityModifier = 0.6f;
        Cone(sparks, 14f, 0.1f);
        Fade(sparks, Yellow, 1f, 0f);
        Stretch(sparks, 2.5f);

        return root;
    }

    /// <summary>
    /// The puff left hanging after the flash. Kept thin and quick-fading: at this reload
    /// rate a heavy cloud would never clear from around the tank.
    /// </summary>
    static GameObject MuzzleSmoke(Material material)
    {
        var root = new GameObject("fx_muzzle_smoke");

        var smoke = Burst(root, "Smoke", material, 5);
        var main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startColor = Smoke;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.08f;                 // drifts up as it thins
        Cone(smoke, 22f, 0.15f);
        // Never above a third: this sits over gameplay and must not obscure a target.
        Fade(smoke, Smoke, 0.32f, 0f);
        Grow(smoke, 0.6f, 1.9f);

        return root;
    }

    /// <summary>
    /// A tree giving way: bark-coloured cubes thrown out of the trunk, plus a few pale
    /// splinters. Mesh cubes rather than quads because the whole game is faceted.
    /// </summary>
    static GameObject TreeSplinter(Material material)
    {
        var root = new GameObject("fx_tree_splinter");

        var chunks = Burst(root, "Chunks", material, 14);
        var main = chunks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8f);
        main.startColor = new ParticleSystem.MinMaxGradient(Bark, Terracotta);
        main.gravityModifier = 1.6f;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        Cone(chunks, 55f, 0.4f);
        Cubes(chunks);
        Spin(chunks, 4f);
        Fade(chunks, Cream, 1f, 0f);

        var splinters = Burst(root, "Splinters", material, 8);
        var splinterMain = splinters.main;
        splinterMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        splinterMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        splinterMain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        splinterMain.startColor = Cream;
        splinterMain.gravityModifier = 1.1f;
        Cone(splinters, 70f, 0.3f);
        Fade(splinters, Cream, 0.85f, 0f);
        Stretch(splinters, 2f);

        return root;
    }

    /// <summary>
    /// Struck off the tracks while the tank slides. Loops, and the drift controller
    /// switches emission on and off rather than spawning a new system per slide.
    /// </summary>
    static GameObject DriftSparks(Material material)
    {
        var root = new GameObject("fx_drift_sparks");

        var sparks = Continuous(root, "Sparks", material, 34f);
        var main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
        main.startColor = new ParticleSystem.MinMaxGradient(Yellow, Terracotta);
        main.gravityModifier = 0.9f;
        Cone(sparks, 42f, 0.35f);
        Fade(sparks, Yellow, 1f, 0f);
        Stretch(sparks, 2.2f);

        return root;
    }

    /// <summary>
    /// The boost itself: a bright yellow wake pouring off the hull for as long as it
    /// lasts. Loud on purpose - it is the payoff for a full meter and the read that the
    /// player is currently untouchable.
    /// </summary>
    static GameObject BoostAura(Material material)
    {
        var root = new GameObject("fx_boost_aura");

        var wake = Continuous(root, "Wake", material, 60f);
        var main = wake.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startColor = new ParticleSystem.MinMaxGradient(Yellow, Cream);
        main.gravityModifier = -0.15f;
        Cone(wake, 30f, 0.9f);
        Fade(wake, Yellow, 0.9f, 0f);
        Grow(wake, 1f, 0.2f);

        return root;
    }

    // =========================================================================
    // Parts
    // =========================================================================

    static ParticleSystem Burst(GameObject root, string name, Material material, int count)
    {
        var ps = Create(root, name, material);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        // Removes itself once the last particle dies, so callers can fire and forget.
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

        return ps;
    }

    static ParticleSystem Continuous(GameObject root, string name, Material material, float rate)
    {
        var ps = Create(root, name, material);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;                  // the gameplay code drives this one
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        return ps;
    }

    static ParticleSystem Create(GameObject root, string name, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);

        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        // World space, or a burst spawned on a moving tank gets dragged along with it -
        // the same mistake the shell used to make.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 10;

        return ps;
    }

    static void Cone(ParticleSystem ps, float angle, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = radius;
    }

    /// <summary>Fades a colour out over the particle's life.</summary>
    static void Fade(ParticleSystem ps, Color colour, float from, float to)
    {
        var over = ps.colorOverLifetime;
        over.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
            new[] { new GradientAlphaKey(from, 0f), new GradientAlphaKey(from * 0.8f, 0.45f),
                    new GradientAlphaKey(to, 1f) });

        over.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    static void Grow(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }

    static void Spin(ParticleSystem ps, float radiansPerSecond)
    {
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
        rotation.z = new ParticleSystem.MinMaxCurve(-radiansPerSecond, radiansPerSecond);
    }

    /// <summary>Elongates a particle along its travel, which reads as speed.</summary>
    static void Stretch(ParticleSystem ps, float scale)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = scale;
    }

    static void Cubes(ParticleSystem ps)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;

        // The engine's own cube, so this pulls in no art dependency.
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        renderer.mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(cube);
    }

    static void Save(GameObject root, string name)
    {
        string path = Dir + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log("[FX] " + name);
    }
}
