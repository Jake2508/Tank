using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the upgrade / pause / game-over art (MENUS_IMPLEMENTATION.md
/// section 1).
///
/// Unlike HudSprites and LevelSelectSprites, nothing here is drawn in code - these
/// PNGs arrived authored from the design handoff, so this is the import half of that
/// pattern only. Every one of them is a white tint target: the colour belongs on the
/// Image, never in the file.
///
/// No sprite here is 9-sliced. The card and the pause band carry their skew in the
/// pixels, so stretching a border region would shear the lean by a different amount
/// at each size - which is exactly what the handoff warns against.
/// </summary>
public static class MenuSprites
{
    public const string Dir = "Assets/UI/Menus/";

    struct Spec
    {
        public string file;
        public int maxSize;
    }

    /// <summary>
    /// Pixels-per-unit is 100 across the board, which makes a sprite's native size
    /// equal its pixel size in the 960x540 canvas. The builder sets every rect
    /// explicitly, so this only has to stay predictable rather than clever.
    /// </summary>
    const float Ppu = 100f;

    static readonly Spec[] All =
    {
        // Card icons at 38px. The 256s are the masters.
        new Spec { file = "icon_armour.png",         maxSize = 256 },
        new Spec { file = "icon_turret.png",         maxSize = 256 },
        new Spec { file = "icon_speed.png",          maxSize = 256 },
        new Spec { file = "icon_demolition.png",     maxSize = 256 },

        // The pixel-exact set, for the 20px HUD slots. A 256 icon downsampled to 20
        // with mipmaps off - which hard-edged UI needs - loses the silhouette; these
        // are only a 3.2x reduction rather than 12.8x.
        new Spec { file = "icon_armour_64.png",      maxSize = 64 },
        new Spec { file = "icon_turret_64.png",      maxSize = 64 },
        new Spec { file = "icon_speed_64.png",       maxSize = 64 },
        new Spec { file = "icon_demolition_64.png",  maxSize = 64 },

        new Spec { file = "upgrade_card_body.png",   maxSize = 512 },
        new Spec { file = "upgrade_card_border.png", maxSize = 512 },
        new Spec { file = "upgrade_card_head.png",   maxSize = 256 },
        new Spec { file = "upgrade_card_rule.png",   maxSize = 256 },

        new Spec { file = "pause_band.png",          maxSize = 1024 },
        new Spec { file = "pause_band_lip.png",      maxSize = 1024 },

        new Spec { file = "vignette_alarm.png",      maxSize = 256 },
    };

    [MenuItem("Tools/Tankeo/10 - Import Menu Sprites")]
    public static void Import()
    {
        int changed = 0;
        foreach (var spec in All)
            if (Apply(spec)) changed++;

        // SaveAndReimport queues the work, so in a batch-mode run the Sprite
        // sub-assets are not loadable until the database has caught up - the builder
        // running in the same call would get a null sprite without this.
        if (changed > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var spec in All)
        {
            string path = Dir + spec.file;
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) continue;

            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                Debug.LogError("[Menus] No Sprite at " + path);
        }

        Debug.Log("[Menus] Sprite import: " + changed + " of " + All.Length + " updated.");
    }

    static bool Apply(Spec spec)
    {
        string path = Dir + spec.file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[Menus] Missing texture: " + path);
            return false;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool dirty = false;
        Set(ref dirty, settings.textureType != TextureImporterType.Sprite, () => settings.textureType = TextureImporterType.Sprite);
        Set(ref dirty, settings.spriteMode != (int)SpriteImportMode.Single, () => settings.spriteMode = (int)SpriteImportMode.Single);
        Set(ref dirty, settings.spriteMeshType != SpriteMeshType.FullRect, () => settings.spriteMeshType = SpriteMeshType.FullRect);
        // Unity refuses to generate a Sprite from a texture with NPOT scaling, and
        // silently leaves only the Texture2D behind. The card sprites are 234x306.
        Set(ref dirty, settings.npotScale != TextureImporterNPOTScale.None, () => settings.npotScale = TextureImporterNPOTScale.None);
        Set(ref dirty, settings.spriteGenerateFallbackPhysicsShape, () => settings.spriteGenerateFallbackPhysicsShape = false);
        Set(ref dirty, !Mathf.Approximately(settings.spritePixelsPerUnit, Ppu), () => settings.spritePixelsPerUnit = Ppu);
        // Explicitly zero: an inherited border would 9-slice the skew.
        Set(ref dirty, settings.spriteBorder != Vector4.zero, () => settings.spriteBorder = Vector4.zero);
        Set(ref dirty, !settings.alphaIsTransparency, () => settings.alphaIsTransparency = true);
        Set(ref dirty, settings.mipmapEnabled, () => settings.mipmapEnabled = false);
        Set(ref dirty, settings.filterMode != FilterMode.Bilinear, () => settings.filterMode = FilterMode.Bilinear);
        // The vignette stretches to the full screen; Repeat would wrap its dark edge
        // back around to the opposite side.
        Set(ref dirty, settings.wrapMode != TextureWrapMode.Clamp, () => settings.wrapMode = TextureWrapMode.Clamp);
        Set(ref dirty, settings.readable, () => settings.readable = false);

        var platform = importer.GetDefaultPlatformTextureSettings();
        // Uncompressed: every one of these is a hard-edged keyline or a flat
        // silhouette, and DXT ringing on a 3px cream line is visible at 960x540.
        bool platformDirty = platform.maxTextureSize != spec.maxSize ||
                             platform.textureCompression != TextureImporterCompression.Uncompressed;
        if (platformDirty)
        {
            platform.maxTextureSize = spec.maxSize;
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
