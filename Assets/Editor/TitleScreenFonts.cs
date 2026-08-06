using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Bakes the title-screen TMP font assets from the TTFs in Assets/Fonts, so the
/// Font Asset Creator window never has to be opened by hand.
/// Settings follow TITLE_SCREEN_1a_IMPLEMENTATION.md section 1:
/// 1024x1024 atlas, ASCII, SDFAA, padding 9.
/// </summary>
public static class TitleScreenFonts
{
    const string FontDir = "Assets/Fonts";
    public const int SamplingPointSize = 90;
    const int AtlasPadding = 9;
    const int AtlasSize = 1024;

    public const string ArchivoAsset = FontDir + "/ArchivoBlack SDF.asset";
    public const string ArchivoUnderlayMat = FontDir + "/ArchivoBlack SDF - Underlay.mat";
    public const string BungeeAsset = FontDir + "/Bungee SDF.asset";
    public const string BungeeUnderlayMat = FontDir + "/Bungee SDF - Underlay.mat";

    /// <summary>
    /// Body copy for the upgrade cards (MENUS_IMPLEMENTATION.md section 1). Condensed
    /// is doing real work here - the descriptions have to fit three lines in the card's
    /// 164px column, which neither of the other two faces manages at a readable size.
    /// No underlay preset: the descriptions sit on the card body, not over terrain.
    /// </summary>
    public const string SairaAsset = FontDir + "/SairaCondensed SDF.asset";

    /// <summary>
    /// Characters the UI uses that fall outside the baked ASCII range. The input
    /// hints on both menu screens separate their key bindings with a middle dot, and
    /// the HUD's drift plate is marked with a double chevron; both render as a
    /// missing glyph until they are in the atlas.
    /// </summary>
    const string ExtraCharacters = "·»";

    [MenuItem("Tools/Tankeo/1 - Generate Menu Font Assets")]
    public static void Generate()
    {
        BuildFontAsset("ArchivoBlack-Regular.ttf", ArchivoAsset);
        BuildFontAsset("Bungee-Regular.ttf", BungeeAsset);
        BuildFontAsset("SairaCondensed-SemiBold.ttf", SairaAsset);
        BuildUnderlayPreset(ArchivoAsset, ArchivoUnderlayMat);
        // The HUD numerals sit straight over the terrain, so they need the shadow too.
        BuildUnderlayPreset(BungeeAsset, BungeeUnderlayMat);
        TopUp();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TitleScreen] Font assets generated.");
    }

    /// <summary>
    /// Adds ExtraCharacters to atlases that were already baked ASCII-only, without
    /// recreating the assets - regenerating them would orphan the underlay material,
    /// which points at the font's atlas sub-asset.
    /// </summary>
    public static void TopUp()
    {
        AddCharacters(ArchivoAsset);
        AddCharacters(BungeeAsset);
        AddCharacters(SairaAsset);
    }

    static void AddCharacters(string assetPath)
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fontAsset == null) return;

        bool missing = false;
        foreach (char c in ExtraCharacters)
            if (!fontAsset.characterLookupTable.ContainsKey(c)) missing = true;
        if (!missing) return;

        // TryAddCharacters refuses to touch a Static atlas, so open it just long
        // enough to bake the extras and close it again.
        var previousMode = fontAsset.atlasPopulationMode;
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        bool ok = fontAsset.TryAddCharacters(ExtraCharacters, out string notAdded);
        fontAsset.atlasPopulationMode = previousMode;

        if (!ok || !string.IsNullOrEmpty(notAdded))
            Debug.LogWarning("[TitleScreen] " + assetPath + " could not bake: " + notAdded);

        foreach (var atlas in fontAsset.atlasTextures)
            if (atlas != null) atlas.Apply(false, false);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[TitleScreen] Topped up " + assetPath + " with: " + ExtraCharacters);
    }

    static TMP_FontAsset BuildFontAsset(string ttfFileName, string assetPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        string ttfPath = FontDir + "/" + ttfFileName;
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogError("[TitleScreen] Missing font file: " + ttfPath);
            return null;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic);

        if (fontAsset == null)
        {
            Debug.LogError("[TitleScreen] Could not create a font asset from " + ttfPath);
            return null;
        }

        string assetName = Path.GetFileNameWithoutExtension(assetPath);
        fontAsset.name = assetName;

        // Bake the printable ASCII range, then freeze the atlas.
        var ascii = new StringBuilder();
        for (int c = 32; c <= 126; c++) ascii.Append((char)c);
        fontAsset.TryAddCharacters(ascii.ToString(), out string missing);
        if (!string.IsNullOrEmpty(missing))
            Debug.LogWarning("[TitleScreen] " + assetName + " is missing glyphs: " + missing);

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        AssetDatabase.CreateAsset(fontAsset, assetPath);

        fontAsset.material.name = assetName + " Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        var atlases = fontAsset.atlasTextures;
        for (int i = 0; i < atlases.Length; i++)
        {
            if (atlases[i] == null) continue;
            atlases[i].name = assetName + " Atlas" + (i == 0 ? "" : " " + i);
            AssetDatabase.AddObjectToAsset(atlases[i], fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }

    /// <summary>
    /// Hard offset shadow behind the glyphs - the same "no blur" rule as the slabs.
    /// </summary>
    static void BuildUnderlayPreset(string fontAssetPath, string materialPath)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null) return;

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (fontAsset == null) return;

        var mat = new Material(fontAsset.material) { name = Path.GetFileNameWithoutExtension(materialPath) };
        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color32(21, 16, 11, 160));
        mat.SetFloat("_UnderlayOffsetX", 0.15f);
        mat.SetFloat("_UnderlayOffsetY", -0.15f);
        mat.SetFloat("_UnderlayDilate", 0f);
        mat.SetFloat("_UnderlaySoftness", 0f);
        ShaderUtilities.GetShaderPropertyIDs();
        ShaderUtilities.UpdateShaderRatios(mat);

        AssetDatabase.CreateAsset(mat, materialPath);
    }
}
