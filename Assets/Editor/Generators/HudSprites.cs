using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws and imports the in-game HUD art for the "Decals" spec
/// (HUD_3B_IMPLEMENTATION.md section 7). Idempotent - a PNG is only rewritten when
/// its bytes actually change, so re-running this does not churn the repo.
///
/// The HUD art is generated rather than hand-exported because every shape in it is
/// a flat fill, a keyline or a ramp: keeping the geometry in code means the -7 degree
/// lean, the 3-unit keyline and the stripe period stay in step with the numbers the
/// builder lays out with.
///
/// Everything is authored white and tinted in the scene, following the house rule
/// set by the menu art. The one exception is the hull stripe, whose notch is a grey
/// band so that a single tint can carry the whole bar from yellow to alarm red.
///
/// Pixels-per-unit follows the house rule too: art is drawn at 4x the 960x540 canvas,
/// so PPU 400 makes one source pixel a quarter of a canvas unit.
/// </summary>
public static class HudSprites
{
    public const string Dir = "Assets/UI/HUD/";

    const int Scale = 4;                    // source pixels per canvas unit

    // ---- slab geometry, canvas units ----------------------------------------
    // Authored at the stat-plate height so the 9-slice runs at multiplier 1 for
    // every HUD decal: the keyline then stays exactly 3 units on all four sides,
    // and the lean keeps a constant 3.5-unit horizontal run instead of shearing
    // harder on the short plates. See section 3.0.
    const float SlabW = 40f, SlabH = 28f;
    const float SlabLean = 3.5f;            // 28 * tan(7 degrees)
    const float SlabStroke = 3f;
    const float SlabBorderX = 8f, SlabBorderY = 4f;

    // ---- hull stripe, canvas units -------------------------------------------
    const float StripeW = 25f, StripeH = 17f;
    const float StripeNotch = 3f;
    // The notch is #1F1712 at 89/255 over the fill, which composites to ~70% of
    // whatever the fill is tinted. Baking it as a grey band keeps the sprite
    // single-tint, so low hull only has to swap yellow for alarm.
    const float NotchValue = 0.7f;

    struct Spec
    {
        public string file;
        public float ppu;
        public Vector4 border;
        public bool repeat;                 // everything else clamps
    }

    static readonly Spec[] All =
    {
        new Spec { file = "hud_slab_fill.png",       ppu = 400f, border = SlabBorder },
        new Spec { file = "hud_slab_border.png",     ppu = 400f, border = SlabBorder },
        new Spec { file = "hud_hull_stripe.png",     ppu = 400f, repeat = true },
        new Spec { file = "hud_bottom_gradient.png", ppu = 400f },
        new Spec { file = "hud_vignette.png",        ppu = 100f },
        new Spec { file = "hud_glow.png",            ppu = 400f },
        new Spec { file = "hud_padlock.png",         ppu = 400f },
        new Spec { file = "hud_pip.png",             ppu = 400f },
        // The four upgrade icons used to be re-cut here from the pre-redesign
        // Textures/Icon-*.png set. The slots draw the shared icon_*_64 art from
        // Assets/UI/Menus/ now, so the card and the hot bar always agree.
    };

    static Vector4 SlabBorder
    {
        get
        {
            float x = SlabBorderX * Scale, y = SlabBorderY * Scale;
            return new Vector4(x, y, x, y);
        }
    }

    [MenuItem("Tools/Tankeo/7 - Generate HUD Sprites")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);

        int written = 0;
        written += Write("hud_slab_fill.png", Slab(fill: true)) ? 1 : 0;
        written += Write("hud_slab_border.png", Slab(fill: false)) ? 1 : 0;
        written += Write("hud_hull_stripe.png", HullStripe()) ? 1 : 0;
        written += Write("hud_bottom_gradient.png", BottomGradient()) ? 1 : 0;
        written += Write("hud_vignette.png", Vignette()) ? 1 : 0;
        written += Write("hud_glow.png", Glow()) ? 1 : 0;
        written += Write("hud_padlock.png", Padlock()) ? 1 : 0;
        written += Write("hud_pip.png", Pip()) ? 1 : 0;

        // New files are only on disk at this point, not in the database.
        if (written > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Import();
        Debug.Log("[HUD] Sprite art: " + written + " of " + All.Length + " redrawn.");
    }

    /// <summary>
    /// Applies the import settings the HUD needs. Split out from Generate so the
    /// builder can call it on its own without touching the PNGs.
    /// </summary>
    public static void Import()
    {
        int changed = 0;
        foreach (var spec in All)
            if (Apply(spec)) changed++;

        // SaveAndReimport queues the work, so in a batch-mode run the Sprite
        // sub-assets are not loadable until the database has caught up - anything
        // building UI in the same call would get a null sprite without this.
        if (changed > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var spec in All)
        {
            string path = Dir + spec.file;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) continue;

            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                Debug.LogError("[HUD] No Sprite at " + path);
        }
    }

    // =========================================================================
    // Shapes
    // =========================================================================

    /// <summary>
    /// The slab shared by every HUD decal: a parallelogram leaning 7 degrees left,
    /// as a solid body or as a keyline of its own outline.
    /// </summary>
    static Color[] Slab(bool fill)
    {
        int w = Px(SlabW), h = Px(SlabH);
        float lean = SlabLean * Scale;
        float stroke = SlabStroke * Scale;

        return Draw(w, h, (x, y) =>
        {
            float t = h <= 1 ? 0f : y / (h - 1f);       // 0 at the top row
            float left = lean * (1f - t);
            float right = w - lean * t;

            if (x < left || x > right) return 0f;
            if (fill) return 1f;

            bool edge = x < left + stroke || x > right - stroke ||
                        y < stroke || y > h - 1f - stroke;
            return edge ? 1f : 0f;
        });
    }

    /// <summary>
    /// One period of the hull fill: a yellow run and a notch, sheared to sit
    /// parallel with the slab. The shear wraps horizontally so the sprite still
    /// tiles seamlessly at Wrap Repeat.
    /// </summary>
    static Color[] HullStripe()
    {
        int w = Px(StripeW), h = Px(StripeH);
        float notch = StripeNotch * Scale;
        float lean = StripeH * Mathf.Tan(7f * Mathf.Deg2Rad) * Scale;

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = h <= 1 ? 0f : y / (h - 1f);
            float shift = lean * (1f - t);
            for (int x = 0; x < w; x++)
            {
                float src = Mathf.Repeat(x - shift, w);
                float value = src >= w - notch ? NotchValue : 1f;
                pixels[y * w + x] = new Color(value, value, value, 1f);
            }
        }
        return pixels;
    }

    /// <summary>Bottom-edge darkening. White with an alpha ramp so it stays tintable.</summary>
    static Color[] BottomGradient()
    {
        int w = Px(2f), h = Px(130f);
        return Draw(w, h, (x, y) =>
        {
            float t = h <= 1 ? 0f : y / (h - 1f);       // 0 at the top row
            // Smoothstep rather than linear: it leaves the top edge with no visible
            // seam while still reaching full strength at the bottom of the frame.
            return Mathf.SmoothStep(0f, 1f, t);
        });
    }

    /// <summary>Edge-weighted alpha for the low-hull alarm. Stretched full screen.</summary>
    static Color[] Vignette()
    {
        const int w = 256, h = 144;
        return Draw(w, h, (x, y) =>
        {
            float nx = x / (w - 1f) * 2f - 1f;
            float ny = y / (h - 1f) * 2f - 1f;
            float r = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Sqrt(2f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, r));
        });
    }

    /// <summary>Soft radial behind the drift plate while drifting.</summary>
    static Color[] Glow()
    {
        int w = Px(64f), h = w;
        return Draw(w, h, (x, y) =>
        {
            float nx = x / (w - 1f) * 2f - 1f;
            float ny = y / (h - 1f) * 2f - 1f;
            float r = Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
            return Mathf.SmoothStep(1f, 0f, r) * 0.85f;
        });
    }

    /// <summary>Locked-slot padlock: an outlined shackle over a solid body.</summary>
    static Color[] Padlock()
    {
        int w = Px(15f), h = Px(17f);
        float bodyTop = Px(7f);                        // 7 units of shackle, 10 of body
        float outer = Px(5f), inner = outer - Px(2f);  // radius 5, outline 2
        float cx = w * 0.5f, cy = bodyTop - Px(2f);

        return Draw(w, h, (x, y) =>
        {
            if (y >= bodyTop) return 1f;

            float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (y <= cy) return d >= inner && d <= outer ? 1f : 0f;

            // Straight legs carry the arch down to the body, in line with the ring.
            float dx = Mathf.Abs(x - cx);
            return dx >= inner && dx <= outer ? 1f : 0f;
        });
    }

    /// <summary>Drift meter pip. Sheared harder than the plates so it reads as a speed mark.</summary>
    static Color[] Pip()
    {
        int w = Px(13f), h = Px(6f);
        float lean = h * Mathf.Tan(20f * Mathf.Deg2Rad);

        return Draw(w, h, (x, y) =>
        {
            float t = h <= 1 ? 0f : y / (h - 1f);
            float left = lean * (1f - t);
            float right = w - lean * t;
            return x >= left && x <= right ? 1f : 0f;
        });
    }


    // =========================================================================
    // Plumbing
    // =========================================================================

    static int Px(float units)
    {
        return Mathf.RoundToInt(units * Scale);
    }

    /// <summary>
    /// Rasterises a coverage function into white pixels with the coverage as alpha.
    /// Supersampled, because every shape here has a slanted edge somewhere.
    /// </summary>
    static Color[] Draw(int w, int h, System.Func<float, float, float> coverage)
    {
        const int Samples = 4;
        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int sy = 0; sy < Samples; sy++)
                    for (int sx = 0; sx < Samples; sx++)
                        sum += coverage(x + (sx + 0.5f) / Samples - 0.5f,
                                        y + (sy + 0.5f) / Samples - 0.5f);

                pixels[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(sum / (Samples * Samples)));
            }
        }
        return pixels;
    }

    /// <summary>
    /// Encodes a pixel block to PNG and writes it only if the result differs from
    /// what is already there. Rows arrive top-down; Texture2D is bottom-up.
    /// </summary>
    static bool Write(string file, Color[] pixels)
    {
        if (pixels == null) return false;

        string path = Dir + file;
        int w, h;
        if (!Dimensions(file, pixels.Length, out w, out h)) return false;

        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var flipped = new Color[pixels.Length];
        for (int y = 0; y < h; y++)
            System.Array.Copy(pixels, y * w, flipped, (h - 1 - y) * w, w);
        texture.SetPixels(flipped);
        texture.Apply();

        var bytes = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        if (File.Exists(path) && Same(File.ReadAllBytes(path), bytes)) return false;

        File.WriteAllBytes(path, bytes);
        return true;
    }

    static bool Dimensions(string file, int count, out int w, out int h)
    {
        switch (file)
        {
            case "hud_slab_fill.png":
            case "hud_slab_border.png":     w = Px(SlabW); h = Px(SlabH); break;
            case "hud_hull_stripe.png":     w = Px(StripeW); h = Px(StripeH); break;
            case "hud_bottom_gradient.png": w = Px(2f); h = Px(130f); break;
            case "hud_vignette.png":        w = 256; h = 144; break;
            case "hud_glow.png":            w = Px(64f); h = Px(64f); break;
            case "hud_padlock.png":         w = Px(15f); h = Px(17f); break;
            case "hud_pip.png":             w = Px(13f); h = Px(6f); break;
            default:                        w = 0; h = 0; break;
        }

        if (w * h == count) return true;
        Debug.LogError("[HUD] " + file + " is " + count + " pixels, expected " + w + "x" + h);
        return false;
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    static bool Apply(Spec spec)
    {
        string path = Dir + spec.file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[HUD] Missing texture: " + path);
            return false;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        var wrap = spec.repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        bool dirty = false;
        Set(ref dirty, settings.textureType != TextureImporterType.Sprite, () => settings.textureType = TextureImporterType.Sprite);
        Set(ref dirty, settings.spriteMode != (int)SpriteImportMode.Single, () => settings.spriteMode = (int)SpriteImportMode.Single);
        Set(ref dirty, settings.spriteMeshType != SpriteMeshType.FullRect, () => settings.spriteMeshType = SpriteMeshType.FullRect);
        // Unity refuses to generate a Sprite from a texture with NPOT scaling, and
        // silently leaves only the Texture2D behind.
        Set(ref dirty, settings.npotScale != TextureImporterNPOTScale.None, () => settings.npotScale = TextureImporterNPOTScale.None);
        Set(ref dirty, settings.spriteGenerateFallbackPhysicsShape, () => settings.spriteGenerateFallbackPhysicsShape = false);
        Set(ref dirty, !Mathf.Approximately(settings.spritePixelsPerUnit, spec.ppu), () => settings.spritePixelsPerUnit = spec.ppu);
        Set(ref dirty, settings.spriteBorder != spec.border, () => settings.spriteBorder = spec.border);
        Set(ref dirty, !settings.alphaIsTransparency, () => settings.alphaIsTransparency = true);
        Set(ref dirty, settings.mipmapEnabled, () => settings.mipmapEnabled = false);
        Set(ref dirty, settings.filterMode != FilterMode.Bilinear, () => settings.filterMode = FilterMode.Bilinear);
        Set(ref dirty, settings.wrapMode != wrap, () => settings.wrapMode = wrap);
        Set(ref dirty, settings.readable, () => settings.readable = false);

        var platform = importer.GetDefaultPlatformTextureSettings();
        bool platformDirty = platform.maxTextureSize != 2048 ||
                             platform.textureCompression != TextureImporterCompression.Uncompressed;
        if (platformDirty)
        {
            platform.maxTextureSize = 2048;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
        }

        if (!dirty && !platformDirty) return false;

        importer.SetTextureSettings(settings);
        if (platformDirty) importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
        return true;
    }

    static void Set(ref bool dirty, bool condition, System.Action apply)
    {
        if (!condition) return;
        apply();
        dirty = true;
    }
}
