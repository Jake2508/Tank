using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the L_MainMenu level-select screen to the "Left Rail" spec
/// (LEVEL_SELECT_2b_IMPLEMENTATION.md). Idempotent - run it as often as you like.
///
/// The spec is written in 1920x1080 canvas units; this project's canvas is
/// 960x540, so every number below is the spec value halved.
/// </summary>
public static class LevelSelectBuilder
{
    const string ScenePath = "Assets/Scenes/L_MainMenu.unity";
    const string Sprites = "Assets/UI/Level Select/";
    const string Shared = "Assets/UI/Shared/";
    const string MainMenu = "Assets/UI/Main Menu/";     // the back arrow reuses the menu chevron
    const string FadeInClipPath = "Assets/Animation/LevelSelectAnim.anim";

    // ---- palette -------------------------------------------------------------
    static readonly Color Cream = new Color32(244, 238, 220, 255);
    static readonly Color CreamDim = new Color32(244, 238, 220, 140);
    static readonly Color CreamMid = new Color32(244, 238, 220, 180);
    static readonly Color Ink = new Color32(31, 23, 18, 255);
    static readonly Color InkPlate = new Color32(31, 23, 18, 240);
    static readonly Color InkBack = new Color32(31, 23, 18, 230);
    static readonly Color InkBackHover = new Color32(44, 33, 26, 245);
    static readonly Color HardShadow = new Color32(21, 16, 11, 140);
    static readonly Color HintCream = new Color32(244, 238, 220, 150);

    // ---- layout, 960x540 units ----------------------------------------------
    const float LeftRail = 55f;                  // shared X with the title screen's menu column

    // The spec puts the heading on the same X as everything else and calls that the
    // wordmark's X, but the title doc actually places the wordmark at 92 (46 here),
    // and the two lockup PNGs carry different internal padding. Matching the numbers
    // left the SELECT ZONE slab 20px right of the TANKEO DRIFT slab at 1920, which is
    // a visible jump on the cross-fade. This is the X that makes the two line up.
    const float HeadingX = 45f;
    const float HeadingY = -39.5f, HeadingW = 326f, HeadingH = 114.5f;
    const float HeadingTilt = -2f;

    const float RowY = -199f, RowW = 858f, RowH = 210f;
    const float CardW = 271f, CardGap = 22.5f;
    const float ThumbH = 159.5f;
    const float ThumbShadowOffset = 9.5f;
    const float BadgeSize = 39.5f, BadgeOverhang = 2.5f;

    const float PlateH = 35f, PlateX = -5f, PlateY = -12f;
    const float PlateShadowOffset = 7f;
    const float PlatePadL = 15.5f, PlatePadR = 15.5f, PlatePadT = 7f, PlatePadB = 9f;
    const float PlateSpacing = 8.5f;
    const float NameSize = 19.5f, NameSpacing = 2f;
    const float TierSize = 10.5f, TierSpacing = 5f;
    const float BadgeLabelSize = 17f;

    const float BackW = 118f, BackH = 39f, BackY = 34.5f;
    const float BackLabelSize = 19.5f, BackLabelInset = 26f;
    const float BackArrowX = 13f, BackArrowW = 8f, BackArrowH = 11f;
    const float HintX = 186f, HintY = 48f, HintSize = 10.5f, HintSpacing = 10f;

    /// <summary>
    /// button_slab_fill/border are authored at 600 px/unit, so they render at their
    /// native 55-unit height with the 7 degree lean intact. Scaling the 9-slice by
    /// nativeHeight/actualHeight keeps that lean at any other height - without it a
    /// short slab leans far harder than the menu column does.
    /// </summary>
    const float SlabNativeHeight = 55f;

    /// <summary>
    /// card_frame is authored at 600 px/unit with a 27 px stroke inside a 60 px
    /// 9-slice border, so a multiplier of 1 draws the 4.5-unit keyline the spec asks
    /// for. The badge wants a 3.5-unit keyline, hence 27 / (6 * 3.5).
    /// </summary>
    const float ThumbKeylineMultiplier = 1f;
    const float BadgeKeylineMultiplier = 27f / (6f * 3.5f);

    struct CardSpec
    {
        public string node, photo, name, tier, numeral;
        public int buildIndex;
    }

    static readonly CardSpec[] Cards =
    {
        new CardSpec { node = "Card_Woodlands", photo = "level_woodlands.png", name = "WOODLANDS",   tier = "EASY",   numeral = "I",   buildIndex = 2 },
        new CardSpec { node = "Card_Desert",    photo = "level_desert.png",    name = "DESERT",      tier = "MEDIUM", numeral = "II",  buildIndex = 3 },
        new CardSpec { node = "Card_Snow",      photo = "level_snow.png",      name = "SNOWY LANDS", tier = "HARD",   numeral = "III", buildIndex = 4 },
    };

    [MenuItem("Tools/Tankeo/4 - Build Level Select")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleScreenFonts.ArchivoAsset) == null)
            TitleScreenFonts.Generate();
        TitleScreenFonts.TopUp();          // the input hint's middle dot is outside the baked ASCII range

        LevelSelectSprites.Import();

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = FindRoot(scene, "Canvas");
        if (canvasGo == null) { Debug.LogError("[LevelSelect] No 'Canvas' in " + ScenePath); return; }

        var root = canvasGo.transform.Find("LevelSelectUI") as RectTransform;
        if (root == null) { Debug.LogError("[LevelSelect] No 'LevelSelectUI' under Canvas."); return; }

        Stretch(root);
        root.gameObject.SetActive(true);
        ClearChildren(root);                       // the old heading, card grid and back button

        BuildScrim(root);
        var content = BuildContent(root);
        BuildHeading(content);
        var cards = BuildCardRow(content);
        var back = BuildBackButton(root);
        BuildHint(root);

        WireScreen(root.gameObject, cards, back);
        RebuildFadeInClip();
        MenuAnimators.FixTriggerTransitions(MenuAnimators.LevelSelectController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[LevelSelect] Build complete.");
    }

    // =========================================================================
    // Screen furniture
    // =========================================================================

    static void BuildScrim(RectTransform root)
    {
        var scrim = Ensure(root, "Scrim");
        Stretch(scrim);
        scrim.SetSiblingIndex(0);

        var img = EnsureComponent<Image>(scrim.gameObject);
        img.sprite = Sprite(Sprites + "levelselect_scrim.png");
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    /// <summary>
    /// Heading + cards live under one rect so they can shrink together on a canvas
    /// too narrow for the 858-unit row. The back button and hint stay outside it,
    /// pinned to the bottom-left corner where they can never collide.
    /// </summary>
    static RectTransform BuildContent(RectTransform root)
    {
        var content = Ensure(root, "Content");
        Corner(content, new Vector2(0f, 1f), Vector2.zero, new Vector2(960f, 540f));

        var fit = EnsureComponent<ScaleToFitWidth>(content.gameObject);
        var so = new SerializedObject(fit);
        so.FindProperty("contentRight").floatValue = LeftRail + RowW;
        so.FindProperty("rightMargin").floatValue = 20f;
        so.FindProperty("minScale").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        return content;
    }

    static void BuildHeading(RectTransform content)
    {
        // The tilt lives on a child so the intro slide can drive the parent's
        // anchoredPosition without also having to preserve a rotation.
        var heading = Ensure(content, "ZoneHeading");
        Corner(heading, new Vector2(0f, 1f), new Vector2(HeadingX, HeadingY), new Vector2(HeadingW, HeadingH));
        heading.localRotation = Quaternion.identity;
        Strip<Image>(heading.gameObject);
        Strip<CanvasRenderer>(heading.gameObject);

        var lockup = Ensure(heading, "Lockup");
        Stretch(lockup);
        lockup.localRotation = Quaternion.Euler(0f, 0f, HeadingTilt);

        var img = EnsureComponent<Image>(lockup.gameObject);
        img.sprite = Sprite(Sprites + "zone_heading_lockup.png");
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = false;

        EnsureComponent<UIIntroSlide>(heading.gameObject);
    }

    static LevelCard[] BuildCardRow(RectTransform content)
    {
        var row = Ensure(content, "CardRow");
        Corner(row, new Vector2(0f, 1f), new Vector2(LeftRail, RowY), new Vector2(RowW, RowH));

        // No HorizontalLayoutGroup: the row is three fixed cards and each card
        // animates its own anchoredPosition on hover, which a live layout group
        // would fight. Same reason the title-screen column is hand-placed.
        var built = new LevelCard[Cards.Length];
        for (int i = 0; i < Cards.Length; i++)
            built[i] = BuildCard(row, Cards[i], i * (CardW + CardGap));

        return built;
    }

    // =========================================================================
    // One card
    // =========================================================================

    static LevelCard BuildCard(RectTransform row, CardSpec spec, float x)
    {
        var card = Ensure(row, spec.node);
        Corner(card, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(CardW, RowH));

        // Transparent but raycastable, so the whole card is a hover/click target.
        var hit = EnsureComponent<Image>(card.gameObject);
        hit.sprite = null;
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        EnsureComponent<CanvasGroup>(card.gameObject);   // driven by the entrance stagger

        var thumb = Ensure(card, "Thumb");
        Corner(thumb, new Vector2(0f, 1f), Vector2.zero, new Vector2(CardW, ThumbH));

        var thumbShadow = Fill(thumb, "Shadow", Shared + "ui_square.png", HardShadow);
        thumbShadow.anchoredPosition = new Vector2(ThumbShadowOffset, -ThumbShadowOffset);

        var photo = Fill(thumb, "Photo", Sprites + spec.photo, Color.white);
        photo.GetComponent<Image>().preserveAspect = false;   // art is already cropped to the card

        var thumbKeyline = Keyline(thumb, "Keyline", CreamDim, ThumbKeylineMultiplier);
        var badgeKeyline = BuildBadge(thumb, spec.numeral);
        var plateBody = BuildNamePlate(card, spec, out TextMeshProUGUI tierWord);

        var levelCard = EnsureComponent<LevelCard>(card.gameObject);
        var so = new SerializedObject(levelCard);
        so.FindProperty("buildIndex").intValue = spec.buildIndex;
        so.FindProperty("thumbKeyline").objectReferenceValue = thumbKeyline;
        so.FindProperty("plateBody").objectReferenceValue = plateBody;
        so.FindProperty("badgeKeyline").objectReferenceValue = badgeKeyline;
        so.FindProperty("tierWord").objectReferenceValue = tierWord;
        so.ApplyModifiedPropertiesWithoutUndo();

        return levelCard;
    }

    static Image BuildBadge(RectTransform thumb, string numeral)
    {
        var badge = Ensure(thumb, "TierBadge");
        badge.anchorMin = new Vector2(1f, 1f);
        badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(1f, 1f);
        badge.anchoredPosition = new Vector2(BadgeOverhang, BadgeOverhang);   // overhangs the thumb corner
        badge.sizeDelta = new Vector2(BadgeSize, BadgeSize);
        badge.localScale = Vector3.one;

        Fill(badge, "BadgeBg", Shared + "ui_square.png", Ink);
        var keyline = Keyline(badge, "BadgeKeyline", CreamMid, BadgeKeylineMultiplier);

        var label = Label(badge, "BadgeLabel", numeral, BadgeLabelSize, Cream, TitleScreenFonts.BungeeAsset, null);
        Stretch(label.rectTransform);
        label.characterSpacing = 0f;
        label.alignment = TextAlignmentOptions.Center;

        return keyline;
    }

    /// <summary>
    /// The plate sizes itself to its labels, so a longer level name just makes a
    /// wider slab. Body/keyline/shadow opt out of the layout and stretch to whatever
    /// width the fitter lands on.
    /// </summary>
    static Image BuildNamePlate(RectTransform card, CardSpec spec, out TextMeshProUGUI tierWord)
    {
        var plate = Ensure(card, "NamePlate");
        plate.anchorMin = new Vector2(0f, 1f);
        plate.anchorMax = new Vector2(0f, 1f);
        plate.pivot = new Vector2(0f, 0f);
        plate.anchoredPosition = new Vector2(PlateX, -ThumbH + PlateY);   // overlaps the photo
        plate.sizeDelta = new Vector2(plate.sizeDelta.x, PlateH);
        plate.localScale = Vector3.one;

        float slabMultiplier = SlabNativeHeight / PlateH;

        var shadow = Fill(plate, "PlateShadow", Shared + "button_slab_fill.png", HardShadow);
        SliceSlab(shadow, slabMultiplier, fillCenter: true);
        shadow.anchoredPosition = new Vector2(PlateShadowOffset, -PlateShadowOffset);
        IgnoreLayout(shadow);

        var body = Fill(plate, "PlateBody", Shared + "button_slab_fill.png", InkPlate);
        SliceSlab(body, slabMultiplier, fillCenter: true);
        IgnoreLayout(body);

        var keyline = Fill(plate, "PlateKeyline", Shared + "button_slab_border.png", Cream);
        SliceSlab(keyline, slabMultiplier, fillCenter: true);
        IgnoreLayout(keyline);

        var group = EnsureComponent<HorizontalLayoutGroup>(plate.gameObject);
        group.padding = new RectOffset((int)PlatePadL, (int)PlatePadR, (int)PlatePadT, (int)PlatePadB);
        group.spacing = PlateSpacing;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = true;
        group.childScaleWidth = false;
        group.childScaleHeight = false;

        // The fitter drives the plate's width, and driven RectTransform values are
        // recomputed on enable rather than serialized - so the width stored in the
        // scene file is meaningless and only the runtime value matters.
        var fitter = EnsureComponent<ContentSizeFitter>(plate.gameObject);
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Both labels share a rect height and use baseline alignment, which is what
        // actually lands the tier word on the level name's baseline.
        var name = Label(plate, "LevelName", spec.name, NameSize, Cream,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        name.characterSpacing = NameSpacing;
        name.alignment = TextAlignmentOptions.BaselineLeft;

        tierWord = Label(plate, "TierWord", spec.tier, TierSize, CreamDim,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        tierWord.characterSpacing = TierSpacing;
        tierWord.alignment = TextAlignmentOptions.BaselineLeft;

        name.transform.SetSiblingIndex(3);
        tierWord.transform.SetSiblingIndex(4);

        return body.GetComponent<Image>();
    }

    // =========================================================================
    // Back button + hint
    // =========================================================================

    static MenuSlab BuildBackButton(RectTransform root)
    {
        var back = Ensure(root, "BackButton");
        Corner(back, new Vector2(0f, 0f), new Vector2(LeftRail, BackY), new Vector2(BackW, BackH));

        Strip<Shadow>(back.gameObject);        // also removes Outline, which derives from it
        Strip<Image>(back.gameObject);
        Strip<CanvasRenderer>(back.gameObject);
        ClearChildren(back);

        float slabMultiplier = SlabNativeHeight / BackH;

        var shadow = Fill(back, "Shadow", Shared + "button_slab_fill.png", HardShadow);
        SliceSlab(shadow, slabMultiplier, fillCenter: true);
        shadow.anchoredPosition = new Vector2(PlateShadowOffset, -PlateShadowOffset);

        var body = Fill(back, "Body", Shared + "button_slab_fill.png", InkBack);
        SliceSlab(body, slabMultiplier, fillCenter: true);
        body.GetComponent<Image>().raycastTarget = true;

        var keyline = Fill(back, "Keyline", Shared + "button_slab_border.png", Cream);
        SliceSlab(keyline, slabMultiplier, fillCenter: true);

        // The spec sets this label as "◂ BACK", but Archivo Black has no U+25C2 at
        // any weight, so a text arrow can only ever render as a missing glyph. The
        // menu's own selection chevron, mirrored and tinted cream, is the same shape
        // and is already part of the design language.
        var arrow = Ensure(back, "BackArrow");
        arrow.anchorMin = new Vector2(0f, 0.5f);
        arrow.anchorMax = new Vector2(0f, 0.5f);
        arrow.pivot = new Vector2(0.5f, 0.5f);
        arrow.anchoredPosition = new Vector2(BackArrowX + BackArrowW * 0.5f, 0f);
        arrow.sizeDelta = new Vector2(BackArrowW, BackArrowH);
        arrow.localScale = Vector3.one;
        // The chevron art points right; a half turn about its centre points it left
        // without moving it, which mirroring via a negative scale would.
        arrow.localRotation = Quaternion.Euler(0f, 0f, 180f);

        var arrowImg = EnsureComponent<Image>(arrow.gameObject);
        arrowImg.sprite = Sprite(MainMenu + "chevron_select.png");
        arrowImg.type = Image.Type.Simple;
        arrowImg.color = Cream;
        arrowImg.raycastTarget = false;

        var label = Label(back, "Label", "BACK", BackLabelSize, Cream,
                          TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(BackLabelInset, 0f);
        label.rectTransform.offsetMax = new Vector2(-10f, 0f);
        label.characterSpacing = 4f;
        label.alignment = TextAlignmentOptions.Left;

        var button = EnsureComponent<Button>(back.gameObject);
        button.transition = Selectable.Transition.None;      // MenuSlab drives the visuals
        button.targetGraphic = body.GetComponent<Image>();
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();    // LevelSelectUI adds its listener at runtime

        var slab = EnsureComponent<MenuSlab>(back.gameObject);
        var so = new SerializedObject(slab);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("shadow").objectReferenceValue = shadow;
        so.FindProperty("body").objectReferenceValue = body.GetComponent<Image>();
        so.FindProperty("chevron").objectReferenceValue = null;
        so.FindProperty("idleTint").colorValue = InkBack;
        so.FindProperty("hoverTint").colorValue = InkBackHover;
        // Not part of a nav column - nothing else would ever hand it the highlight.
        so.FindProperty("selfHover").boolValue = true;
        so.FindProperty("hoverOffset").vector2Value = new Vector2(5f, 2.5f);
        so.FindProperty("pressOffset").vector2Value = new Vector2(5f, -1.5f);
        so.FindProperty("shadowIdle").vector2Value = new Vector2(7f, -7f);
        so.FindProperty("shadowHover").vector2Value = new Vector2(10.5f, -11f);
        so.FindProperty("shadowPress").vector2Value = new Vector2(3.5f, -3.5f);
        so.ApplyModifiedPropertiesWithoutUndo();

        return slab;
    }

    static void BuildHint(RectTransform root)
    {
        var text = Label(root, "InputHint", "A / D · MOVE    ENTER · DEPLOY    ESC · BACK",
                         HintSize, HintCream, TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        Corner(text.rectTransform, new Vector2(0f, 0f), new Vector2(HintX, HintY), new Vector2(500f, 20f));
        text.characterSpacing = HintSpacing;
        text.alignment = TextAlignmentOptions.BottomLeft;
    }

    // =========================================================================
    // Wiring
    // =========================================================================

    static void WireScreen(GameObject rootGo, LevelCard[] cards, MenuSlab back)
    {
        var nav = EnsureComponent<LevelSelectNav>(rootGo);
        var navSo = new SerializedObject(nav);
        var list = navSo.FindProperty("cards");
        list.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        navSo.ApplyModifiedPropertiesWithoutUndo();

        var ui = rootGo.GetComponent<LevelSelectUI>();
        if (ui == null) { Debug.LogError("[LevelSelect] LevelSelectUI missing from " + rootGo.name); return; }

        var uiSo = new SerializedObject(ui);
        var uiCards = uiSo.FindProperty("cards");
        uiCards.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
            uiCards.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        uiSo.FindProperty("backButton").objectReferenceValue = back.GetComponent<Button>();

        // levelLoader is an existing scene reference; only fill it if it came unset.
        var loaderProp = uiSo.FindProperty("levelLoader");
        if (loaderProp.objectReferenceValue == null)
        {
            var loader = Object.FindObjectOfType<LevelLoaderNew>();
            if (loader != null) loaderProp.objectReferenceValue = loader;
            else Debug.LogWarning("[LevelSelect] No LevelLoaderNew in the scene - cards will fall back to SceneManager.");
        }
        uiSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// The old fade-in clip animated the level buttons by name, and those objects are
    /// gone. Reduce it to the CanvasGroup cross-fade the spec asks for; the heading
    /// slide and card stagger are script-driven now.
    /// </summary>
    static void RebuildFadeInClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FadeInClipPath);
        if (clip == null) { Debug.LogWarning("[LevelSelect] " + FadeInClipPath + " not found."); return; }

        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            AnimationUtility.SetEditorCurve(clip, binding, null);
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("", typeof(CanvasGroup), "m_Alpha"),
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f)));

        EditorUtility.SetDirty(clip);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    static RectTransform Fill(RectTransform parent, string name, string spritePath, Color tint)
    {
        var rt = Ensure(parent, name);
        Stretch(rt);
        var img = EnsureComponent<Image>(rt.gameObject);
        img.sprite = Sprite(spritePath);
        img.type = Image.Type.Simple;
        img.color = tint;
        img.raycastTarget = false;
        return rt;
    }

    static Image Keyline(RectTransform parent, string name, Color tint, float multiplier)
    {
        var rt = Fill(parent, name, Sprites + "card_frame.png", tint);
        var img = rt.GetComponent<Image>();
        img.type = Image.Type.Sliced;
        img.fillCenter = false;
        img.pixelsPerUnitMultiplier = multiplier;
        return img;
    }

    static void SliceSlab(RectTransform rt, float multiplier, bool fillCenter)
    {
        var img = rt.GetComponent<Image>();
        img.type = Image.Type.Sliced;
        // The slab border art has zero top/bottom slice, so its horizontal keylines
        // live in the centre cell - Fill Center has to stay on or they vanish.
        img.fillCenter = fillCenter;
        img.pixelsPerUnitMultiplier = multiplier;
    }

    static void IgnoreLayout(RectTransform rt)
    {
        var element = EnsureComponent<LayoutElement>(rt.gameObject);
        element.ignoreLayout = true;
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

    static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
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
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Sprite Sprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[LevelSelect] Sprite not found (is it imported as Sprite?): " + path);
        return s;
    }
}
