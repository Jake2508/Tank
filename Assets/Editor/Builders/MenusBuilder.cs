using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the upgrade, pause and end-of-run screens to MENUS_IMPLEMENTATION.md in
/// all three play scenes. Idempotent - run it as often as you like.
///
/// This edits the scenes in place: the old menu roots on the shared Canvas are removed
/// and replaced by one MenuCanvas, but everything else in the scene - the terrain, the
/// player, the loading wipe, HudCanvas - keeps its existing fileIDs and is not touched.
///
/// The spec is written in 960x540 units already, so every number below is a literal
/// canvas value with no scaling factor.
/// </summary>
public static class MenusBuilder
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/L_Woodlands.unity",
        "Assets/Scenes/L_Desert.unity",
        "Assets/Scenes/L_Snowy.unity",
    };

    const string Menus = MenuSprites.Dir;
    const string Shared = "Assets/UI/Shared/";
    const string Hud = "Assets/UI/HUD/";

    /// <summary>
    /// The full-screen dims are a flat white square stretched out, not menu_scrim.png.
    /// That file is the title screen's authored vignette - alpha 157 at the corners but
    /// 23 in the middle - so tinting it A184 dims the edges and leaves the centre of the
    /// screen almost untouched. These three menus need an even wash at an exact alpha.
    /// </summary>
    const string Dim = Shared + "ui_square.png";

    const string CanvasName = "MenuCanvas";

    /// <summary>
    /// Above HudCanvas (-1) and the shared Canvas (0), below the loading wipe (50) so a
    /// scene change still covers everything.
    /// </summary>
    const int SortOrder = 10;

    /// <summary>
    /// The slab art is authored 55 units tall with its 7 degree lean. Scaling the
    /// 9-slice by nativeHeight/actualHeight keeps that lean constant at any other
    /// height - without it a 20px tier chip would shear far harder than a 52px button.
    /// </summary>
    const float SlabNativeHeight = 55f;

    // =========================================================================
    // Layout - upgrade screen, section 3
    // =========================================================================

    static readonly float[] CardX = { -321f, -107f, 107f, 321f };
    const float CardW = 234f, CardH = 306f;

    /// <summary>
    /// The sprite is 234 wide because the parallelogram leans +-19px; the visible
    /// column is 196. Footer padding is measured from the visible edge, which is what
    /// puts every upright child inside the middle 164.
    /// </summary>
    const float CardVisibleW = 196f;
    const float CardContentW = 164f;

    const float BannerY = -34f, SubtitleY = -124f, CardY = -138f, UpHintY = 34f;
    const float BannerPadX = 30f, BannerPadTop = 7f, BannerPadBottom = 9f;
    const float BannerShadow = 7f;

    const float CardHeadH = 44f, CardRuleH = 3f, CardShadow = 6f;
    const float PlateSize = 64f, PlateTop = 62f, PlateStroke = 3f, CardIconSize = 38f;
    const float DescGap = 15f, DescLine = 20f;
    const float FooterBottom = 14f, FooterPad = 16f;
    const float ChipW = 26f, ChipH = 20f, ChipGap = 4f, KeyChip = 20f;

    // =========================================================================
    // Layout - pause, section 4
    // =========================================================================

    const float BandX = -60f, BandW = 480f, BandH = 600f;
    const float ContentX = 66f, ContentInset = 56f;

    /// <summary>
    /// The spec asks for 262, but the delivered band slants harder than that assumes:
    /// pause_band.png is opaque to x=426 at its top row and only x=353 at its bottom,
    /// which puts the band's right edge at screen x~303 level with the lowest button.
    /// A 262 column starting at +66 would run 25px out past the slant and float on the
    /// terrain. 226 keeps the bottom button inside the band with room to spare, and
    /// still fits "RESTART RUN" at Archivo Black 16 with its 22px left padding.
    /// </summary>
    const float ContentW = 226f;

    const float TitleY = 0f, PauseRuleY = -52f, PauseRuleW = 120f, PauseRuleH = 5f;
    const float StatTop = 87f, StatRowH = 30f, StatRowGap = 12f;
    const float PauseButtonH = 52f, PauseButtonGap = 11f, PauseLabelPad = 22f;
    const float PauseHintH = 12f, PauseHintGap = 6f;

    // =========================================================================
    // Layout - end of run, section 5
    // =========================================================================

    const float VerdictY = -128f, EndSubtitleY = -254f;
    const float StripY = -306f, StripInset = 150f, StripH = 100f;
    const float StripRuleH = 3f, StripPad = 22f;
    const float ValueH = 48f, StripLabelGap = 5f, StripLabelH = 12f;

    const float EndButtonY = 56f, EndButtonW = 168f, EndButtonH = 46f, EndButtonGap = 12f;
    const float EndHintY = 26f;

    const float PipW = 14f, PipH = 6f, PipGap = 3f, PipColumnGap = 7f;
    const float VerdictAngle = -7f;

    // =========================================================================
    // Type
    // =========================================================================

    /// <summary>
    /// TMP character spacing is in font design units, so a tracking figure in canvas
    /// pixels has to be scaled by the atlas sampling size to survive a size change.
    /// </summary>
    static float Tracking(float pixels, float fontSize)
    {
        return pixels * TitleScreenFonts.SamplingPointSize / fontSize;
    }

    [MenuItem("Tools/Tankeo/11 - Build Menus")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleScreenFonts.SairaAsset) == null)
            TitleScreenFonts.Generate();

        MenuSprites.Import();

        foreach (var path in Scenes) BuildScene(path);

        AssetDatabase.SaveAssets();
        Debug.Log("[Menus] Build complete in " + Scenes.Length + " scenes.");
    }

    static void BuildScene(string scenePath)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Harvested before the old screens are deleted - they were doing real work
        // alongside their layout.
        GameObject volume = null, explosion = null;
        RetireOldMenus(scene, ref volume, ref explosion);

        var canvas = EnsureCanvas(scene);

        var controller = EnsureComponent<MenuController>(canvas.gameObject);
        var model = EnsureComponent<UpgradeModel>(canvas.gameObject);
        var effects = EnsureComponent<RunEffects>(canvas.gameObject);
        effects.EditorBind(volume ? volume : FindVolume(scene), explosion);

        var upgrade = BuildUpgradeScreen(canvas);
        var pause = BuildPauseScreen(canvas);
        var end = BuildEndScreen(canvas);

        Bind(controller, "upgradeScreen", upgrade);
        Bind(controller, "pauseScreen", pause);
        Bind(controller, "endScreen", end);
        Bind(controller, "upgrades", model);
        Bind(controller, "levelLoader", Object.FindObjectOfType<LevelLoaderNew>());

        Validate(canvas, scene.name);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Menus] Rebuilt " + scenePath);
    }

    // =========================================================================
    // Validation
    // =========================================================================

    /// <summary>
    /// Walks every serialized object reference on the menu components and reports the
    /// empty ones. A missed Bind does not throw - it leaves a null that quietly does
    /// nothing at runtime, which a layout capture cannot show. This is the check that
    /// says the screens are wired, not just that they look right.
    /// </summary>
    static void Validate(RectTransform canvas, string sceneName)
    {
        int missing = 0;

        foreach (var behaviour in canvas.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;

            // Only the components this builder owns; MenuFocusItem covers the buttons
            // and the cards.
            bool mine = behaviour is MenuController || behaviour is UpgradeScreen ||
                        behaviour is PauseScreen || behaviour is EndScreen ||
                        behaviour is MenuFocusGroup || behaviour is MenuFocusItem ||
                        behaviour is RunEffects;
            if (!mine) continue;

            var so = new SerializedObject(behaviour);
            var prop = so.GetIterator();

            while (prop.NextVisible(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (prop.objectReferenceValue != null) continue;

                Debug.LogError("[Menus] Unbound: " + sceneName + " / " + behaviour.GetType().Name +
                               " (" + behaviour.name + ") . " + prop.propertyPath);
                missing++;
            }
        }

        if (missing == 0)
            Debug.Log("[Menus] " + sceneName + ": all menu references bound.");
    }

    // =========================================================================
    // Retiring the old screens
    // =========================================================================

    /// <summary>
    /// Deletes the four old menu roots. GameOverUI and WinnerUI carried the
    /// colour-grading swing and the death explosion as well as their layout, so those
    /// two references are lifted out first and re-hosted on RunEffects.
    /// </summary>
    static void RetireOldMenus(Scene scene, ref GameObject volume, ref GameObject explosion)
    {
        var doomed = new List<GameObject>();

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;

                string type = behaviour.GetType().Name;
                switch (type)
                {
                    case "GameOverUI":
                    case "WinnerUI":
                        Harvest(behaviour, ref volume, ref explosion);
                        doomed.Add(behaviour.gameObject);
                        break;
                    case "UpgradeUI":
                    case "PauseMenuUI":
                        doomed.Add(behaviour.gameObject);
                        break;
                }
            }
        }

        foreach (var go in doomed)
        {
            if (go == null) continue;
            Debug.Log("[Menus] Retiring " + go.name + " in " + scene.name);
            Object.DestroyImmediate(go);
        }
    }

    static void Harvest(MonoBehaviour behaviour, ref GameObject volume, ref GameObject explosion)
    {
        var so = new SerializedObject(behaviour);

        var v = so.FindProperty("globalVolumeGO");
        if (v != null && v.objectReferenceValue != null && volume == null)
            volume = v.objectReferenceValue as GameObject;

        var e = so.FindProperty("characterExplosion");
        if (e != null && e.objectReferenceValue != null && explosion == null)
            explosion = e.objectReferenceValue as GameObject;
    }

    static GameObject FindVolume(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var volume = root.GetComponentInChildren<UnityEngine.Rendering.Volume>(true);
            if (volume != null && volume.isGlobal) return volume.gameObject;
        }
        return null;
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
        canvas.pixelPerfect = true;
        canvas.overrideSorting = false;
        canvas.sortingOrder = SortOrder;

        var scaler = EnsureComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        scaler.referencePixelsPerUnit = 100f;

        // Unlike the HUD, these screens are clickable, so the raycaster stays.
        EnsureComponent<GraphicRaycaster>(go);
        EnsureEventSystem(scene);

        return (RectTransform)go.transform;
    }

    /// <summary>Keyboard focus needs an EventSystem; a play scene may not have one.</summary>
    static void EnsureEventSystem(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null) return;

        var go = new GameObject("EventSystem",
                                typeof(UnityEngine.EventSystems.EventSystem),
                                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
    }

    // =========================================================================
    // Upgrade screen
    // =========================================================================

    static UpgradeScreen BuildUpgradeScreen(RectTransform canvas)
    {
        var root = Ensure(canvas, "UpgradeScreen");
        Stretch(root);
        var group = EnsureComponent<CanvasGroup>(root.gameObject);
        var screen = EnsureComponent<UpgradeScreen>(root.gameObject);
        var focus = EnsureComponent<MenuFocusGroup>(root.gameObject);

        var scrim = Ensure(root, "Scrim");
        Stretch(scrim);
        Paint(scrim, Dim, MenuPalette.ScrimUpgrade, Image.Type.Simple);

        // ---- banner ----------------------------------------------------------
        var banner = Ensure(root, "Banner");
        var bannerGroup = EnsureComponent<CanvasGroup>(banner.gameObject);

        var bannerLabel = Label(banner, "Label", "LEVEL UP", 34f, MenuPalette.Cream,
                                TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
        bannerLabel.characterSpacing = Tracking(2f, 34f);
        bannerLabel.alignment = TextAlignmentOptions.Center;

        var bannerSize = Measure(bannerLabel) +
                         new Vector2(BannerPadX * 2f, BannerPadTop + BannerPadBottom);

        Corner(banner, new Vector2(0.5f, 1f), new Vector2(0f, BannerY), bannerSize);
        banner.pivot = new Vector2(0.5f, 1f);

        var bannerShadow = Ensure(banner, "Shadow");
        Centre(bannerShadow, new Vector2(BannerShadow, -BannerShadow), bannerSize);
        Slab(bannerShadow, Shared + "button_slab_fill.png", MenuPalette.Shadow, bannerSize.y, true, false);

        var bannerFill = Ensure(banner, "Fill");
        Centre(bannerFill, Vector2.zero, bannerSize);
        Slab(bannerFill, Shared + "button_slab_fill.png", MenuPalette.Terracotta, bannerSize.y, true, false);

        var bannerBorder = Ensure(banner, "Border");
        Centre(bannerBorder, Vector2.zero, bannerSize);
        Slab(bannerBorder, Shared + "button_slab_border.png", MenuPalette.Cream, bannerSize.y, true, false);

        Centre((RectTransform)bannerLabel.transform, new Vector2(0f, -(BannerPadTop - BannerPadBottom) * 0.5f), bannerSize);
        bannerLabel.transform.SetAsLastSibling();

        // ---- subtitle and hint ------------------------------------------------
        var subtitle = Label(root, "Subtitle", "CHOOSE ONE UPGRADE", 11f, MenuPalette.Subtitle,
                             TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        subtitle.characterSpacing = Tracking(3f, 11f);
        subtitle.alignment = TextAlignmentOptions.Center;
        // Bottom pivot, so Y -124 is the subtitle's lower edge and the text sits above
        // it. With a top pivot the 16px band runs down to -140, which a focused card -
        // resting at -138 and rising 10 - draws straight over.
        var subtitleRt = (RectTransform)subtitle.transform;
        Corner(subtitleRt, new Vector2(0.5f, 1f), new Vector2(0f, SubtitleY), new Vector2(400f, 16f));
        subtitleRt.pivot = new Vector2(0.5f, 0f);
        var subtitleGroup = EnsureComponent<CanvasGroup>(subtitle.gameObject);

        var hint = Label(root, "Hint", "1-4 OR ARROWS · ENTER TO TAKE", 10f, MenuPalette.HintUpgrade,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        hint.characterSpacing = Tracking(2.5f, 10f);
        hint.alignment = TextAlignmentOptions.Center;
        var hintRt = (RectTransform)hint.transform;
        Corner(hintRt, new Vector2(0.5f, 0f), new Vector2(0f, UpHintY), new Vector2(500f, 14f));
        hintRt.pivot = new Vector2(0.5f, 0f);
        var hintGroup = EnsureComponent<CanvasGroup>(hint.gameObject);

        // ---- cards -----------------------------------------------------------
        var cards = new UpgradeCard[4];
        var items = new List<MenuFocusItem>();

        for (int i = 0; i < 4; i++)
        {
            var type = UpgradeCatalog.Order[i];
            cards[i] = BuildCard(root, "Card_" + UpgradeCatalog.DisplayName(type), type, CardX[i]);
            items.Add(cards[i]);
        }

        focus.SetItems(items);
        Bind(focus, "horizontal", true);
        Bind(focus, "numberKeys", true);

        Bind(screen, "group", group);
        Bind(screen, "scrim", scrim.GetComponent<Image>());
        Bind(screen, "banner", banner);
        Bind(screen, "bannerGroup", bannerGroup);
        Bind(screen, "subtitleGroup", subtitleGroup);
        Bind(screen, "hintGroup", hintGroup);
        Bind(screen, "focus", focus);
        BindArray(screen, "cards", cards);

        return screen;
    }

    static UpgradeCard BuildCard(RectTransform parent, string name, UpgradeType type, float x)
    {
        var card = Ensure(parent, name);
        Corner(card, new Vector2(0.5f, 1f), new Vector2(x, CardY), new Vector2(CardW, CardH));
        card.pivot = new Vector2(0.5f, 1f);

        var group = EnsureComponent<CanvasGroup>(card.gameObject);
        var component = EnsureComponent<UpgradeCard>(card.gameObject);
        Bind(component, "type", (int)type);
        Bind(component, "focusSeconds", 0.12f);

        // Glow sits behind everything, including the shadow.
        var glow = Ensure(card, "Glow");
        Centre(glow, new Vector2(0f, -CardH * 0.5f), new Vector2(CardW + 60f, CardH + 60f));
        glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
        glow.pivot = new Vector2(0.5f, 0.5f);
        var glowImage = Paint(glow, Hud + "hud_glow.png", MenuPalette.Clear, Image.Type.Simple);

        var shadow = CardLayer(card, "Shadow", "upgrade_card_body.png", MenuPalette.Shadow,
                               new Vector2(CardShadow, -CardShadow));
        var body = CardLayer(card, "Body", "upgrade_card_body.png", MenuPalette.CardBody, Vector2.zero);
        // The one raycast target on the card - the whole column is the click area.
        body.raycastTarget = true;

        // Head and rule are 234-wide sprites like the body, so they line up on their own.
        var head = Ensure(card, "Head");
        Corner(head, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CardW, CardHeadH));
        head.pivot = new Vector2(0.5f, 1f);
        var headImage = Paint(head, Menus + "upgrade_card_head.png", MenuPalette.Terracotta, Image.Type.Simple);

        var headLabel = Label(head, "Label", UpgradeCatalog.DisplayName(type), 15f, MenuPalette.Cream,
                              TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        headLabel.characterSpacing = Tracking(1.5f, 15f);
        headLabel.alignment = TextAlignmentOptions.Center;
        Centre((RectTransform)headLabel.transform, Vector2.zero, new Vector2(CardContentW, CardHeadH));

        var rule = Ensure(card, "Rule");
        Corner(rule, new Vector2(0.5f, 1f), new Vector2(0f, -CardHeadH), new Vector2(CardW, CardRuleH));
        rule.pivot = new Vector2(0.5f, 1f);
        var ruleImage = Paint(rule, Menus + "upgrade_card_rule.png", MenuPalette.CardRule, Image.Type.Simple);

        var border = CardLayer(card, "Border", "upgrade_card_border.png", MenuPalette.CardBorder, Vector2.zero);

        // ---- icon plate ------------------------------------------------------
        var plate = Ensure(card, "IconPlate");
        Corner(plate, new Vector2(0.5f, 1f), new Vector2(0f, -PlateTop), new Vector2(PlateSize, PlateSize));
        plate.pivot = new Vector2(0.5f, 1f);
        var plateImage = Paint(plate, Shared + "ui_square.png", MenuPalette.IconPlate, Image.Type.Simple);

        var plateBorder = Ensure(plate, "Border");
        Stretch(plateBorder);
        var plateBorderImage = Paint(plateBorder, Shared + "ui_square.png",
                                     MenuPalette.IconPlateBorder, Image.Type.Simple);
        // Drawn as a ring: an inner square punches the centre out of the 3px keyline.
        var plateInner = Ensure(plateBorder, "Inner");
        Centre(plateInner, Vector2.zero, new Vector2(PlateSize - PlateStroke * 2f, PlateSize - PlateStroke * 2f));
        Paint(plateInner, Shared + "ui_square.png", MenuPalette.IconPlate, Image.Type.Simple);

        var icon = Ensure(plate, "Icon");
        Centre(icon, Vector2.zero, new Vector2(CardIconSize, CardIconSize));
        var iconImage = Paint(icon, Menus + IconFile(type, large: true), MenuPalette.Cream, Image.Type.Simple);
        icon.SetAsLastSibling();

        // ---- description -----------------------------------------------------
        float descTop = PlateTop + PlateSize + DescGap;
        var description = Label(card, "Description", "", 17f, MenuPalette.Description,
                                TitleScreenFonts.SairaAsset, null);
        description.alignment = TextAlignmentOptions.Top;
        description.enableWordWrapping = true;
        // Saira's natural leading at 17 is already ~20, which is the 17/20 the spec
        // asks for. An explicit lineSpacing on top of that pushed the third line out of
        // the box, and Truncate then cut the longest description mid-word.
        description.lineSpacing = 0f;
        description.overflowMode = TextOverflowModes.Overflow;
        var descRt = (RectTransform)description.transform;
        // Four lines of room for three lines of copy: there is 131px of clear card
        // between here and the footer, so the headroom costs nothing.
        Corner(descRt, new Vector2(0.5f, 1f), new Vector2(0f, -descTop), new Vector2(CardContentW, DescLine * 4f));
        descRt.pivot = new Vector2(0.5f, 1f);

        // ---- footer ----------------------------------------------------------
        // Padding runs from the visible column, not the sprite, which is what keeps
        // every chip inside the middle 164 and clear of the slanted edges.
        float contentLeft = -CardVisibleW * 0.5f + FooterPad;
        float contentRight = CardVisibleW * 0.5f - FooterPad;
        // The chips pivot bottom-left, so this is their bottom edge: 14 up from the
        // card's bottom, measured from the top-centre anchor.
        float footerY = -(CardH - FooterBottom);

        var keyChip = Ensure(card, "KeyChip");
        Corner(keyChip, new Vector2(0.5f, 1f), new Vector2(contentLeft, footerY), new Vector2(KeyChip, ChipH));
        keyChip.pivot = new Vector2(0f, 0f);
        var keyChipBorder = Slab(keyChip, Shared + "button_slab_border.png",
                                 MenuPalette.KeyChipKeyline, ChipH, true, false);
        var keyChipLabel = Label(keyChip, "Label", "1", 10f, MenuPalette.KeyChipLabel,
                                 TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        keyChipLabel.alignment = TextAlignmentOptions.Center;
        Stretch((RectTransform)keyChipLabel.transform);

        var fills = new Image[3];
        var keylines = new Image[3];
        var labels = new TextMeshProUGUI[3];
        string[] numerals = { "I", "II", "III" };

        float tiersWidth = ChipW * 3f + ChipGap * 2f;
        for (int i = 0; i < 3; i++)
        {
            var chip = Ensure(card, "Tier_" + numerals[i]);
            float chipX = contentRight - tiersWidth + i * (ChipW + ChipGap);
            Corner(chip, new Vector2(0.5f, 1f), new Vector2(chipX, footerY), new Vector2(ChipW, ChipH));
            chip.pivot = new Vector2(0f, 0f);

            var fill = Ensure(chip, "Fill");
            Stretch(fill);
            fills[i] = Slab(fill, Shared + "button_slab_fill.png", MenuPalette.Clear, ChipH, true, false);

            var keyline = Ensure(chip, "Keyline");
            Stretch(keyline);
            keylines[i] = Slab(keyline, Shared + "button_slab_border.png",
                               MenuPalette.ChipLockedKeyline, ChipH, true, false);

            labels[i] = Label(chip, "Label", numerals[i], 10f, MenuPalette.ChipLockedLabel,
                              TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
            labels[i].alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)labels[i].transform);
        }

        Bind(component, "group", group);
        Bind(component, "body", body);
        Bind(component, "border", border);
        Bind(component, "head", headImage);
        Bind(component, "rule", ruleImage);
        Bind(component, "glow", glowImage);
        Bind(component, "headLabel", headLabel);
        Bind(component, "iconPlate", plateImage);
        Bind(component, "iconPlateBorder", plateBorderImage);
        Bind(component, "icon", iconImage);
        Bind(component, "description", description);
        Bind(component, "keyChipLabel", keyChipLabel);
        Bind(component, "keyChipKeyline", keyChipBorder);
        BindArray(component, "tierFills", fills);
        BindArray(component, "tierKeylines", keylines);
        BindArray(component, "tierLabels", labels);

        return component;
    }

    static Image CardLayer(RectTransform card, string name, string sprite, Color tint, Vector2 offset)
    {
        var rt = Ensure(card, name);
        Corner(rt, new Vector2(0.5f, 1f), offset, new Vector2(CardW, CardH));
        rt.pivot = new Vector2(0.5f, 1f);
        return Paint(rt, Menus + sprite, tint, Image.Type.Simple);
    }

    /// <summary>
    /// The 64px set for the 20px HUD slots, the 256 masters for the 38px cards. A 256
    /// icon downsampled to 20 with mipmaps off loses its silhouette.
    /// </summary>
    public static string IconFile(UpgradeType type, bool large)
    {
        string stem;
        switch (type)
        {
            case UpgradeType.Armor:  stem = "icon_armour"; break;
            case UpgradeType.Turret: stem = "icon_turret"; break;
            case UpgradeType.Tracks: stem = "icon_speed"; break;
            default:                 stem = "icon_demolition"; break;
        }
        return large ? stem + ".png" : stem + "_64.png";
    }

    // =========================================================================
    // Pause screen
    // =========================================================================

    static PauseScreen BuildPauseScreen(RectTransform canvas)
    {
        var root = Ensure(canvas, "PauseScreen");
        Stretch(root);
        var group = EnsureComponent<CanvasGroup>(root.gameObject);
        var screen = EnsureComponent<PauseScreen>(root.gameObject);
        var focus = EnsureComponent<MenuFocusGroup>(root.gameObject);

        var dim = Ensure(root, "Dim");
        Stretch(dim);
        var dimImage = Paint(dim, Dim, MenuPalette.ScrimPause, Image.Type.Simple);

        // The band overhangs top and bottom by 30 each - intended, it stops the slant
        // from reading as a floating shape.
        //
        // Stretched rather than a fixed 600, because 600 is only 540-plus-overhang at
        // 16:9. At 4:3 the canvas is taller than 540 and a fixed height leaves a gap at
        // both ends; anchoring to both edges keeps the overhang at every aspect ratio.
        var band = Ensure(root, "Band");
        band.anchorMin = new Vector2(0f, 0f);
        band.anchorMax = new Vector2(0f, 1f);
        band.pivot = new Vector2(0f, 0.5f);
        band.sizeDelta = new Vector2(BandW, (BandH - 540f));
        band.anchoredPosition = new Vector2(BandX, 0f);
        band.localScale = Vector3.one;
        Paint(band, Menus + "pause_band.png", MenuPalette.At(MenuPalette.Ink, 242), Image.Type.Simple);

        var lip = Ensure(band, "Lip");
        Stretch(lip);
        Paint(lip, Menus + "pause_band_lip.png", MenuPalette.Cream, Image.Type.Simple);

        // ---- content column ---------------------------------------------------
        var content = Ensure(root, "Content");
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = new Vector2(ContentX, 0f);
        content.sizeDelta = new Vector2(ContentW, -ContentInset * 2f);
        content.localScale = Vector3.one;

        var items = new List<CanvasGroup>();

        var title = Label(content, "Title", "PAUSED", 36f, MenuPalette.Cream,
                          TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
        title.characterSpacing = Tracking(3f, 36f);
        TopLeft((RectTransform)title.transform, new Vector2(0f, TitleY), new Vector2(ContentW, 44f));
        items.Add(EnsureComponent<CanvasGroup>(title.gameObject));

        var rule = Ensure(content, "Rule");
        TopLeft(rule, new Vector2(0f, PauseRuleY), new Vector2(PauseRuleW, PauseRuleH));
        Slab(rule, Shared + "button_slab_fill.png", MenuPalette.Terracotta, PauseRuleH, true, false);
        items.Add(EnsureComponent<CanvasGroup>(rule.gameObject));

        var statNames = new[] { "TIME", "KILLS", "LEVEL" };
        var statValues = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            var row = Ensure(content, "Stat_" + statNames[i]);
            TopLeft(row, new Vector2(0f, -(StatTop + i * (StatRowH + StatRowGap))),
                    new Vector2(ContentW, StatRowH));

            var label = Label(row, "Label", statNames[i], 9f, MenuPalette.StatLabel,
                              TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
            label.characterSpacing = Tracking(2f, 9f);
            label.alignment = TextAlignmentOptions.Left;
            TopLeft((RectTransform)label.transform, new Vector2(0f, -9f), new Vector2(120f, 12f));

            statValues[i] = Label(row, "Value", "0", 20f, i == 1 ? MenuPalette.Yellow : MenuPalette.Cream,
                                  TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
            statValues[i].alignment = TextAlignmentOptions.Right;
            var valueRt = (RectTransform)statValues[i].transform;
            valueRt.anchorMin = valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.pivot = new Vector2(1f, 1f);
            valueRt.anchoredPosition = new Vector2(0f, 0f);
            valueRt.sizeDelta = new Vector2(140f, 24f);
            valueRt.localScale = Vector3.one;

            var underline = Ensure(row, "Underline");
            underline.anchorMin = new Vector2(0f, 0f);
            underline.anchorMax = new Vector2(1f, 0f);
            underline.pivot = new Vector2(0.5f, 0f);
            underline.offsetMin = new Vector2(0f, 0f);
            underline.offsetMax = new Vector2(0f, 2f);
            underline.localScale = Vector3.one;
            Paint(underline, Shared + "ui_square.png", MenuPalette.StatUnderline, Image.Type.Simple);

            items.Add(EnsureComponent<CanvasGroup>(row.gameObject));
        }

        // ---- buttons, measured up from the bottom -----------------------------
        var labels = new[] { "RESUME", "RESTART RUN", "MENU" };
        var buttons = new MenuButton[3];
        float buttonBase = PauseHintH + PauseHintGap;

        for (int i = 0; i < 3; i++)
        {
            float y = buttonBase + (2 - i) * (PauseButtonH + PauseButtonGap);
            buttons[i] = BuildButton(content, "Button_" + labels[i].Replace(" ", ""), labels[i],
                                     new Vector2(0f, y), new Vector2(ContentW, PauseButtonH),
                                     16f, 2.5f, TextAlignmentOptions.Left, PauseLabelPad,
                                     MenuPalette.ButtonPause, MenuPalette.ButtonKeylinePause,
                                     MenuPalette.ShadowSoft, 0.08f);
            items.Add(buttons[i].GetComponent<CanvasGroup>());
        }

        var hint = Label(content, "Hint", "ESC TO RESUME", 10f, MenuPalette.HintPause,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        hint.characterSpacing = Tracking(2.5f, 10f);
        hint.alignment = TextAlignmentOptions.Left;
        var hintRt = (RectTransform)hint.transform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0f, 0f);
        hintRt.pivot = new Vector2(0f, 0f);
        hintRt.anchoredPosition = Vector2.zero;
        hintRt.sizeDelta = new Vector2(ContentW, PauseHintH);
        hintRt.localScale = Vector3.one;
        items.Add(EnsureComponent<CanvasGroup>(hint.gameObject));

        var focusItems = new List<MenuFocusItem> { buttons[0], buttons[1], buttons[2] };
        focus.SetItems(focusItems);
        Bind(focus, "horizontal", false);
        Bind(focus, "numberKeys", false);

        // The band, its dim and the column reveal live on SlideBand, shared with the
        // settings screen so there is one copy of the slide curve rather than two that
        // have to agree.
        var slide = EnsureComponent<SlideBand>(root.gameObject);
        Bind(slide, "band", band);
        Bind(slide, "dim", dimImage);
        BindArray(slide, "contentItems", items.ToArray());
        Bind(slide, "dimColour", (Color)MenuPalette.ScrimPause);

        Bind(screen, "group", group);
        Bind(screen, "slide", slide);
        Bind(screen, "timeValue", statValues[0]);
        Bind(screen, "killsValue", statValues[1]);
        Bind(screen, "levelValue", statValues[2]);
        Bind(screen, "focus", focus);
        Bind(screen, "resumeButton", buttons[0]);
        Bind(screen, "restartButton", buttons[1]);
        Bind(screen, "menuButton", buttons[2]);

        return screen;
    }

    // =========================================================================
    // End screen
    // =========================================================================

    static EndScreen BuildEndScreen(RectTransform canvas)
    {
        var root = Ensure(canvas, "EndScreen");
        Stretch(root);
        var group = EnsureComponent<CanvasGroup>(root.gameObject);
        var screen = EnsureComponent<EndScreen>(root.gameObject);
        var focus = EnsureComponent<MenuFocusGroup>(root.gameObject);

        var scrim = Ensure(root, "Scrim");
        Stretch(scrim);
        var scrimImage = Paint(scrim, Dim, MenuPalette.ScrimGameOver, Image.Type.Simple);

        var vignette = Ensure(root, "Vignette");
        Stretch(vignette);
        Paint(vignette, Menus + "vignette_alarm.png", Color.white, Image.Type.Simple);
        var vignetteGroup = EnsureComponent<CanvasGroup>(vignette.gameObject);
        vignetteGroup.alpha = 0f;

        var flash = Ensure(root, "Flash");
        Stretch(flash);
        Paint(flash, Dim, MenuPalette.Cream, Image.Type.Simple);
        var flashGroup = EnsureComponent<CanvasGroup>(flash.gameObject);
        flashGroup.alpha = 0f;

        // ---- verdict ----------------------------------------------------------
        var verdict = Ensure(root, "Verdict");
        Corner(verdict, new Vector2(0.5f, 1f), new Vector2(0f, VerdictY), new Vector2(900f, 110f));
        verdict.pivot = new Vector2(0.5f, 0.5f);
        verdict.localRotation = Quaternion.Euler(0f, 0f, VerdictAngle);
        var verdictGroup = EnsureComponent<CanvasGroup>(verdict.gameObject);

        var verdictLabel = Label(verdict, "Label", "WRECKED", 92f, MenuPalette.Alarm,
                                 TitleScreenFonts.BungeeAsset, null);
        verdictLabel.characterSpacing = Tracking(6f, 92f);
        verdictLabel.alignment = TextAlignmentOptions.Center;
        Stretch((RectTransform)verdictLabel.transform);
        StampMaterial(verdictLabel);

        var subtitle = Label(root, "Subtitle", "ZONE — RUN ENDED", 10f, MenuPalette.Subtitle,
                             TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        subtitle.characterSpacing = Tracking(4f, 10f);
        subtitle.alignment = TextAlignmentOptions.Center;
        var subRt = (RectTransform)subtitle.transform;
        Corner(subRt, new Vector2(0.5f, 1f), new Vector2(0f, EndSubtitleY), new Vector2(600f, 14f));
        subRt.pivot = new Vector2(0.5f, 1f);

        // ---- stat strip, five columns -----------------------------------------
        // Horizontally stretched: sizeDelta.x is the inset off both edges, and
        // anchoredPosition has to come *after* it - assigning one rewrites the other.
        var strip = Ensure(root, "StatStrip");
        strip.anchorMin = new Vector2(0f, 1f);
        strip.anchorMax = new Vector2(1f, 1f);
        strip.pivot = new Vector2(0.5f, 1f);
        strip.sizeDelta = new Vector2(-StripInset * 2f, StripH);
        strip.anchoredPosition = new Vector2(0f, StripY);
        strip.localScale = Vector3.one;
        var stripGroup = EnsureComponent<CanvasGroup>(strip.gameObject);

        var stripRule = Ensure(strip, "Rule");
        stripRule.anchorMin = new Vector2(0f, 1f);
        stripRule.anchorMax = new Vector2(1f, 1f);
        stripRule.pivot = new Vector2(0.5f, 1f);
        stripRule.sizeDelta = new Vector2(0f, StripRuleH);
        stripRule.anchoredPosition = Vector2.zero;
        stripRule.localScale = Vector3.one;
        Paint(stripRule, Shared + "ui_square.png", MenuPalette.StripRule, Image.Type.Simple);

        float stripW = 960f - StripInset * 2f;
        float columnW = stripW / 5f;

        // Upgrades last: it is the only column that is a picture rather than a numeral,
        // so it reads better as the end of the row than as a gap in the middle of one.
        var columnLabels = new[] { "SURVIVED", "KILLS", "LEVEL", "COINS", "UPGRADES" };
        const int PipColumn = 4;
        var values = new TextMeshProUGUI[5];
        RectTransform levelNumeral = null;
        var pips = new Image[12];

        for (int c = 0; c < 5; c++)
        {
            var column = Ensure(strip, "Col_" + columnLabels[c]);
            column.anchorMin = column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 1f);
            column.anchoredPosition = new Vector2(c * columnW, -(StripRuleH + StripPad));
            column.sizeDelta = new Vector2(columnW, ValueH + StripLabelGap + StripLabelH);
            column.localScale = Vector3.one;

            if (c == PipColumn)
            {
                BuildPipBlock(column, columnW, pips);
            }
            else
            {
                values[c] = Label(column, "Value", "0", 40f,
                                  c == 1 ? MenuPalette.Yellow : MenuPalette.Cream,
                                  TitleScreenFonts.BungeeAsset, TitleScreenFonts.BungeeUnderlayMat);
                values[c].alignment = TextAlignmentOptions.Center;
                var vrt = (RectTransform)values[c].transform;
                TopLeft(vrt, Vector2.zero, new Vector2(columnW, ValueH));
                if (c == 2) levelNumeral = vrt;
            }

            var label = Label(column, "Label", columnLabels[c], 9f, MenuPalette.StripLabel,
                              TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
            label.characterSpacing = Tracking(2.5f, 9f);
            label.alignment = TextAlignmentOptions.Center;
            TopLeft((RectTransform)label.transform, new Vector2(0f, -(ValueH + StripLabelGap)),
                    new Vector2(columnW, StripLabelH));
        }

        // ---- buttons -----------------------------------------------------------
        var primary = BuildButton(root, "Button_Primary", "RETRY",
                                  new Vector2(-(EndButtonW + EndButtonGap) * 0.5f, EndButtonY),
                                  new Vector2(EndButtonW, EndButtonH),
                                  15f, 2.5f, TextAlignmentOptions.Center, 0f,
                                  MenuPalette.ButtonGameOver, MenuPalette.ButtonKeylineGameOver,
                                  MenuPalette.ShadowSoft, 0.08f);
        CentreBottom((RectTransform)primary.transform,
                     new Vector2(-(EndButtonW + EndButtonGap) * 0.5f, EndButtonY),
                     new Vector2(EndButtonW, EndButtonH));

        var menu = BuildButton(root, "Button_Menu", "MENU",
                               new Vector2((EndButtonW + EndButtonGap) * 0.5f, EndButtonY),
                               new Vector2(EndButtonW, EndButtonH),
                               15f, 2.5f, TextAlignmentOptions.Center, 0f,
                               MenuPalette.ButtonGameOver, MenuPalette.ButtonKeylineGameOver,
                               MenuPalette.ShadowSoft, 0.08f);
        CentreBottom((RectTransform)menu.transform,
                     new Vector2((EndButtonW + EndButtonGap) * 0.5f, EndButtonY),
                     new Vector2(EndButtonW, EndButtonH));

        var hint = Label(root, "Hint", "ENTER TO RETRY", 10f, MenuPalette.HintGameOver,
                         TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        hint.characterSpacing = Tracking(2f, 10f);
        hint.alignment = TextAlignmentOptions.Center;
        var hintRt = (RectTransform)hint.transform;
        Corner(hintRt, new Vector2(0.5f, 0f), new Vector2(0f, EndHintY), new Vector2(500f, 14f));
        hintRt.pivot = new Vector2(0.5f, 0f);

        focus.SetItems(new List<MenuFocusItem> { primary, menu });
        Bind(focus, "horizontal", true);
        Bind(focus, "numberKeys", false);

        Bind(screen, "group", group);
        Bind(screen, "scrim", scrimImage);
        Bind(screen, "vignetteGroup", vignetteGroup);
        Bind(screen, "flashGroup", flashGroup);
        Bind(screen, "verdict", verdict);
        Bind(screen, "verdictGroup", verdictGroup);
        Bind(screen, "verdictLabel", verdictLabel);
        Bind(screen, "subtitle", subtitle);
        Bind(screen, "stripGroup", stripGroup);
        Bind(screen, "timeValue", values[0]);
        Bind(screen, "killsValue", values[1]);
        Bind(screen, "levelValue", values[2]);
        Bind(screen, "levelNumeral", levelNumeral);
        Bind(screen, "coinsValue", values[3]);
        BindArray(screen, "pips", pips);
        Bind(screen, "focus", focus);
        Bind(screen, "primaryButton", primary);
        Bind(screen, "menuButton", menu);
        BindArray(screen, "buttonGroups", new[]
        {
            primary.GetComponent<CanvasGroup>(), menu.GetComponent<CanvasGroup>(),
        });
        Bind(screen, "hint", hint);

        return screen;
    }

    /// <summary>
    /// The run's build in one glance: four stacks in the fixed upgrade order, three
    /// pips each, filled bottom-up. No icons - at this size the pattern is the readable
    /// thing, and the order is fixed so players know which column is which.
    /// </summary>
    static void BuildPipBlock(RectTransform column, float columnW, Image[] pips)
    {
        var block = Ensure(column, "Pips");
        float blockW = PipW * 4f + PipColumnGap * 3f;
        float blockH = PipH * 3f + PipGap * 2f;
        TopLeft(block, new Vector2((columnW - blockW) * 0.5f, -(ValueH - blockH) * 0.5f),
                new Vector2(blockW, blockH));

        for (int stack = 0; stack < 4; stack++)
        {
            for (int row = 0; row < 3; row++)
            {
                var pip = Ensure(block, "Pip_" + stack + "_" + row);
                pip.anchorMin = pip.anchorMax = new Vector2(0f, 0f);
                pip.pivot = new Vector2(0f, 0f);
                pip.anchoredPosition = new Vector2(stack * (PipW + PipColumnGap),
                                                   row * (PipH + PipGap));
                pip.sizeDelta = new Vector2(PipW, PipH);
                pip.localScale = Vector3.one;

                pips[stack * 3 + row] = Paint(pip, Shared + "ui_square.png",
                                              MenuPalette.PipEmpty, Image.Type.Simple);
            }
        }
    }

    // =========================================================================
    // Shared pieces
    // =========================================================================

    static MenuButton BuildButton(RectTransform parent, string name, string text,
                                  Vector2 position, Vector2 size,
                                  float fontSize, float trackingPx,
                                  TextAlignmentOptions align, float padLeft,
                                  Color restFill, Color restKeyline, Color restShadow,
                                  float focusSeconds)
    {
        var rt = Ensure(parent, name);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;

        EnsureComponent<CanvasGroup>(rt.gameObject);
        var button = EnsureComponent<MenuButton>(rt.gameObject);
        Bind(button, "focusSeconds", focusSeconds);

        var glow = Ensure(rt, "Glow");
        Centre(glow, Vector2.zero, size + new Vector2(50f, 40f));
        var glowImage = Paint(glow, Hud + "hud_glow.png", MenuPalette.Clear, Image.Type.Simple);

        var shadow = Ensure(rt, "Shadow");
        Centre(shadow, new Vector2(5f, -5f), size);
        var shadowImage = Slab(shadow, Shared + "button_slab_fill.png", restShadow, size.y, true, false);

        var fill = Ensure(rt, "Fill");
        Centre(fill, Vector2.zero, size);
        // The one raycast target on the button.
        var fillImage = Slab(fill, Shared + "button_slab_fill.png", restFill, size.y, true, true);

        var keyline = Ensure(rt, "Keyline");
        Centre(keyline, Vector2.zero, size);
        var keylineImage = Slab(keyline, Shared + "button_slab_border.png", restKeyline, size.y, true, false);

        var label = Label(rt, "Label", text, fontSize, MenuPalette.ButtonLabel,
                          TitleScreenFonts.ArchivoAsset, TitleScreenFonts.ArchivoUnderlayMat);
        label.characterSpacing = Tracking(trackingPx, fontSize);
        label.alignment = align;
        var labelRt = (RectTransform)label.transform;
        Stretch(labelRt);
        labelRt.offsetMin = new Vector2(padLeft, 0f);
        labelRt.offsetMax = new Vector2(-padLeft, 0f);

        Bind(button, "fill", fillImage);
        Bind(button, "keyline", keylineImage);
        Bind(button, "shadow", shadowImage);
        Bind(button, "glow", glowImage);
        Bind(button, "label", label);
        Bind(button, "restFill", restFill);
        Bind(button, "restKeyline", restKeyline);
        Bind(button, "restShadow", restShadow);

        return button;
    }

    /// <summary>
    /// The verdict is the one piece of type that has to hold over bright terrain with
    /// no plate behind it, so it gets its own material: a cream outline plus a hard
    /// offset underlay, neither of which the shared presets carry.
    /// </summary>
    static void StampMaterial(TextMeshProUGUI text)
    {
        if (text.font == null) return;

        var mat = new Material(text.font.material) { name = "Verdict Stamp" };
        mat.EnableKeyword("OUTLINE_ON");
        mat.SetColor(ShaderUtilities.ID_OutlineColor, MenuPalette.Cream);
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);

        mat.EnableKeyword("UNDERLAY_ON");
        mat.SetColor("_UnderlayColor", new Color32(16, 10, 6, 140));
        mat.SetFloat("_UnderlayOffsetX", 0.35f);
        mat.SetFloat("_UnderlayOffsetY", -0.35f);
        mat.SetFloat("_UnderlayDilate", 0f);
        mat.SetFloat("_UnderlaySoftness", 0f);

        ShaderUtilities.GetShaderPropertyIDs();
        ShaderUtilities.UpdateShaderRatios(mat);
        text.fontSharedMaterial = mat;
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
        // The slab border art has zero top/bottom slice, so its horizontal keylines
        // live in the centre cell - Fill Center has to stay on or they vanish.
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
        // Scrims, labels, icons and pips must never eat a WebGL click; the card body
        // and the button fill turn this back on explicitly.
        img.raycastTarget = false;
        return img;
    }

    static Vector2 Measure(TextMeshProUGUI text)
    {
        text.ForceMeshUpdate();
        var size = text.GetPreferredValues();
        return new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
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

    static void Corner(RectTransform rt, Vector2 corner, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = corner;
        rt.anchorMax = corner;
        rt.pivot = corner;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
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

    static void CentreBottom(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
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
        text.enableAutoSizing = false;          // the small sizes here are deliberate
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Sprite Sprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[Menus] Sprite not found (is it imported as Sprite?): " + path);
        return s;
    }

    // ---- serialized binding --------------------------------------------------

    static void Bind(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Menus] No property " + property + " on " + target); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, bool value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Menus] No property " + property + " on " + target); return; }
        prop.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, float value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Menus] No property " + property + " on " + target); return; }
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Menus] No property " + property + " on " + target); return; }
        prop.enumValueIndex = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Bind(Object target, string property, Color value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(property);
        if (prop == null) { Debug.LogError("[Menus] No property " + property + " on " + target); return; }
        prop.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BindArray<T>(Object target, string property, T[] values) where T : Object
    {
        var so = new SerializedObject(target);
        var list = so.FindProperty(property);
        if (list == null) { Debug.LogError("[Menus] No property " + property + " on " + target); return; }

        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
