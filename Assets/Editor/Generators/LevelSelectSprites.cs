using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the import settings from LEVEL_SELECT_2b_IMPLEMENTATION.md section 0 to
/// the level-select art, so dropping the PNGs into the project is the only manual
/// step. Idempotent - files already set up are skipped rather than reimported.
///
/// Pixels-per-unit follows the house rule set by the title screen: art is authored
/// at 4x the 960x540 canvas, so PPU 400 makes Set Native Size give the intended
/// display size. The two 9-sliced sprites are the exception and are documented
/// where they are used.
/// </summary>
public static class LevelSelectSprites
{
    const string Sprites = "Assets/UI/Level Select/";
    const string Shared = "Assets/UI/Shared/";

    struct Spec
    {
        public string path;
        public float ppu;
        public Vector4 border;
        public bool compress;
    }

    static readonly Spec[] All =
    {
        // Photographic - compressed, they are the only heavy textures on the screen.
        new Spec { path = Sprites + "level_woodlands.png", ppu = 400f, compress = true },
        new Spec { path = Sprites + "level_desert.png",    ppu = 400f, compress = true },
        new Spec { path = Sprites + "level_snow.png",      ppu = 400f, compress = true },

        // Flat art with hard keylines - uncompressed, same as the title-screen sprites.
        new Spec { path = Sprites + "zone_heading_lockup.png", ppu = 400f },
        new Spec { path = Sprites + "levelselect_scrim.png",   ppu = 100f },
        new Spec { path = Shared + "ui_square.png",            ppu = 100f },

        // 600 px/unit matches the slab sprites, which puts the 27 px stroke inside
        // the 60 px 9-slice border at 4.5 canvas units with no multiplier.
        new Spec { path = Sprites + "card_frame.png", ppu = 600f, border = new Vector4(60f, 60f, 60f, 60f) },
    };

    [MenuItem("Tools/Tankeo/0 - Import Level Select Sprites")]
    public static void Import()
    {
        int changed = 0;
        foreach (var spec in All)
            if (Apply(spec)) changed++;

        // SaveAndReimport queues the work, so in a batch-mode run the new Sprite
        // sub-assets are not loadable until the database has actually caught up.
        // Anything building UI in the same call would get a null sprite without this.
        if (changed > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Settings living in the .meta is not the same as the imported artifact
        // matching them, so verify rather than assume, and force any stragglers.
        foreach (var spec in All)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(spec.path) != null) continue;

            AssetDatabase.ImportAsset(spec.path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<Sprite>(spec.path) == null)
                Debug.LogError("[LevelSelect] No Sprite at " + spec.path + " - contains: " + Describe(spec.path));
        }

        Debug.Log("[LevelSelect] Sprite import settings: " + changed + " of " + All.Length + " updated.");
    }

    static bool Apply(Spec spec)
    {
        var importer = AssetImporter.GetAtPath(spec.path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("[LevelSelect] Missing texture: " + spec.path);
            return false;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool dirty = false;
        Set(ref dirty, settings.textureType != TextureImporterType.Sprite, () => settings.textureType = TextureImporterType.Sprite);
        Set(ref dirty, settings.spriteMode != (int)SpriteImportMode.Single, () => settings.spriteMode = (int)SpriteImportMode.Single);
        Set(ref dirty, settings.spriteMeshType != SpriteMeshType.FullRect, () => settings.spriteMeshType = SpriteMeshType.FullRect);
        // Textures dropped in as plain textures default to NPOT scaling, and Unity
        // silently refuses to generate a Sprite from those - "Sprites can not be
        // generated from textures with NPOT scaling" - leaving only the Texture2D.
        Set(ref dirty, settings.npotScale != TextureImporterNPOTScale.None, () => settings.npotScale = TextureImporterNPOTScale.None);
        Set(ref dirty, settings.spriteGenerateFallbackPhysicsShape, () => settings.spriteGenerateFallbackPhysicsShape = false);
        Set(ref dirty, !Mathf.Approximately(settings.spritePixelsPerUnit, spec.ppu), () => settings.spritePixelsPerUnit = spec.ppu);
        Set(ref dirty, settings.spriteBorder != spec.border, () => settings.spriteBorder = spec.border);
        Set(ref dirty, !settings.alphaIsTransparency, () => settings.alphaIsTransparency = true);
        Set(ref dirty, settings.mipmapEnabled, () => settings.mipmapEnabled = false);
        Set(ref dirty, settings.filterMode != FilterMode.Bilinear, () => settings.filterMode = FilterMode.Bilinear);
        Set(ref dirty, settings.wrapMode != TextureWrapMode.Clamp, () => settings.wrapMode = TextureWrapMode.Clamp);
        Set(ref dirty, settings.readable, () => settings.readable = false);

        var platform = importer.GetDefaultPlatformTextureSettings();
        var compression = spec.compress ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        bool platformDirty = platform.maxTextureSize != 2048 || platform.textureCompression != compression;
        if (platformDirty)
        {
            platform.maxTextureSize = 2048;
            platform.textureCompression = compression;
        }

        if (!dirty && !platformDirty) return false;

        importer.SetTextureSettings(settings);
        if (platformDirty) importer.SetPlatformTextureSettings(platform);
        importer.SaveAndReimport();
        return true;
    }

    static string Describe(string path)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        if (all == null || all.Length == 0) return "nothing";

        var names = new string[all.Length];
        for (int i = 0; i < all.Length; i++)
            names[i] = all[i] == null ? "null" : all[i].GetType().Name + " '" + all[i].name + "'";
        return string.Join(", ", names);
    }

    static void Set(ref bool dirty, bool condition, System.Action apply)
    {
        if (!condition) return;
        apply();
        dirty = true;
    }
}
