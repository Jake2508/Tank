using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the settings screen into the title scene (SETTINGS_1b_IMPLEMENTATION).
///
/// The band, its dim and its column reveal are the pause screen's, through the shared
/// SlideBand component - the spec is explicit that neither the band nor its tween may be
/// rebuilt. Only the column contents differ, plus the read-only controls plate.
///
/// Edits L_MainMenu in place; the title screen, level select and everything else in the
/// scene keep their fileIDs.
/// </summary>
public static class SettingsBuilder
{
    const string ScenePath = "Assets/Scenes/L_MainMenu.unity";

    const string Menus = "Assets/UI/Menus/";
    const string Shared = "Assets/UI/Shared/";
    const string Hud = "Assets/UI/HUD/";
    const string Settings = SettingsSprites.Dir;
    const string Dim = Shared + "ui_square.png";

    const string RootName = "SettingsMenu";

    /// <summary>The slab art's native height; scaling the 9-slice by this keeps its lean.</summary>
    const float SlabNativeHeight = 55f;

    // ---- band, shared with pause ---------------------------------------------
    const float BandX = -60f, BandW = 480f;

    /// <summary>
    /// How far the band runs past the top and bottom of the screen. The design's 600
    /// height is 540 plus 30 at each end; expressing it as an overhang keeps that true
    /// when the canvas is taller.
    /// </summary>
    const float BandOverhang = 30f;
    const float ContentX = 66f, ContentTop = 52f, ContentBottom = 46f;

    /// <summary>
    /// The spec asks for 268. The delivered band slants harder than that allows: its
    /// opaque right edge is at screen x ~302 level with the lowest row, and a 268 column
    /// starting at +66 would reach 334 and hang off the band onto the title art. 226 is
    /// the same width the pause screen settled on for the same reason.
    /// </summary>
    const float ContentW = 226f;

    // ---- column ---------------------------------------------------------------
    const float TitleH = 42f;
    const float RuleY = -50f, RuleW = 120f, RuleH = 5f;
    const float RowsTop = 89f, RowH = 46f, RowGap = 22f;
    const float NoteGap = 22f, NoteH = 30f;
    const float BackH = 52f, HintH = 12f, HintGap = 6f;

    // ---- volume row -----------------------------------------------------------
    const float LabelH = 13f, TroughH = 16f, TroughY = -30f;
    const float KnobW = 10f, KnobH = 28f;

    // ---- controls plate -------------------------------------------------------
    const float PlateX = 448f, PlateY = -112f, PlateW = 430f;
    const float PlatePadX = 26f, PlatePadTop = 22f;
    const float PlateHeadingH = 14f, PlateHeadingGap = 20f;
    const float GlyphColW = 116f, TextColW = 210f, ColGap = 18f;
    const float PlateRowH = 46f, PlateRowGap = 18f;
    const float KeyW = 26f, KeyH = 25f, KeyGap = 3f, ShiftW = 92f;
    const float MouseW = 26f, MouseH = 38f;

    static float Tracking(float pixels, float fontSize)
    {
        return pixels * TitleScreenFonts.SamplingPointSize / fontSize;
    }

    [MenuItem("Tools/Tankeo/20 - Build Settings")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleScreenFonts.SairaAsset) == null)
            TitleScreenFonts.Generate();

        MenuSprites.Import();
        SettingsSprites.Generate();

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = FindRoot(scene, "Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[Settings] No Canvas in " + ScenePath);
            return;
        }

        var canvas = (RectTransform)canvasGo.transform;

        var root = Ensure(canvas, RootName);
        Stretch(root);
        var group = EnsureComponent<CanvasGroup>(root.gameObject);
        var menu = EnsureComponent<SettingsMenu>(root.gameObject);
        var focus = EnsureComponent<MenuFocusGroup>(root.gameObject);
        var band = EnsureComponent<SlideBand>(root.gameObject);

        var dim = BuildBand(root);
        var content = BuildContent(root, out var items, out var music, out var sfx, out var back);
        var plate = BuildPlate(root, out var plateGroup);

        // The band's own refs, so both screens run one curve.
        Bind(band, "band", root.Find("Band"));
        Bind(band, "dim", dim);
        BindArray(band, "contentItems", items.ToArray());
        // Lighter than pause: the title art sits behind this, not live gameplay.
        Bind(band, "dimColour", new Color32(20, 14, 10, 128));

        focus.SetItems(new List<MenuFocusItem> { music, sfx, back });
        Bind(focus, "horizontal", false);
        Bind(focus, "numberKeys", false);

        Bind(menu, "group", group);
        Bind(menu, "slide", band);
        Bind(menu, "plateGroup", plateGroup);
        Bind(menu, "plate", plate);
        Bind(menu, "plateFit", root.GetComponent<PlateFit>());
        Bind(menu, "musicSlider", music);
        Bind(menu, "sfxSlider", sfx);
        Bind(menu, "focus", focus);
        Bind(menu, "backButton", back);

        WireTitleButton(scene, menu);
        Validate(root, scene.name);

        // Left ACTIVE on purpose. SettingsMenu.Awake registers the static Instance that
        // MainMenuUI's click handler looks for, and then hides itself - but Awake never
        // runs on an object that is inactive when the scene loads, so deactivating it
        // here meant the SETTINGS button found a null Instance and silently did nothing.
        // LevelSelectUI works the same way: active in the scene, hidden in Awake.
        root.gameObject.SetActive(true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Settings] Build complete.");
    }

    // =========================================================================
    // Band
    // =========================================================================

    static Image BuildBand(RectTransform root)
    {
        var dim = Ensure(root, "Dim");
        Stretch(dim);
        var dimImage = Paint(dim, Dim, new Color32(20, 14, 10, 128), Image.Type.Simple);

        // Stretched vertically, not a fixed 600. The design's 600 is 540 plus a 30
        // overhang top and bottom, which only holds at 16:9 - at 4:3 the canvas is ~623
        // units tall and a fixed band leaves a gap at both ends, which the QA list calls
        // out. Anchoring to both edges with a negative inset keeps that overhang at any
        // aspect ratio.
        var band = Ensure(root, "Band");
        band.anchorMin = new Vector2(0f, 0f);
        band.anchorMax = new Vector2(0f, 1f);
        band.pivot = new Vector2(0f, 0.5f);
        band.sizeDelta = new Vector2(BandW, BandOverhang * 2f);
        band.anchoredPosition = new Vector2(BandX, 0f);
        band.localScale = Vector3.one;
        Paint(band, Menus + "pause_band.png", new Color32(31, 23, 18, 245), Image.Type.Simple);

        var lip = Ensure(band, "Lip");
        Stretch(lip);
        Paint(lip, Menus + "pause_band_lip.png", MenuPalette.Cream, Image.Type.Simple);

        return dimImage;
    }

    // =========================================================================
    // Column
    // =========================================================================

    static RectTransform BuildContent(RectTransform root, out List<CanvasGroup> items,
                                      out SettingsSlider music, out SettingsSlider sfx,
                                      out MenuButton back)
    {
        var content = Ensure(root, "Content");
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.sizeDelta = new Vector2(ContentW, -(ContentTop + ContentBottom));
        content.anchoredPosition = new Vector2(ContentX, -(ContentTop - ContentBottom) * 0.5f);
        content.localScale = Vector3.one;

        items = new List<CanvasGroup>();

        var title = Label(content, "Title", "SETTINGS", 34f, MenuPalette.Cream,
                          TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
        title.characterSpacing = Tracking(3f, 34f);
        TopLeft((RectTransform)title.transform, Vector2.zero, new Vector2(ContentW, TitleH));
        items.Add(EnsureComponent<CanvasGroup>(title.gameObject));

        var rule = Ensure(content, "Rule");
        TopLeft(rule, new Vector2(0f, RuleY), new Vector2(RuleW, RuleH));
        Slab(rule, Shared + "button_slab_fill.png", MenuPalette.Terracotta, RuleH, true, false);
        items.Add(EnsureComponent<CanvasGroup>(rule.gameObject));

        music = BuildRow(content, "Row_Music", "MUSIC", -RowsTop);
        items.Add(music.GetComponent<CanvasGroup>());

        sfx = BuildRow(content, "Row_Sfx", "SFX", -(RowsTop + RowH + RowGap));
        items.Add(sfx.GetComponent<CanvasGroup>());

        var note = Label(content, "Note", "VOLUME APPLIES LIVE · SAVED ON BACK", 9f,
                         MenuPalette.At(MenuPalette.Cream, 97),
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        note.characterSpacing = Tracking(2f, 9f);
        note.enableWordWrapping = true;
        note.lineSpacing = 20f;                       // the spec's 1.7 line height
        note.alignment = TextAlignmentOptions.TopLeft;
        TopLeft((RectTransform)note.transform,
                new Vector2(0f, -(RowsTop + RowH * 2f + RowGap + NoteGap)),
                new Vector2(ContentW, NoteH));
        items.Add(EnsureComponent<CanvasGroup>(note.gameObject));

        // Pinned to the bottom, with the flexible gap above, exactly as pause does it.
        back = BuildBack(content, new Vector2(0f, HintH + HintGap), new Vector2(ContentW, BackH));
        items.Add(back.GetComponent<CanvasGroup>());

        var hint = Label(content, "Hint", "ESC TO GO BACK", 10f, MenuPalette.HintPause,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        hint.characterSpacing = Tracking(2.5f, 10f);
        hint.alignment = TextAlignmentOptions.Left;
        var hintRt = (RectTransform)hint.transform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0f, 0f);
        hintRt.pivot = new Vector2(0f, 0f);
        hintRt.anchoredPosition = Vector2.zero;
        hintRt.sizeDelta = new Vector2(ContentW, HintH);
        hintRt.localScale = Vector3.one;
        items.Add(EnsureComponent<CanvasGroup>(hint.gameObject));

        return content;
    }

    /// <summary>
    /// A volume row. The trough and its fill reuse the shared slab art rather than a
    /// dedicated slider_trough sprite: it is already a -7 degree parallelogram, already
    /// tintable and already 9-sliced, so the fill can be resized without shearing - the
    /// same reason the upgrade screen's tier chips use it.
    /// </summary>
    static SettingsSlider BuildRow(RectTransform content, string name, string labelText, float y)
    {
        var row = Ensure(content, name);
        TopLeft(row, new Vector2(0f, y), new Vector2(ContentW, RowH));
        EnsureComponent<CanvasGroup>(row.gameObject);

        var component = EnsureComponent<SettingsSlider>(row.gameObject);
        Bind(component, "focusSeconds", 0.08f);

        var label = Label(row, "Label", labelText, 10f, MenuPalette.StatLabel,
                          TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        label.characterSpacing = Tracking(2.5f, 10f);
        label.alignment = TextAlignmentOptions.Left;
        TopLeft((RectTransform)label.transform, Vector2.zero, new Vector2(140f, LabelH));

        var value = Label(row, "Value", "0", 19f, MenuPalette.Yellow,
                          TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
        value.alignment = TextAlignmentOptions.Right;
        var valueRt = (RectTransform)value.transform;
        valueRt.anchorMin = valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.pivot = new Vector2(1f, 1f);
        valueRt.anchoredPosition = new Vector2(0f, 2f);
        valueRt.sizeDelta = new Vector2(90f, 22f);
        valueRt.localScale = Vector3.one;

        // The Slider lives on the trough, so its rect is the drag area.
        var trough = Ensure(row, "Trough");
        TopLeft(trough, new Vector2(0f, TroughY), new Vector2(ContentW, TroughH));
        var troughBody = Slab(trough, Shared + "button_slab_fill.png",
                              MenuPalette.IconPlate, TroughH, true, true);

        var keyline = Ensure(trough, "Keyline");
        Stretch(keyline);
        var keylineImage = Slab(keyline, Shared + "button_slab_border.png",
                                MenuPalette.IconPlateBorder, TroughH, true, false);

        // Anchor-driven, with zero sizeDelta: Unity's Slider sets the fill's anchorMax
        // from the value, so the rect has to be sized purely by its anchors.
        var fill = Ensure(trough, "Fill");
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta = Vector2.zero;
        fill.localScale = Vector3.one;
        Slab(fill, Shared + "button_slab_fill.png", MenuPalette.Yellow, TroughH, true, false);

        // An earlier build parented the knob straight to the trough and positioned it by
        // hand. Ensure() matches by name and parent, so it would happily create the new
        // one under HandleArea and leave that original sitting at the left end - two
        // knobs on one slider.
        var strandedKnob = trough.Find("Knob");
        if (strandedKnob != null) Object.DestroyImmediate(strandedKnob.gameObject);

        // The standard Unity slider hierarchy: the handle lives inside an area inset by
        // half its own width, which is what keeps the knob inside the trough's skewed
        // ends at 0 and 100 instead of hanging off them.
        var handleArea = Ensure(trough, "HandleArea");
        handleArea.anchorMin = new Vector2(0f, 0f);
        handleArea.anchorMax = new Vector2(1f, 1f);
        handleArea.pivot = new Vector2(0.5f, 0.5f);
        handleArea.offsetMin = new Vector2(KnobW * 0.5f, 0f);
        handleArea.offsetMax = new Vector2(-KnobW * 0.5f, 0f);
        handleArea.localScale = Vector3.one;

        var knob = Ensure(handleArea, "Knob");
        knob.pivot = new Vector2(0.5f, 0.5f);
        knob.sizeDelta = new Vector2(KnobW, KnobH);
        knob.anchoredPosition = Vector2.zero;
        knob.localScale = Vector3.one;

        var knobShadow = Ensure(knob, "Shadow");
        Centre(knobShadow, new Vector2(3f, -3f), new Vector2(KnobW, KnobH));
        Paint(knobShadow, Settings + "slider_knob.png", MenuPalette.Shadow, Image.Type.Simple);

        var knobBody = Ensure(knob, "Body");
        Centre(knobBody, Vector2.zero, new Vector2(KnobW, KnobH));
        var knobImage = Paint(knobBody, Settings + "slider_knob.png", MenuPalette.Cream, Image.Type.Simple);

        var slider = EnsureComponent<Slider>(trough.gameObject);
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.targetGraphic = troughBody;

        // Both of these were null, which is what broke mouse input entirely. Slider
        // resolves its click area as handleRect.parent ?? fillRect.parent, so with
        // neither assigned there was no rect to drag against - keyboard worked, because
        // SettingsSlider handles that itself, and the mouse did nothing at all.
        //
        // Handing Unity the fill and the handle also means it owns their placement, so
        // click-to-set, drag and the handle inset at both extremes all come for free.
        slider.fillRect = fill;
        slider.handleRect = knob;

        Bind(component, "slider", slider);
        Bind(component, "label", label);
        Bind(component, "value", value);
        Bind(component, "fill", fill);
        Bind(component, "knob", knob);
        Bind(component, "knobImage", knobImage);
        Bind(component, "troughKeyline", keylineImage);

        return component;
    }

    static MenuButton BuildBack(RectTransform parent, Vector2 position, Vector2 size)
    {
        var rt = Ensure(parent, "Button_Back");
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        EnsureComponent<CanvasGroup>(rt.gameObject);
        var button = EnsureComponent<MenuButton>(rt.gameObject);
        Bind(button, "focusSeconds", 0.08f);

        var glow = Ensure(rt, "Glow");
        Centre(glow, Vector2.zero, size + new Vector2(50f, 40f));
        var glowImage = Paint(glow, Hud + "hud_glow.png", MenuPalette.Clear, Image.Type.Simple);

        var shadow = Ensure(rt, "Shadow");
        Centre(shadow, new Vector2(5f, -5f), size);
        var shadowImage = Slab(shadow, Shared + "button_slab_fill.png",
                               MenuPalette.ShadowFocus, size.y, true, false);

        var fill = Ensure(rt, "Fill");
        Centre(fill, Vector2.zero, size);
        var fillImage = Slab(fill, Shared + "button_slab_fill.png", MenuPalette.Cream, size.y, true, true);

        var keyline = Ensure(rt, "Keyline");
        Centre(keyline, Vector2.zero, size);
        var keylineImage = Slab(keyline, Shared + "button_slab_border.png",
                                MenuPalette.Cream, size.y, true, false);

        // A sprite, not a text glyph. U+25C0 is not in Archivo Black, so a TMP arrow
        // bakes as a missing-glyph box; the level-select back button already reuses the
        // menu chevron for exactly this reason. Mirrored on X to point back.
        //
        // Under a new name because the earlier build made "Chevron" a TMP text object,
        // and a GameObject cannot carry both a text and an image Graphic - reusing it
        // would fail to add the Image and leave a null behind.
        var stale = rt.Find("Chevron");
        if (stale != null) Object.DestroyImmediate(stale.gameObject);

        var chevron = Ensure(rt, "ChevronIcon");
        chevron.anchorMin = chevron.anchorMax = new Vector2(0f, 0.5f);
        chevron.pivot = new Vector2(0f, 0.5f);
        chevron.anchoredPosition = new Vector2(22f, 0f);
        chevron.sizeDelta = new Vector2(14f, 14f);
        chevron.localScale = new Vector3(-1f, 1f, 1f);
        Paint(chevron, "Assets/UI/Main Menu/chevron_select.png", MenuPalette.Ink, Image.Type.Simple);

        var label = Label(rt, "Label", "BACK", 16f, MenuPalette.ButtonLabel,
                          TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        label.characterSpacing = Tracking(2.5f, 16f);
        label.alignment = TextAlignmentOptions.Left;
        var labelRt = (RectTransform)label.transform;
        Stretch(labelRt);
        // Chevron at 22 plus its 14 width plus the 11 gap.
        labelRt.offsetMin = new Vector2(47f, 0f);
        labelRt.offsetMax = new Vector2(-16f, 0f);

        // BACK rests cream because it is the focused-looking slab in the design, and
        // MenuButton drives the ink label from the same fill swap the pause buttons use.
        Bind(button, "fill", fillImage);
        Bind(button, "keyline", keylineImage);
        Bind(button, "shadow", shadowImage);
        Bind(button, "glow", glowImage);
        Bind(button, "label", label);
        Bind(button, "restFill", (Color)MenuPalette.ButtonPause);
        Bind(button, "restKeyline", (Color)MenuPalette.ButtonKeylinePause);
        Bind(button, "restShadow", (Color)MenuPalette.ShadowSoft);

        return button;
    }

    // =========================================================================
    // Controls plate
    // =========================================================================

    static RectTransform BuildPlate(RectTransform root, out CanvasGroup group)
    {
        var plate = Ensure(root, "ControlsPlate");
        plate.anchorMin = plate.anchorMax = new Vector2(1f, 1f);
        plate.pivot = new Vector2(1f, 1f);
        plate.sizeDelta = new Vector2(PlateW, PlateHeight());
        plate.anchoredPosition = new Vector2(-(960f - (PlateX + PlateW)), PlateY);
        plate.localScale = Vector3.one;

        group = EnsureComponent<CanvasGroup>(plate.gameObject);
        // Read-only reference: no hit targets, nothing focusable.
        group.interactable = false;
        group.blocksRaycasts = false;

        var fit = EnsureComponent<PlateFit>(root.gameObject);
        Bind(fit, "plate", plate);

        var shadow = Ensure(plate, "Shadow");
        Stretch(shadow);
        shadow.offsetMin = new Vector2(9f, -9f);
        shadow.offsetMax = new Vector2(9f, -9f);
        Paint(shadow, Dim, MenuPalette.ShadowFocus, Image.Type.Simple);

        var body = Ensure(plate, "Body");
        Stretch(body);
        Paint(body, Dim, new Color32(21, 16, 11, 235), Image.Type.Simple);

        var border = Ensure(plate, "Border");
        Stretch(border);
        Paint(border, Dim, MenuPalette.CardBorder, Image.Type.Simple);
        var inner = Ensure(border, "Inner");
        Stretch(inner);
        inner.offsetMin = new Vector2(3f, 3f);
        inner.offsetMax = new Vector2(-3f, -3f);
        Paint(inner, Dim, new Color32(21, 16, 11, 235), Image.Type.Simple);

        var heading = Label(plate, "Heading", "CONTROLS", 10f, MenuPalette.CardBorder,
                            TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        heading.characterSpacing = Tracking(3f, 10f);
        heading.alignment = TextAlignmentOptions.Center;
        var headingRt = (RectTransform)heading.transform;
        headingRt.anchorMin = new Vector2(0f, 1f);
        headingRt.anchorMax = new Vector2(1f, 1f);
        headingRt.pivot = new Vector2(0.5f, 1f);
        headingRt.sizeDelta = new Vector2(0f, PlateHeadingH);
        headingRt.anchoredPosition = new Vector2(0f, -PlatePadTop);
        headingRt.localScale = Vector3.one;

        float rowsTop = PlatePadTop + PlateHeadingH + PlateHeadingGap;

        BuildControlRow(plate, 0, rowsTop, "MOVE", "Arrow keys also work", ControlGlyph.Wasd);
        BuildControlRow(plate, 1, rowsTop, "AIM", "Turret follows the cursor", ControlGlyph.Mouse);
        BuildControlRow(plate, 2, rowsTop, "FIRE", "Hold left mouse", ControlGlyph.MouseFire);
        // Corrected from the spec's "Hold while turning": what actually ships is a meter
        // charged by hard cornering and spent with a tap when it is full.
        BuildControlRow(plate, 3, rowsTop, "DRIFT", "Corner hard, then tap", ControlGlyph.Shift);

        return plate;
    }

    static float PlateHeight()
    {
        return PlatePadTop + PlateHeadingH + PlateHeadingGap
             + PlateRowH * 4f + PlateRowGap * 3f + PlatePadTop;
    }

    enum ControlGlyph { Wasd, Mouse, MouseFire, Shift }

    static void BuildControlRow(RectTransform plate, int index, float top,
                                string title, string sub, ControlGlyph glyph)
    {
        var row = Ensure(plate, "Row_" + title);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-(PlatePadX * 2f), PlateRowH);
        row.anchoredPosition = new Vector2(0f, -(top + index * (PlateRowH + PlateRowGap)));
        row.localScale = Vector3.one;

        // Both columns are fixed so all four rows align as one block.
        var glyphCol = Ensure(row, "Glyph");
        glyphCol.anchorMin = glyphCol.anchorMax = new Vector2(0f, 0.5f);
        glyphCol.pivot = new Vector2(0f, 0.5f);
        glyphCol.sizeDelta = new Vector2(GlyphColW, PlateRowH);
        glyphCol.anchoredPosition = Vector2.zero;
        glyphCol.localScale = Vector3.one;

        switch (glyph)
        {
            case ControlGlyph.Wasd: BuildWasd(glyphCol); break;
            case ControlGlyph.Shift: BuildKeycap(glyphCol, "SHIFT", ShiftW, true, Vector2.zero); break;
            default: BuildMouse(glyphCol, glyph == ControlGlyph.MouseFire); break;
        }

        var text = Ensure(row, "Text");
        text.anchorMin = text.anchorMax = new Vector2(0f, 0.5f);
        text.pivot = new Vector2(0f, 0.5f);
        text.sizeDelta = new Vector2(TextColW, PlateRowH);
        text.anchoredPosition = new Vector2(GlyphColW + ColGap, 0f);
        text.localScale = Vector3.one;

        var titleLabel = Label(text, "Title", title, 11f, MenuPalette.Cream,
                               TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        titleLabel.characterSpacing = Tracking(1.5f, 11f);
        titleLabel.alignment = TextAlignmentOptions.Left;
        TopLeft((RectTransform)titleLabel.transform, new Vector2(0f, -4f), new Vector2(TextColW, 14f));

        var subLabel = Label(text, "Sub", sub, 13f, MenuPalette.CardBorder,
                             TitleScreenFonts.SairaAsset, null);
        subLabel.alignment = TextAlignmentOptions.TopLeft;
        subLabel.enableWordWrapping = false;
        TopLeft((RectTransform)subLabel.transform, new Vector2(0f, -22f), new Vector2(TextColW, 18f));
    }

    /// <summary>W centred over A S D, 113 wide in total.</summary>
    static void BuildWasd(RectTransform parent)
    {
        float rowW = KeyW * 3f + KeyGap * 2f;
        float left = (GlyphColW - rowW) * 0.5f;
        float topY = (KeyH + KeyGap) * 0.5f;

        BuildKeycap(parent, "W", KeyW, false, new Vector2(left + KeyW + KeyGap, topY));
        BuildKeycap(parent, "A", KeyW, false, new Vector2(left, topY - KeyH - KeyGap));
        BuildKeycap(parent, "S", KeyW, false, new Vector2(left + KeyW + KeyGap, topY - KeyH - KeyGap));
        BuildKeycap(parent, "D", KeyW, false, new Vector2(left + (KeyW + KeyGap) * 2f, topY - KeyH - KeyGap));
    }

    /// <summary>
    /// A key. SHIFT is the only one with a yellow border and label - it is the only key
    /// with a hold-modifier meaning, and the colour says so without needing a legend.
    /// </summary>
    static void BuildKeycap(RectTransform parent, string cap, float width, bool accent, Vector2 position)
    {
        var key = Ensure(parent, "Key_" + cap);
        key.anchorMin = key.anchorMax = new Vector2(0f, 0.5f);
        key.pivot = new Vector2(0f, 0.5f);
        key.sizeDelta = new Vector2(width, KeyH);
        key.anchoredPosition = accent ? new Vector2((GlyphColW - width) * 0.5f, 0f) : position;
        key.localScale = Vector3.one;

        var colour = accent ? MenuPalette.Yellow : MenuPalette.Cream;

        Slab(key, Shared + "button_slab_fill.png", MenuPalette.IconPlate, KeyH, true, false);

        var border = Ensure(key, "Border");
        Stretch(border);
        Slab(border, Shared + "button_slab_border.png", colour, KeyH, true, false);

        // Upright inside the skewed cap, so the letter never leans.
        var label = Label(key, "Label", cap, 10f, colour,
                          TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        label.alignment = TextAlignmentOptions.Center;
        Stretch((RectTransform)label.transform);
    }

    static void BuildMouse(RectTransform parent, bool fire)
    {
        var mouse = Ensure(parent, "Mouse");
        Centre(mouse, Vector2.zero, new Vector2(MouseW, MouseH));
        mouse.anchorMin = mouse.anchorMax = new Vector2(0.5f, 0.5f);

        if (fire)
        {
            // Yellow left button under the outline.
            var lmb = Ensure(mouse, "Lmb");
            Stretch(lmb);
            Paint(lmb, Settings + "mouse_lmb_fill.png", MenuPalette.Yellow, Image.Type.Simple);
        }

        var outline = Ensure(mouse, "Outline");
        Stretch(outline);
        Paint(outline, Settings + "mouse_outline.png", MenuPalette.Cream, Image.Type.Simple);
    }

    // =========================================================================
    // Title button
    // =========================================================================

    /// <summary>
    /// MainMenuUI already holds a reference to the SETTINGS slab and adds the listener
    /// itself; this only has to make sure the reference is still there after a rebuild.
    /// </summary>
    static void WireTitleButton(Scene scene, SettingsMenu menu)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var ui = root.GetComponentInChildren<MainMenuUI>(true);
            if (ui == null) continue;

            var so = new SerializedObject(ui);
            var button = so.FindProperty("settingsButton");

            if (button != null && button.objectReferenceValue == null)
                Debug.LogWarning("[Settings] MainMenuUI has no settingsButton - run the " +
                                 "title screen builder first.");
            return;
        }
    }

    static void Validate(RectTransform root, string sceneName)
    {
        int missing = 0;

        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            if (!(behaviour is SettingsMenu || behaviour is SlideBand || behaviour is MenuFocusGroup ||
                  behaviour is MenuFocusItem || behaviour is PlateFit)) continue;

            var so = new SerializedObject(behaviour);
            var prop = so.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.objectReferenceValue != null) continue;

                Debug.LogError("[Settings] Unbound: " + behaviour.GetType().Name + " . " + prop.propertyPath);
                missing++;
            }
        }

        if (missing == 0) Debug.Log("[Settings] " + sceneName + ": all references bound.");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    static Image Slab(RectTransform rt, string spritePath, Color tint, float height,
                      bool fillCenter, bool raycast)
    {
        var img = EnsureComponent<Image>(rt.gameObject);
        img.sprite = Sprite(spritePath);
        img.type = Image.Type.Sliced;
        // The border art has zero top/bottom slice, so its horizontal keylines live in
        // the centre cell - Fill Center has to stay on or they vanish.
        img.fillCenter = fillCenter;
        img.pixelsPerUnitMultiplier = SlabNativeHeight / Mathf.Max(1f, height);
        img.color = tint;
        img.raycastTarget = raycast;
        return img;
    }

    static Image Paint(RectTransform rt, string spritePath, Color tint, Image.Type type)
    {
        var img = EnsureComponent<Image>(rt.gameObject);
        img.sprite = Sprite(spritePath);
        img.type = type;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = tint;
        img.raycastTarget = false;
        return img;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == name) return go;
        return null;
    }

    static RectTransform Ensure(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null && existing is RectTransform rect)
        {
            rect.gameObject.SetActive(true);
            return rect;
        }
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    static void Centre(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    static void TopLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string content, float size,
                                 Color color, string fontPath, string materialPath)
    {
        var rt = Ensure(parent, name);
        var text = EnsureComponent<TextMeshProUGUI>(rt.gameObject);
        text.text = content;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);

        if (!string.IsNullOrEmpty(materialPath))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (mat != null) text.fontSharedMaterial = mat;
        }
        else if (text.font != null)
        {
            text.fontSharedMaterial = text.font.material;
        }

        text.fontSize = size;
        text.color = color;
        text.enableAutoSizing = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Sprite Sprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[Settings] Sprite not found: " + path);
        return s;
    }

    static void Bind(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Settings] No property " + property + " on " + target); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Settings] No property " + property + " on " + target); return; }
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Settings] No property " + property + " on " + target); return; }
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, Color value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Settings] No property " + property + " on " + target); return; }
        prop.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindArray<T>(Object target, string property, T[] values) where T : Object
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        if (list == null) { Debug.LogError("[Settings] No property " + property + " on " + target); return; }

        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
