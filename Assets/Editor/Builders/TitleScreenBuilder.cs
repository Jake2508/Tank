using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the L_MainMenu title screen to the "Field Slab" spec
/// (TITLE_SCREEN_1a_IMPLEMENTATION.md). Idempotent - run it as often as you like.
///
/// The spec is written in 1920x1080 canvas units; this project's canvas is
/// 960x540, so every number below is the spec value halved.
/// </summary>
public static class TitleScreenBuilder
{
    const string ScenePath = "Assets/Scenes/L_MainMenu.unity";
    const string Sprites = "Assets/UI/Main Menu/";
    const string Shared = "Assets/UI/Shared/";      // slab art the level-select screen uses too
    const string FadeClipPath = "Assets/Animation/MainMenuFade.anim";

    // ---- palette (section 3) -------------------------------------------------
    static readonly Color Cream = new Color32(244, 238, 220, 255);
    static readonly Color Ink = new Color32(31, 23, 18, 235);
    static readonly Color InkHover = new Color32(44, 33, 26, 245);
    static readonly Color Terracotta = new Color32(196, 85, 47, 255);
    // Spec section 5 gives Quit's hover as #C4552F A140, but that replaces the body
    // colour outright, so the slab drops to 55% opacity and the terrain reads through
    // it. This is the same terracotta composited *over* the ink body instead, which
    // is what "only Quit turns warm" is after, at the body's own opacity.
    static readonly Color TerracottaWarm = new Color32(122, 57, 34, 235);
    static readonly Color HardShadow = new Color32(21, 16, 11, 140);
    static readonly Color SignalYellow = new Color32(233, 201, 63, 255);
    static readonly Color PillText = new Color32(31, 23, 18, 215);
    static readonly Color HintCream = new Color32(244, 238, 220, 185);
    static readonly Color VersionCream = new Color32(244, 238, 220, 128);

    // ---- layout, 960x540 units ----------------------------------------------
    const float TitleX = 46f, TitleY = -24f, TitleW = 295.5f, TitleH = 198f;
    const float TitleTilt = -2f;

    const float ColX = 55f, ColW = 300f;
    const float PlayY = -262.5f, PlayH = 55f;
    const float SettingsY = -328.5f, SettingsH = 49f;
    const float QuitY = -388.5f, QuitH = 49f;

    const float LabelInset = 23f;
    const float PlayLabelSize = 27.5f, MenuLabelSize = 21.5f, LabelSpacing = 4f;

    [MenuItem("Tools/Tankeo/2 - Build Title Screen")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleScreenFonts.ArchivoAsset) == null)
            TitleScreenFonts.Generate();

        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var canvasGo = FindRoot(scene, "Canvas");
        if (canvasGo == null) { Debug.LogError("[TitleScreen] No 'Canvas' in " + ScenePath); return; }

        ConfigureCanvas(canvasGo);

        var canvas = (RectTransform)canvasGo.transform;
        var menu = canvas.Find("MainMenuUI") as RectTransform;
        if (menu == null) { Debug.LogError("[TitleScreen] No 'MainMenuUI' under Canvas."); return; }

        BuildScrim(canvas, menu);
        menu.SetSiblingIndex(0);   // LevelSelectUI draws over the title screen

        var title = BuildTitle(menu);
        var play = BuildSlab(menu, "PlayButton", "PLAY", PlayY, PlayH, PlayLabelSize,
                             Terracotta, Terracotta, withKeyPill: true);
        var settings = BuildSlab(menu, "SettingsButton", "SETTINGS", SettingsY, SettingsH, MenuLabelSize,
                                 Ink, InkHover, withKeyPill: false);
        var quit = BuildSlab(menu, "QuitButton", "QUIT", QuitY, QuitH, MenuLabelSize,
                             Ink, TerracottaWarm, withKeyPill: false);

        BuildHint(menu);
        BuildVersion(menu);

        // Scrim is index 0 inside the menu, so everything else indexes from 1.
        title.SetSiblingIndex(1);
        play.transform.SetSiblingIndex(2);
        settings.transform.SetSiblingIndex(3);
        quit.transform.SetSiblingIndex(4);
        menu.Find("InputHint").SetSiblingIndex(5);
        menu.Find("VersionLabel").SetSiblingIndex(6);

        WireNav(menu.gameObject, play, settings, quit);
        RetargetFadeClip();
        MenuAnimators.FixTriggerTransitions(MenuAnimators.MainMenuController);
        ClearEventSystemFirstSelected(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[TitleScreen] Build complete.");
    }

    // =========================================================================
    // Canvas
    // =========================================================================

    static void ConfigureCanvas(GameObject canvasGo)
    {
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960f, 540f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        // The spec asks for Match 0.5, but this layout is a vertical stack down the
        // left edge, so height is the binding constraint: at 0.5 a short/wide browser
        // window scales the UI up with width until InputHint runs into QUIT. Match 1
        // pins UI height at 540 so the column can never collide. Identical at 960x540.
        scaler.matchWidthOrHeight = 1f;
        scaler.referencePixelsPerUnit = 100f;
        EditorUtility.SetDirty(scaler);
    }

    /// <summary>
    /// The scrim belongs to the title screen, not the Canvas: level select is a
    /// full-screen takeover with a scrim of its own, and two of them stacked darken
    /// the left third twice over. Living under MainMenuUI means it fades out with
    /// everything else it belongs to.
    /// </summary>
    static void BuildScrim(RectTransform canvas, RectTransform menu)
    {
        var stale = canvas.Find("Scrim");
        if (stale != null && stale.parent == canvas) Object.DestroyImmediate(stale.gameObject);

        var scrim = Ensure(menu, "Scrim");
        Stretch(scrim);
        scrim.SetSiblingIndex(0);

        var img = EnsureComponent<Image>(scrim.gameObject);
        img.sprite = Sprite(Sprites + "menu_scrim.png");
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    // =========================================================================
    // Title lockup
    // =========================================================================

    static RectTransform BuildTitle(RectTransform menu)
    {
        // The MainMenuFade clip pins TitleImage's rotation to 0, so the -2 degree
        // tilt and the idle bob live on a child.
        var title = Ensure(menu, "TitleImage");
        Corner(title, new Vector2(0f, 1f), new Vector2(TitleX, TitleY), new Vector2(TitleW, TitleH));
        title.localRotation = Quaternion.identity;
        title.localScale = Vector3.one;
        Strip<Image>(title.gameObject);
        Strip<CanvasRenderer>(title.gameObject);
        ClearChildren(title);

        var lockup = Ensure(title, "Lockup");
        Stretch(lockup);
        lockup.localRotation = Quaternion.Euler(0f, 0f, TitleTilt);

        var img = EnsureComponent<Image>(lockup.gameObject);
        img.sprite = Sprite(Sprites + "tankeo_title_lockup.png");
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = false;

        EnsureComponent<TitleBob>(lockup.gameObject);
        return title;
    }

    // =========================================================================
    // Menu slabs
    // =========================================================================

    static MenuSlab BuildSlab(RectTransform menu, string name, string label, float y, float height,
                              float labelSize, Color idle, Color hover, bool withKeyPill)
    {
        var root = Ensure(menu, name);
        Corner(root, new Vector2(0f, 1f), new Vector2(ColX, y), new Vector2(ColW, height));
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        // The old text button carried legacy UI effects and its own background.
        Strip<Shadow>(root.gameObject);      // also removes Outline, which derives from it
        Strip<Image>(root.gameObject);
        Strip<CanvasRenderer>(root.gameObject);
        ClearChildren(root);

        var shadow = Slab(root, "Shadow", Shared + "button_slab_fill.png", HardShadow, false);
        shadow.anchoredPosition = new Vector2(7f, -7f);

        var body = Slab(root, "Body", Shared + "button_slab_fill.png", idle, true);
        var keyline = Slab(root, "Keyline", Shared + "button_slab_border.png", Cream, false);
        // Border art has zero top/bottom slice, so its horizontal keylines live in
        // the centre cell - Fill Center has to stay on or they vanish.

        float rightInset = withKeyPill ? 80f : 20f;
        var text = Label(root, "Label", label, labelSize, Cream, TitleScreenFonts.ArchivoUnderlayMat);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(LabelInset, 0f);
        text.rectTransform.offsetMax = new Vector2(-rightInset, 0f);
        text.alignment = TextAlignmentOptions.Left;

        if (withKeyPill) BuildKeyPill(root);

        var chevron = Ensure(root, "Chevron");
        chevron.anchorMin = new Vector2(0f, 0.5f);
        chevron.anchorMax = new Vector2(0f, 0.5f);
        chevron.pivot = new Vector2(1f, 0.5f);
        chevron.anchoredPosition = new Vector2(-9f, 0f);
        chevron.sizeDelta = new Vector2(15f, 20f);
        var chevImg = EnsureComponent<Image>(chevron.gameObject);
        chevImg.sprite = Sprite(Sprites + "chevron_select.png");
        chevImg.type = Image.Type.Simple;
        chevImg.color = SignalYellow;
        chevImg.raycastTarget = false;
        chevron.gameObject.SetActive(false);

        var button = EnsureComponent<Button>(root.gameObject);
        button.transition = Selectable.Transition.None;   // MenuSlab drives the visuals
        button.targetGraphic = body.GetComponent<Image>();
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        button.interactable = true;

        var slab = EnsureComponent<MenuSlab>(root.gameObject);
        var so = new SerializedObject(slab);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("shadow").objectReferenceValue = shadow;
        so.FindProperty("body").objectReferenceValue = body.GetComponent<Image>();
        so.FindProperty("chevron").objectReferenceValue = chevron;
        so.FindProperty("idleTint").colorValue = idle;
        so.FindProperty("hoverTint").colorValue = hover;
        so.ApplyModifiedPropertiesWithoutUndo();

        return slab;
    }

    static RectTransform Slab(RectTransform parent, string name, string spritePath, Color tint, bool raycast)
    {
        var rt = Ensure(parent, name);
        Stretch(rt);
        var img = EnsureComponent<Image>(rt.gameObject);
        img.sprite = Sprite(spritePath);
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = tint;
        img.raycastTarget = raycast;
        return rt;
    }

    static void BuildKeyPill(RectTransform parent)
    {
        var pill = Ensure(parent, "KeyPill");
        pill.anchorMin = new Vector2(1f, 0.5f);
        pill.anchorMax = new Vector2(1f, 0.5f);
        pill.pivot = new Vector2(1f, 0.5f);
        pill.anchoredPosition = new Vector2(-17f, 0f);
        pill.sizeDelta = new Vector2(54f, 17f);

        // The spec draws a keyline box around ENTER, but the design mock does not have
        // one - and at 17px tall the slab border's 7 degree lean skews to nearly 40.
        Strip<Image>(pill.gameObject);
        Strip<CanvasRenderer>(pill.gameObject);

        var text = Label(pill, "Text", "ENTER", 10.5f, PillText, null);
        text.characterSpacing = 6f;
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
    }

    // =========================================================================
    // Corner furniture
    // =========================================================================

    static void BuildHint(RectTransform menu)
    {
        var text = Label(menu, "InputHint", "W / S · MOVE    ENTER · SELECT", 10.5f,
                         HintCream, TitleScreenFonts.ArchivoUnderlayMat);
        Corner(text.rectTransform, new Vector2(0f, 0f), new Vector2(58.5f, 17f), new Vector2(420f, 20f));
        text.characterSpacing = 10f;
        text.alignment = TextAlignmentOptions.BottomLeft;
    }

    static void BuildVersion(RectTransform menu)
    {
        var text = Label(menu, "VersionLabel", "V 1.0", 9.5f, VersionCream, TitleScreenFonts.ArchivoUnderlayMat);
        Corner(text.rectTransform, new Vector2(1f, 0f), new Vector2(-22.5f, 19f), new Vector2(150f, 20f));
        text.characterSpacing = 8f;
        text.alignment = TextAlignmentOptions.BottomRight;
    }

    // =========================================================================
    // Wiring
    // =========================================================================

    static void WireNav(GameObject menuGo, MenuSlab play, MenuSlab settings, MenuSlab quit)
    {
        var nav = EnsureComponent<MainMenuNav>(menuGo);
        var so = new SerializedObject(nav);
        var list = so.FindProperty("slabs");
        list.arraySize = 3;
        list.GetArrayElementAtIndex(0).objectReferenceValue = play;
        list.GetArrayElementAtIndex(1).objectReferenceValue = settings;
        list.GetArrayElementAtIndex(2).objectReferenceValue = quit;
        so.ApplyModifiedPropertiesWithoutUndo();

        var ui = menuGo.GetComponent<MainMenuUI>();
        if (ui != null)
        {
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("playButton").objectReferenceValue = play.GetComponent<Button>();
            uiSo.FindProperty("settingsButton").objectReferenceValue = settings.GetComponent<Button>();
            uiSo.FindProperty("quitButton").objectReferenceValue = quit.GetComponent<Button>();
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// MainMenuFade still drives TitleImage's Y toward the pre-redesign position and
    /// knows nothing about SettingsButton. Retarget it rather than leave the Animator
    /// pinning the title off-layout.
    /// </summary>
    static void RetargetFadeClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FadeClipPath);
        if (clip == null) { Debug.LogWarning("[TitleScreen] " + FadeClipPath + " not found."); return; }

        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve("TitleImage", typeof(RectTransform), "m_AnchoredPosition.y"),
            new AnimationCurve(new Keyframe(0f, TitleY - 25f), new Keyframe(0.8333333f, TitleY)));

        // Give SettingsButton the same entrance the other two already have.
        foreach (var axis in new[] { "x", "y", "z" })
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve("SettingsButton", typeof(Transform), "m_LocalScale." + axis),
                new AnimationCurve(new Keyframe(0f, 1.2f), new Keyframe(0.8333333f, 1f)));
        }
        AnimationUtility.SetEditorCurve(
            clip, EditorCurveBinding.FloatCurve("SettingsButton", typeof(Transform), "localEulerAnglesRaw.x"),
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.8333333f, 0f)));
        AnimationUtility.SetEditorCurve(
            clip, EditorCurveBinding.FloatCurve("SettingsButton", typeof(Transform), "localEulerAnglesRaw.y"),
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.8333333f, 0f)));
        AnimationUtility.SetEditorCurve(
            clip, EditorCurveBinding.FloatCurve("SettingsButton", typeof(Transform), "localEulerAnglesRaw.z"),
            new AnimationCurve(new Keyframe(0f, 10f), new Keyframe(0.41666666f, -5f), new Keyframe(0.8333333f, 0f)));

        EditorUtility.SetDirty(clip);
    }

    static void ClearEventSystemFirstSelected(Scene scene)
    {
        var es = FindRoot(scene, "EventSystem");
        if (es == null) return;
        var system = es.GetComponent<UnityEngine.EventSystems.EventSystem>();
        if (system == null) return;
        system.firstSelectedGameObject = null;
        EditorUtility.SetDirty(system);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

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

    static TextMeshProUGUI Label(Transform parent, string name, string content, float size, Color color, string materialPath)
    {
        var rt = Ensure(parent, name);
        var text = EnsureComponent<TextMeshProUGUI>(rt.gameObject);
        text.text = content;
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleScreenFonts.ArchivoAsset);
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
        text.characterSpacing = LabelSpacing;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Sprite Sprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogError("[TitleScreen] Sprite not found (is it imported as Sprite?): " + path);
        return s;
    }
}
