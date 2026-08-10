using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws the three sprites the settings screen needs that were not in any handoff:
/// the slider knob and the two mouse glyphs on the controls plate.
///
/// The spec also names slider_trough.png and button_glow.png. Neither is generated
/// here, because the project already has both:
///   - the trough and its fill are button_slab_fill / button_slab_border, which are
///     already -7 degree parallelograms, already tintable and already 9-sliced. That is
///     the same technique the upgrade screen's tier chips use, so the slider inherits
///     the house lean by construction rather than by a second sprite agreeing with it.
///   - button_glow is hud_glow.png, which is what the pause RESUME button already uses
///     for exactly this focus glow.
///
/// Drawn at 4x the canvas size and imported at PPU 400, the house rule for generated
/// art. Idempotent: a PNG is only rewritten when its bytes change.
/// </summary>
public static class SettingsSprites
{
    public const string Dir = "Assets/UI/Settings/";

    const int Scale = 4;                       // source pixels per canvas unit
    const float Lean = 7f;                     // degrees, the house skew

    // ---- knob, canvas units --------------------------------------------------
    const float KnobW = 10f, KnobH = 28f;

    // ---- mouse glyph, canvas units -------------------------------------------
    // Source is 32 x 46 per the spec; it draws at 26 x 38.
    const float MouseW = 32f, MouseH = 46f;
    const float MouseStroke = 3f;
    /// <summary>Height of the button row, measured from the top of the shell.</summary>
    const float MouseButtonRow = 18f;

    struct Spec
    {
        public string file;
        public float ppu;
    }

    static readonly Spec[] All =
    {
        new Spec { file = "slider_knob.png",   ppu = 400f },
        new Spec { file = "mouse_outline.png", ppu = 400f },
        new Spec { file = "mouse_lmb_fill.png", ppu = 400f },
    };

    [MenuItem("Tools/Tankeo/19 - Generate Settings Sprites")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);

        int written = 0;
        written += Write("slider_knob.png", Knob()) ? 1 : 0;
        written += Write("mouse_outline.png", Mouse(fillLeftButton: false)) ? 1 : 0;
        written += Write("mouse_lmb_fill.png", Mouse(fillLeftButton: true)) ? 1 : 0;

        if (written > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Import();
        Debug.Log("[Settings] Sprite art: " + written + " of " + All.Length + " redrawn.");
    }

    public static void Import()
    {
        int changed = 0;
        foreach (var spec in All) if (Apply(spec)) changed++;

        if (changed > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var spec in All)
        {
            string path = Dir + spec.file;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) continue;

            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                Debug.LogError("[Settings] No Sprite at " + path);
        }
    }

    // =========================================================================
    // Shapes
    // =========================================================================

    /// <summary>
    /// The slider knob: a solid parallelogram leaning with everything else.
    ///
    /// Drawn rather than 9-sliced from the shared slab because at 10 units wide the
    /// slab's 20-unit end caps have nowhere to go - they would overlap and shear the
    /// knob far harder than the trough it slides along.
    /// </summary>
    static Color[] Knob()
    {
        int w = Px(KnobW), h = Px(KnobH);
        float lean = KnobH * Mathf.Tan(Lean * Mathf.Deg2Rad) * Scale;

        return Draw(w, h, (x, y) =>
        {
            // 0 at the top row, so the lean runs the same way as the slab art.
            float t = h <= 1 ? 0f : y / (h - 1f);
            float left = lean * (1f - t);
            float right = w - lean * t;

            return x >= left && x <= right ? 1f : 0f;
        });
    }

    /// <summary>
    /// The mouse glyph: a rounded shell with a button row across the top and a divider
    /// down the middle. Either the outline on its own, or the left button as a solid
    /// shape to sit underneath it in yellow.
    /// </summary>
    static Color[] Mouse(bool fillLeftButton)
    {
        int w = Px(MouseW), h = Px(MouseH);
        float stroke = MouseStroke * Scale;
        float row = MouseButtonRow * Scale;
        float mid = w * 0.5f;

        return Draw(w, h, (x, y) =>
        {
            bool inside = InShell(x, y, w, h, 0f);
            if (!inside) return 0f;

            if (fillLeftButton)
            {
                // Left button only: above the row, left of the divider, and inset by the
                // stroke so the outline drawn over it still reads as a keyline.
                bool inButton = y <= row - stroke * 0.5f && x <= mid - stroke * 0.5f;
                return inButton && InShell(x, y, w, h, stroke) ? 1f : 0f;
            }

            // Outline: in the shell but not in the shell shrunk by the stroke.
            if (!InShell(x, y, w, h, stroke)) return 1f;

            // The horizontal split between the buttons and the body.
            if (Mathf.Abs(y - row) <= stroke * 0.5f) return 1f;

            // The divider between left and right buttons, above the split only.
            if (y < row && Mathf.Abs(x - mid) <= stroke * 0.5f) return 1f;

            return 0f;
        });
    }

    /// <summary>
    /// The mouse silhouette: a capsule, rounder at the top than the bottom.
    /// <paramref name="inset"/> shrinks it, which is how the outline is produced.
    /// </summary>
    static bool InShell(float x, float y, int w, int h, float inset)
    {
        float left = inset, right = w - 1f - inset;
        float top = inset, bottom = h - 1f - inset;
        if (x < left || x > right || y < top || y > bottom) return false;

        float halfW = (right - left) * 0.5f;
        float centreX = (left + right) * 0.5f;

        // Top: a half-ellipse as wide as the shell and about 40% of its height.
        float topRadius = (bottom - top) * 0.4f;
        if (y < top + topRadius)
        {
            float dx = (x - centreX) / halfW;
            float dy = (top + topRadius - y) / topRadius;
            return dx * dx + dy * dy <= 1f;
        }

        // Bottom: a gentler round-off so it reads as a mouse rather than a pill.
        float bottomRadius = (bottom - top) * 0.22f;
        if (y > bottom - bottomRadius)
        {
            float dx = (x - centreX) / halfW;
            float dy = (y - (bottom - bottomRadius)) / bottomRadius;
            return dx * dx + dy * dy <= 1f;
        }

        return true;
    }

    // =========================================================================
    // Plumbing
    // =========================================================================

    static int Px(float units) => Mathf.RoundToInt(units * Scale);

    /// <summary>
    /// Fills a buffer from a coverage function, sampling 2x2 per pixel so the curves
    /// come out antialiased rather than stepped. White throughout - every one of these
    /// is tinted in the scene, following the house rule.
    /// </summary>
    static Color[] Draw(int w, int h, Func<float, float, float> coverage)
    {
        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float a = 0f;
                a += coverage(x + 0.25f, y + 0.25f);
                a += coverage(x + 0.75f, y + 0.25f);
                a += coverage(x + 0.25f, y + 0.75f);
                a += coverage(x + 0.75f, y + 0.75f);
                a *= 0.25f;

                pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        return pixels;
    }

    static bool Write(string file, Color[] pixels)
    {
        if (pixels == null) return false;

        int w, h;
        if (!Dimensions(file, pixels.Length, out w, out h)) return false;

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        // Draw works top-down; Texture2D is bottom-up.
        var flipped = new Color[pixels.Length];
        for (int y = 0; y < h; y++)
            Array.Copy(pixels, y * w, flipped, (h - 1 - y) * w, w);

        texture.SetPixels(flipped);
        texture.Apply();

        byte[] bytes = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        string path = Dir + file;
        if (File.Exists(path) && Same(File.ReadAllBytes(path), bytes)) return false;

        File.WriteAllBytes(path, bytes);
        return true;
    }

    static bool Dimensions(string file, int count, out int w, out int h)
    {
        switch (file)
        {
            case "slider_knob.png": w = Px(KnobW); h = Px(KnobH); break;
            default:                w = Px(MouseW); h = Px(MouseH); break;
        }

        if (w * h == count) return true;
        Debug.LogError("[Settings] " + file + " is " + count + " pixels, expected " + w + "x" + h);
        return false;
    }

    static bool Apply(Spec spec)
    {
        string path = Dir + spec.file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError("[Settings] Missing texture: " + path); return false; }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool dirty = false;
        Set(ref dirty, settings.textureType != TextureImporterType.Sprite, () => settings.textureType = TextureImporterType.Sprite);
        Set(ref dirty, settings.spriteMode != (int)SpriteImportMode.Single, () => settings.spriteMode = (int)SpriteImportMode.Single);
        Set(ref dirty, settings.spriteMeshType != SpriteMeshType.FullRect, () => settings.spriteMeshType = SpriteMeshType.FullRect);
        Set(ref dirty, settings.npotScale != TextureImporterNPOTScale.None, () => settings.npotScale = TextureImporterNPOTScale.None);
        Set(ref dirty, settings.spriteGenerateFallbackPhysicsShape, () => settings.spriteGenerateFallbackPhysicsShape = false);
        Set(ref dirty, !Mathf.Approximately(settings.spritePixelsPerUnit, spec.ppu), () => settings.spritePixelsPerUnit = spec.ppu);
        Set(ref dirty, settings.spriteBorder != Vector4.zero, () => settings.spriteBorder = Vector4.zero);
        Set(ref dirty, !settings.alphaIsTransparency, () => settings.alphaIsTransparency = true);
        Set(ref dirty, settings.mipmapEnabled, () => settings.mipmapEnabled = false);
        Set(ref dirty, settings.filterMode != FilterMode.Bilinear, () => settings.filterMode = FilterMode.Bilinear);
        Set(ref dirty, settings.wrapMode != TextureWrapMode.Clamp, () => settings.wrapMode = TextureWrapMode.Clamp);
        Set(ref dirty, settings.readable, () => settings.readable = false);

        var platform = importer.GetDefaultPlatformTextureSettings();
        bool platformDirty = platform.maxTextureSize != 512 ||
                             platform.textureCompression != TextureImporterCompression.Uncompressed;
        if (platformDirty)
        {
            platform.maxTextureSize = 512;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
        }

        if (!dirty && !platformDirty) return false;

        importer.SetTextureSettings(settings);
        if (platformDirty) importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
        return true;
    }

    static void Set(ref bool dirty, bool condition, Action apply)
    {
        if (!condition) return;
        apply();
        dirty = true;
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
