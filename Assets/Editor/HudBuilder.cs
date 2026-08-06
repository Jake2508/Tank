using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the in-game HUD to the "Decals" spec (HUD_3B_IMPLEMENTATION.md) in all
/// three play scenes. Idempotent - run it as often as you like.
///
/// Unlike the menu specs, this one is written in 960x540 units already, so every
/// number below is a literal canvas value.
///
/// The HUD gets its own Canvas rather than joining the one the pause / upgrade /
/// game-over screens live on: it has to fade as a unit while they stay solid, and
/// it must never take a raycast, which is easier to guarantee for a canvas with no
/// GraphicRaycaster at all.
/// </summary>
public static class HudBuilder
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/L_Woodlands.unity",
        "Assets/Scenes/L_Desert.unity",
        "Assets/Scenes/L_Snowy.unity",
    };

    const string Hud = HudSprites.Dir;
    const string Shared = "Assets/UI/Shared/";

    const string CanvasName = "HudCanvas";

    /// <summary>The old HUD's four containers, replaced wholesale by HudCanvas.</summary>
    static readonly string[] Retired =
    {
        "BottomLeftContainer", "BottomRightContainer", "TopRightContainer", "BottomBar",
    };

    // ---- layout, 960x540 units ----------------------------------------------
    const float ScrimHeight = 130f;

    const float StatX = -18f, StatY = -18f, StatW = 150f, StatH = 78f;
    const float TimePlateH = 40f, KillPlateH = 30f, StatGap = 8f;

    const float RowX = 20f, RowY = 20f, SlotSize = 42f, SlotGap = 8f;
    const int SlotCount = 4;

    const float HullX = 0f, HullY = 22f, HullW = 430f;
    const float LabelRowH = 14f, BarH = 23f, XpRowH = 15f, ClusterGap = 7f;
    const float TrackInsetX = 5f, TrackInsetY = 3f;
    const float XpRowW = 366f, LvChipW = 46f, HairlineH = 5f, HairlineGap = 8f;

    const float DriftX = -20f, DriftY = 20f, DriftW = 120f, DriftH = 48f;
    const float DriftPlateH = 22f, PipW = 13f, PipH = 6f, PipGap = 4f;
    const int PipCount = 3;

    const float SlotPipW = 7f, SlotPipH = 3f, SlotPipGap = 2f;
    const int SlotPipCount = 3;
    const float IconSize = 20f, PadlockW = 15f, PadlockH = 17f;

    // ---- type ----------------------------------------------------------------
    const float MicroSize = 9f, MicroSizeLarge = 10f;
    const float TimeSize = 25f, KillSize = 19f, HullValueSize = 15f, ChevronSize = 17f;

    /// <summary>
    /// Width reserved for mm:ss so the timer plate never reflows. Sized for 00:00,
    /// which is the widest the clock gets - zero is Bungee's widest digit.
    /// </summary>
    const float ClockWidth = 84f;

    /// <summary>
    /// TMP character spacing is in font design units, so a tracking figure in canvas
    /// pixels has to be scaled by the atlas sampling size to survive a font size
    /// change. The spec quotes tracking in pixels; this converts.
    /// </summary>
    static float Tracking(float pixels, float fontSize)
    {
        return pixels * TitleScreenFonts.SamplingPointSize / fontSize;
    }

    struct SlotSpec
    {
        public string node, icon;
        public UpgradeType type;
    }

    /// <summary>
    /// The slots now carry the same four icons as the upgrade cards, so a shield in the
    /// hot bar is the shield the player picked. They were previously re-cut from the
    /// pre-redesign Textures/Icon-*.png set, which no longer matches anything else.
    ///
    /// The 64px variants, not the 256 masters: these draw at 20px with mipmaps off,
    /// and a 12.8x downsample loses the silhouette these icons are built around.
    /// </summary>
    static readonly SlotSpec[] Slots =
    {
        new SlotSpec { node = "Slot_Armor",  icon = MenuSprites.Dir + "icon_armour_64.png",     type = UpgradeType.Armor },
        new SlotSpec { node = "Slot_Turret", icon = MenuSprites.Dir + "icon_turret_64.png",     type = UpgradeType.Turret },
        new SlotSpec { node = "Slot_Tracks", icon = MenuSprites.Dir + "icon_speed_64.png",      type = UpgradeType.Tracks },
        new SlotSpec { node = "Slot_Magnet", icon = MenuSprites.Dir + "icon_demolition_64.png", type = UpgradeType.Magnet },
    };

    [MenuItem("Tools/Tankeo/8 - Build Game HUD")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleScreenFonts.ArchivoAsset) == null ||
            AssetDatabase.LoadAssetAtPath<Material>(TitleScreenFonts.BungeeUnderlayMat) == null)
            TitleScreenFonts.Generate();

        HudSprites.Generate();
        // The upgrade slots draw from the shared menu icon set now.
        MenuSprites.Import();

        foreach (var path in Scenes) BuildScene(path);

        AssetDatabase.SaveAssets();
        Debug.Log("[HUD] Build complete in " + Scenes.Length + " scenes.");
    }

    static void BuildScene(string scenePath)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        RetireOldHud(scene);

        var canvas = EnsureCanvas(scene);
        var hud = EnsureComponent<HudController>(canvas.gameObject);

        var scrim = BuildScrim(canvas);
        var vignette = BuildVignette(canvas);
        var stats = BuildStatStack(canvas);
        var slots = BuildUpgradeRow(canvas);
        var hull = BuildHullCluster(canvas);
        var drift = BuildDriftCluster(canvas);

        scrim.SetSiblingIndex(0);
        vignette.transform.SetSiblingIndex(1);

        Wire(hud, stats, slots, hull, drift, vignette);
        WireDriftSource(canvas.gameObject);

        // The HUD is one canvas now, and it fades rather than disappearing on death,
        // so GameManager has nothing left to switch off.
        ClearDeathHides(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HUD] Rebuilt " + scenePath);
    }

    // =========================================================================
    // Canvas
    // =========================================================================

    static RectTransform EnsureCanvas(Scene scene)
    {
        var go = FindRoot(scene, CanvasName);
        if (go == null)
        {
            go = new GameObject(CanvasName, typeof(RectTransform));
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        }

        var canvas = EnsureComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;              // every shape here is a hard-edged rect
        canvas.overrideSorting = false;
        // Below the pause / upgrade / game-over screens on the main Canvas (0) and
        // the loading wipe (50), so they always cover it.
        canvas.sortingOrder = -1;

        var scaler = EnsureComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // Four corner clusters, so neither axis is the binding one: 0.5 keeps them in
        // their corners and leaves the most air in the middle at odd aspects.
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        var group = EnsureComponent<CanvasGroup>(go);
        group.interactable = false;
        group.blocksRaycasts = false;

        // No GraphicRaycaster on purpose - a read-only HUD must not eat WebGL clicks.
        Strip<GraphicRaycaster>(go);

        return (RectTransform)go.transform;
    }

    static RectTransform BuildScrim(RectTransform canvas)
    {
        var scrim = Ensure(canvas, "BottomScrim");
        scrim.anchorMin = new Vector2(0f, 0f);
        scrim.anchorMax = new Vector2(1f, 0f);
        scrim.pivot = new Vector2(0.5f, 0f);
        scrim.offsetMin = Vector2.zero;
        scrim.offsetMax = new Vector2(0f, ScrimHeight);
        scrim.localScale = Vector3.one;

        // The only "panel" in the HUD. Its own alpha ramp tops out at 1, so the tint's
        // alpha is what holds it to the 50% the spec allows.
        Paint(scrim, Hud + "hud_bottom_gradient.png", HudPalette.Scrim, Image.Type.Simple);
        return scrim;
    }

    static CanvasGroup BuildVignette(RectTransform canvas)
    {
        var vignette = Ensure(canvas, "LowHullVignette");
        Stretch(vignette);
        Paint(vignette, Hud + "hud_vignette.png", HudPalette.Alarm, Image.Type.Simple);

        var group = EnsureComponent<CanvasGroup>(vignette.gameObject);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    // =========================================================================
    // Timer and kills
    // =========================================================================

    struct StatRefs
    {
        public TextMeshProUGUI time, kills;
        public RectTransform killNumeral;
    }

    static StatRefs BuildStatStack(RectTransform canvas)
    {
        var stack = Ensure(canvas, "StatStack");
        Corner(stack, new Vector2(1f, 1f), new Vector2(StatX, StatY), new Vector2(StatW, StatH));

        // Two plates rather than a layout group: each sizes itself to its own text and
        // both grow to the left from the same edge, so stacking them is one offset.
        var time = Decal(stack, "TimePlate", TimePlateH, new Vector2(0f, 0f),
                         HudPalette.Ink, HudPalette.Cream, HudPalette.Shadow, new Vector2(5f, -5f),
                         new RectOffset(15, 15, 3, 5), 9f);

        var kills = Decal(stack, "KillPlate", KillPlateH, new Vector2(0f, -(TimePlateH + StatGap)),
                          HudPalette.Ink, HudPalette.At(HudPalette.Cream, 150), HudPalette.ShadowSoft,
                          new Vector2(4f, -4f), new RectOffset(15, 15, 2, 4), 9f);

        var refs = new StatRefs();
        Micro(time, "Label", "TIME", MicroSize, 2f);
        refs.time = Numeral(time, "Value", "00:00", TimeSize, HudPalette.Cream);
        refs.time.alignment = TextAlignmentOptions.Midline;
        // The plate is content-sized and grows to the left, so a proportional clock
        // would shuffle its edge on every tick. TMP 3.0 only exposes mono spacing
        // through a rich-text tag, which would mean rebuilding the tag every second -
        // reserving the width the clock can ever need does the same job for free.
        EnsureComponent<LayoutElement>(refs.time.gameObject).preferredWidth = ClockWidth;

        Micro(kills, "Label", "KILLS", MicroSize, 2f);
        refs.kills = Numeral(kills, "Value", "0", KillSize, HudPalette.Yellow);
        refs.killNumeral = refs.kills.rectTransform;

        return refs;
    }

    // =========================================================================
    // Upgrade row
    // =========================================================================

    static UpgradeSlot[] BuildUpgradeRow(RectTransform canvas)
    {
        var row = Ensure(canvas, "UpgradeRow");
        float width = SlotCount * SlotSize + (SlotCount - 1) * SlotGap;
        Corner(row, new Vector2(0f, 0f), new Vector2(RowX, RowY), new Vector2(width, SlotSize));

        var built = new UpgradeSlot[Slots.Length];
        for (int i = 0; i < Slots.Length; i++)
            built[i] = BuildSlot(row, Slots[i], i * (SlotSize + SlotGap));
        return built;
    }

    static UpgradeSlot BuildSlot(RectTransform row, SlotSpec spec, float x)
    {
        var slot = Ensure(row, spec.node);
        Corner(slot, new Vector2(0f, 0f), new Vector2(x, 0f), new Vector2(SlotSize, SlotSize));

        // Authored locked, which is how every slot starts a run - UpgradeSlot repaints
        // it on unlock, and the scene view then matches the first frame of play.
        var shadow = Paint(Ensure(slot, "Shadow"), Hud + "hud_slab_fill.png", HudPalette.Clear, Image.Type.Sliced);
        Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(4f, -4f);

        var body = Slab(slot, "Body", "hud_slab_fill.png", HudPalette.DeepInkLocked);
        var keyline = Slab(slot, "Keyline", "hud_slab_border.png", HudPalette.CreamFaint);

        var icon = Ensure(slot, "Icon");
        Centre(icon, new Vector2(0f, 5f), new Vector2(IconSize, IconSize));
        var iconImage = Paint(icon, spec.icon, HudPalette.Clear, Image.Type.Simple);

        var padlock = Ensure(slot, "Padlock");
        Centre(padlock, Vector2.zero, new Vector2(PadlockW, PadlockH));
        var padlockImage = Paint(padlock, Hud + "hud_padlock.png", HudPalette.CreamLock, Image.Type.Simple);

        var pips = new Image[SlotPipCount];
        float pitch = SlotPipW + SlotPipGap;
        float start = -(SlotPipCount - 1) * pitch * 0.5f;
        for (int i = 0; i < SlotPipCount; i++)
        {
            var pip = Ensure(slot, "Pip" + i);
            Centre(pip, new Vector2(start + i * pitch, -12f), new Vector2(SlotPipW, SlotPipH));
            pips[i] = Paint(pip, Shared + "ui_square.png", HudPalette.CreamPipEmpty, Image.Type.Simple);
        }

        var component = EnsureComponent<UpgradeSlot>(slot.gameObject);
        var so = new SerializedObject(component);
        so.FindProperty("type").enumValueIndex = (int)spec.type;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("keyline").objectReferenceValue = keyline;
        so.FindProperty("shadow").objectReferenceValue = shadow;
        so.FindProperty("icon").objectReferenceValue = iconImage;
        so.FindProperty("padlock").objectReferenceValue = padlockImage;
        var list = so.FindProperty("pips");
        list.arraySize = pips.Length;
        for (int i = 0; i < pips.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = pips[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        return component;
    }

    // =========================================================================
    // Hull, value, XP
    // =========================================================================

    struct HullRefs
    {
        public RectTransform fill, xpFill, levelChip;
        public Image stripe, keyline, labelChip, labelChipShadow, valueChip, valueChipShadow;
        public TextMeshProUGUI label, value, level;
        public float trackWidth, xpWidth;
    }

    static HullRefs BuildHullCluster(RectTransform canvas)
    {
        float height = LabelRowH + ClusterGap + BarH + ClusterGap + XpRowH;

        var cluster = Ensure(canvas, "HullCluster");
        Corner(cluster, new Vector2(0.5f, 0f), new Vector2(HullX, HullY), new Vector2(HullW, height));

        var refs = new HullRefs();
        BuildHullLabelRow(cluster, ref refs);
        BuildHullBar(cluster, ref refs);
        BuildXpRow(cluster, ref refs);
        return refs;
    }

    static void BuildHullLabelRow(RectTransform cluster, ref HullRefs refs)
    {
        var row = Ensure(cluster, "HullLabelRow");
        Corner(row, new Vector2(0f, 1f), Vector2.zero, new Vector2(HullW, LabelRowH));

        // Both readings sit in chips that are invisible until hull runs low. Keeping
        // the chip in the tree and only changing its colour means the low state is a
        // tint change, not a rebuild - and the offsets below cancel the chip padding
        // so the text does not shift when the chip appears.
        var label = Decal(row, "HullLabel", LabelRowH, new Vector2(-7f, 0f),
                          HudPalette.Clear, HudPalette.Clear, HudPalette.Clear, new Vector2(3f, -3f),
                          new RectOffset(7, 7, 2, 3), 0f);
        Anchor(label, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        refs.labelChip = label.Find("Body").GetComponent<Image>();
        refs.labelChipShadow = label.Find("Shadow").GetComponent<Image>();
        refs.label = Micro(label, "Text", "HULL", MicroSizeLarge, 2.5f, HudPalette.CreamLabel);

        var value = Decal(row, "HullValue", LabelRowH, new Vector2(6f, 0f),
                          HudPalette.Clear, HudPalette.Clear, HudPalette.Clear, new Vector2(3f, -3f),
                          new RectOffset(6, 6, 1, 2), 0f);
        Anchor(value, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
        refs.valueChip = value.Find("Body").GetComponent<Image>();
        refs.valueChipShadow = value.Find("Shadow").GetComponent<Image>();
        refs.value = Numeral(value, "Text", "100", HullValueSize, HudPalette.Cream);
    }

    static void BuildHullBar(RectTransform cluster, ref HullRefs refs)
    {
        var bar = Ensure(cluster, "HullBar");
        Corner(bar, new Vector2(0f, 1f), new Vector2(0f, -(LabelRowH + ClusterGap)), new Vector2(HullW, BarH));

        Slab(bar, "Body", "hud_slab_fill.png", HudPalette.DeepInk);

        var track = Ensure(bar, "Track");
        Stretch(track);
        track.offsetMin = new Vector2(TrackInsetX, TrackInsetY);
        track.offsetMax = new Vector2(-TrackInsetX, -TrackInsetY);

        var trough = Ensure(track, "Trough");
        Stretch(trough);
        Paint(trough, Hud + "hud_slab_fill.png", HudPalette.CreamTrough, Image.Type.Sliced);

        // Tiled rather than stretched, so shrinking the rect walks whole notches off
        // the leading edge - that is what makes a hit countable. The last notch is
        // clipped mid-stripe, which is also what a hard damage edge should look like.
        var fill = Ensure(track, "Fill");
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.localScale = Vector3.one;
        refs.trackWidth = HullW - TrackInsetX * 2f;
        fill.sizeDelta = new Vector2(refs.trackWidth, 0f);
        refs.stripe = Paint(fill, Hud + "hud_hull_stripe.png", HudPalette.Yellow, Image.Type.Tiled);
        refs.fill = fill;

        refs.keyline = Slab(bar, "Keyline", "hud_slab_border.png", HudPalette.Cream);
    }

    static void BuildXpRow(RectTransform cluster, ref HullRefs refs)
    {
        var row = Ensure(cluster, "XpRow");
        Corner(row, new Vector2(0f, 1f), new Vector2((HullW - XpRowW) * 0.5f, -(LabelRowH + BarH + ClusterGap * 2f)),
               new Vector2(XpRowW, XpRowH));

        // Fixed width rather than content-sized: LV 13 is wider than LV 1, and a chip
        // that grows would drag the hairline's start with it every level.
        var chip = Ensure(row, "LvChip");
        Corner(chip, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(LvChipW, XpRowH));

        var chipShadow = Paint(Ensure(chip, "Shadow"), Hud + "hud_slab_fill.png", HudPalette.Shadow, Image.Type.Sliced);
        Stretch(chipShadow.rectTransform);
        chipShadow.rectTransform.anchoredPosition = new Vector2(3f, -3f);
        Slab(chip, "Body", "hud_slab_fill.png", HudPalette.Terracotta);

        refs.level = Micro(chip, "Text", "LV 1", MicroSize, 1.5f, HudPalette.Cream);
        Stretch(refs.level.rectTransform);
        refs.level.alignment = TextAlignmentOptions.Center;
        refs.levelChip = chip;

        var hairline = Ensure(row, "Hairline");
        hairline.anchorMin = new Vector2(0f, 0.5f);
        hairline.anchorMax = new Vector2(1f, 0.5f);
        hairline.pivot = new Vector2(0.5f, 0.5f);
        hairline.offsetMin = new Vector2(LvChipW + HairlineGap, -HairlineH * 0.5f);
        hairline.offsetMax = new Vector2(0f, HairlineH * 0.5f);
        hairline.localScale = Vector3.one;

        var trough = Ensure(hairline, "Trough");
        Stretch(trough);
        Paint(trough, Shared + "ui_square.png", HudPalette.DeepInkChip, Image.Type.Simple);

        var fill = Ensure(hairline, "Fill");
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.localScale = Vector3.one;
        refs.xpWidth = XpRowW - LvChipW - HairlineGap;
        fill.sizeDelta = new Vector2(0f, 0f);
        // Cream, not yellow: yellow is hull's colour and nothing else may borrow it.
        Paint(fill, Shared + "ui_square.png", HudPalette.Cream, Image.Type.Simple);
        refs.xpFill = fill;
    }

    // =========================================================================
    // Drift
    // =========================================================================

    struct DriftRefs
    {
        public Image[] pips;
        public Image body, keyline, glow;
        public TextMeshProUGUI chevron, label;
    }

    static DriftRefs BuildDriftCluster(RectTransform canvas)
    {
        var cluster = Ensure(canvas, "DriftCluster");
        Corner(cluster, new Vector2(1f, 0f), new Vector2(DriftX, DriftY), new Vector2(DriftW, DriftH));

        var refs = new DriftRefs();

        var pipRow = Ensure(cluster, "MeterPips");
        float pipWidth = PipCount * PipW + (PipCount - 1) * PipGap;
        Corner(pipRow, new Vector2(1f, 0f), new Vector2(0f, DriftPlateH + 6f), new Vector2(pipWidth, PipH));

        refs.pips = new Image[PipCount];
        for (int i = 0; i < PipCount; i++)
        {
            var pip = Ensure(pipRow, "Pip" + i);
            Corner(pip, new Vector2(0f, 0f), new Vector2(i * (PipW + PipGap), 0f), new Vector2(PipW, PipH));
            refs.pips[i] = Paint(pip, Hud + "hud_pip.png", HudPalette.CreamPipEmpty, Image.Type.Simple);
        }

        var plate = Decal(cluster, "DriftPlate", DriftPlateH, Vector2.zero,
                          HudPalette.DeepInk, HudPalette.CreamDim, HudPalette.Shadow, new Vector2(4f, -4f),
                          new RectOffset(11, 11, 2, 3), 6f);
        Anchor(plate, new Vector2(1f, 0f), new Vector2(1f, 0f));

        refs.body = plate.Find("Body").GetComponent<Image>();
        refs.keyline = plate.Find("Keyline").GetComponent<Image>();

        // Inside the plate and stretched past its edges, so it tracks whatever width
        // the label ends up at.
        var glow = Ensure(plate, "Glow");
        Stretch(glow);
        glow.offsetMin = new Vector2(-24f, -19f);
        glow.offsetMax = new Vector2(24f, 19f);
        refs.glow = Paint(glow, Hud + "hud_glow.png", HudPalette.Clear, Image.Type.Simple);
        IgnoreLayout(glow);
        glow.SetSiblingIndex(0);

        refs.chevron = Numeral(plate, "Chevron", "»", ChevronSize, HudPalette.CreamDim);
        refs.label = Micro(plate, "Label", "DRIFT", MicroSize, 1.5f, HudPalette.CreamDim);
        return refs;
    }

    // =========================================================================
    // Wiring
    // =========================================================================

    static void Wire(HudController hud, StatRefs stats, UpgradeSlot[] slots, HullRefs hull,
                     DriftRefs drift, CanvasGroup vignette)
    {
        var so = new SerializedObject(hud);

        so.FindProperty("hullFill").objectReferenceValue = hull.fill;
        so.FindProperty("hullStripe").objectReferenceValue = hull.stripe;
        so.FindProperty("hullKeyline").objectReferenceValue = hull.keyline;
        so.FindProperty("hullValue").objectReferenceValue = hull.value;
        so.FindProperty("hullLabel").objectReferenceValue = hull.label;
        so.FindProperty("hullLabelChip").objectReferenceValue = hull.labelChip;
        so.FindProperty("hullLabelChipShadow").objectReferenceValue = hull.labelChipShadow;
        so.FindProperty("hullValueChip").objectReferenceValue = hull.valueChip;
        so.FindProperty("hullValueChipShadow").objectReferenceValue = hull.valueChipShadow;
        so.FindProperty("lowHullVignette").objectReferenceValue = vignette;
        so.FindProperty("hullTrackWidth").floatValue = hull.trackWidth;

        so.FindProperty("xpFill").objectReferenceValue = hull.xpFill;
        so.FindProperty("levelChip").objectReferenceValue = hull.levelChip;
        so.FindProperty("levelValue").objectReferenceValue = hull.level;
        so.FindProperty("xpTrackWidth").floatValue = hull.xpWidth;

        so.FindProperty("timeValue").objectReferenceValue = stats.time;
        so.FindProperty("killNumeral").objectReferenceValue = stats.killNumeral;
        so.FindProperty("killValue").objectReferenceValue = stats.kills;

        var slotList = so.FindProperty("slots");
        slotList.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            slotList.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        var pipList = so.FindProperty("driftPips");
        pipList.arraySize = drift.pips.Length;
        for (int i = 0; i < drift.pips.Length; i++)
            pipList.GetArrayElementAtIndex(i).objectReferenceValue = drift.pips[i];

        so.FindProperty("driftBody").objectReferenceValue = drift.body;
        so.FindProperty("driftKeyline").objectReferenceValue = drift.keyline;
        so.FindProperty("driftGlow").objectReferenceValue = drift.glow;
        so.FindProperty("driftChevron").objectReferenceValue = drift.chevron;
        so.FindProperty("driftLabel").objectReferenceValue = drift.label;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireDriftSource(GameObject canvasGo)
    {
        var meter = EnsureComponent<DriftMeter>(canvasGo);
        var tank = Object.FindObjectOfType<Tank.TankController>();
        var body = tank != null ? tank.GetComponent<Rigidbody>() : null;
        if (body == null) Debug.LogWarning("[HUD] No player Rigidbody found - the drift meter will stay empty.");

        var so = new SerializedObject(meter);
        so.FindProperty("player").objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =========================================================================
    // Retiring the old HUD
    // =========================================================================

    static void RetireOldHud(Scene scene)
    {
        // A disabled dev helper that blanked the four old containers for screenshots.
        // HudCanvas has a CanvasGroup, so hiding the HUD is one alpha now.
        var hider = FindRoot(scene, "HideUIElements");
        if (hider != null) Object.DestroyImmediate(hider);

        // The menu canvas is called "Canvas" in one scene and "Canvas (1)" in the
        // other two, so go by component rather than by name.
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == CanvasName || go.GetComponent<Canvas>() == null) continue;

            foreach (var name in Retired)
            {
                var node = go.transform.Find(name);
                if (node != null) Object.DestroyImmediate(node.gameObject);
            }
        }
    }

    static void ClearDeathHides(Scene scene)
    {
        var game = Object.FindObjectOfType<GameManager>();
        if (game == null) return;

        var so = new SerializedObject(game);
        so.FindProperty("uiElements").arraySize = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // =========================================================================
    // Building blocks
    // =========================================================================

    /// <summary>
    /// The shared HUD decal: skewed body, cream keyline, hard offset shadow, and a
    /// row of upright content that sizes the plate. Same three layers as a menu
    /// button, at HUD scale.
    /// </summary>
    static RectTransform Decal(RectTransform parent, string name, float height, Vector2 position,
                               Color body, Color keyline, Color shadow, Vector2 shadowOffset,
                               RectOffset padding, float spacing)
    {
        var decal = Ensure(parent, name);
        Corner(decal, new Vector2(1f, 1f), position, new Vector2(0f, height));

        var shadowRect = Paint(Ensure(decal, "Shadow"), Hud + "hud_slab_fill.png", shadow, Image.Type.Sliced);
        Stretch(shadowRect.rectTransform);
        shadowRect.rectTransform.anchoredPosition = shadowOffset;
        IgnoreLayout(shadowRect.rectTransform);

        var bodyRect = Slab(decal, "Body", "hud_slab_fill.png", body);
        IgnoreLayout(bodyRect.rectTransform);

        var keylineRect = Slab(decal, "Keyline", "hud_slab_border.png", keyline);
        IgnoreLayout(keylineRect.rectTransform);

        var group = EnsureComponent<HorizontalLayoutGroup>(decal.gameObject);
        group.padding = padding;
        group.spacing = spacing;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = true;
        group.childScaleWidth = false;
        group.childScaleHeight = false;

        // The fitter drives the width, and driven values are recomputed on enable
        // rather than serialized - the width in the scene file is meaningless.
        var fitter = EnsureComponent<ContentSizeFitter>(decal.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        return decal;
    }

    static Image Slab(RectTransform parent, string name, string sprite, Color tint)
    {
        var rt = Ensure(parent, name);
        Stretch(rt);
        var img = Paint(rt, Hud + sprite, tint, Image.Type.Sliced);
        // The border art carries its keyline in all four 9-slice edges, so the centre
        // cell is the only thing Fill Center would add - and it must stay off.
        img.fillCenter = sprite != "hud_slab_border.png";
        return img;
    }

    static TextMeshProUGUI Micro(RectTransform parent, string name, string content, float size, float trackingPx)
    {
        return Micro(parent, name, content, size, trackingPx, HudPalette.CreamDim);
    }

    static TextMeshProUGUI Micro(RectTransform parent, string name, string content, float size,
                                 float trackingPx, Color colour)
    {
        var text = Label(parent, name, content, size, colour,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        text.characterSpacing = Tracking(trackingPx, size);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    static TextMeshProUGUI Numeral(RectTransform parent, string name, string content, float size, Color colour)
    {
        var text = Label(parent, name, content, size, colour,
                         TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
        text.characterSpacing = 0f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    // =========================================================================
    // Helpers - same shape as the menu builders
    // =========================================================================

    static Image Paint(RectTransform rt, string spritePath, Color tint, Image.Type type)
    {
        var img = EnsureComponent<Image>(rt.gameObject);
        img.sprite = Sprite(spritePath);
        img.type = type;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = tint;
        img.raycastTarget = false;              // nothing in the HUD is clickable
        return img;
    }

    static void IgnoreLayout(RectTransform rt)
    {
        EnsureComponent<LayoutElement>(rt.gameObject).ignoreLayout = true;
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

    static void Strip<T>(GameObject go) where T : Component
    {
        foreach (var c in go.GetComponents<T>()) Object.DestroyImmediate(c, true);
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

    /// <summary>Anchors and pivots to one corner so an odd browser aspect can't drift the layout.</summary>
    static void Corner(RectTransform rt, Vector2 corner, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = corner;
        rt.anchorMax = corner;
        rt.pivot = corner;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    /// <summary>Re-anchors a rect built by Decal, whose corner is fixed at top-right.</summary>
    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot)
    {
        var pos = rt.anchoredPosition;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
    }

    static void Centre(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    static TextMeshProUGUI Label(Transform parent, string name, string content, float size, Color color,
                                 string fontPath, string materialPath)
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
        text.enableAutoSizing = false;          // the small sizes here are deliberate
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Sprite Sprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[HUD] Sprite not found (is it imported as Sprite?): " + path);
        return s;
    }
}
