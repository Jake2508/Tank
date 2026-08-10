using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens every play scene and reports what would throw at runtime: components whose
/// script no longer exists, serialized references left empty, and missing singletons.
///
/// Written because "there are a bunch of errors on that level" is not something a
/// layout capture can answer - the failures are null references that only fire once the
/// scene is running, and they need to be found by inspecting the scene rather than by
/// playing it.
/// </summary>
public static class SceneAudit
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/L_Woodlands.unity",
        "Assets/Scenes/L_Desert.unity",
        "Assets/Scenes/L_Snowy.unity",
        "Assets/Scenes/L_MainMenu.unity",
    };

    /// <summary>
    /// Screens that register a static Instance in Awake for another script to find.
    ///
    /// Unity never calls Awake on an object that is inactive when the scene loads, so a
    /// screen saved inactive silently never registers and whatever opens it finds null -
    /// no error, no warning, just a button that does nothing. That is exactly how the
    /// settings screen shipped broken. These must be saved ACTIVE and hide themselves.
    /// </summary>
    static readonly string[] MustBeActive = { "SettingsMenu", "LevelSelectUI" };

    /// <summary>
    /// Fields that are genuinely allowed to be empty - optional hooks, or things the
    /// game assigns at runtime.
    /// </summary>
    static readonly HashSet<string> Optional = new HashSet<string>
    {
        "destroyEffect", "trail", "BigShellProjectile", "m_Script",
        "levelLoader", "characterExplosion", "globalVolumeGO",
        // The level-select back button is a standalone self-hover slab; only the slabs
        // in the title column carry a selection chevron.
        "chevron",
        // Deliberately empty: the slot a track-bend Animator drops into once the model
        // is rigged. TankTurnFx checks its parameters exist before setting them.
        "trackAnimator",
    };

    /// <summary>
    /// Package components with their own conventions about empty fields. Cinemachine in
    /// particular leaves Follow, LookAt and blend assets null as a matter of course, and
    /// six such warnings per scene drown out the one that matters.
    /// </summary>
    static bool IsPackageComponent(System.Type type)
    {
        string name = type.FullName ?? string.Empty;
        return name.StartsWith("Cinemachine") || name.StartsWith("TMPro") ||
               name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor");
    }

    [MenuItem("Tools/Tankeo/17 - Audit Scenes")]
    public static void Audit()
    {
        foreach (var path in Scenes) AuditScene(path);
    }

    static void AuditScene(string scenePath)
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        string name = Path.GetFileNameWithoutExtension(scenePath);
        int problems = 0;

        // ---- components whose script is gone -------------------------------
        var missing = new List<string>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (count > 0) missing.Add(HierarchyPath(t) + " x" + count);
            }
        }

        if (missing.Count > 0)
        {
            problems += missing.Count;
            Debug.LogError("[Audit] " + name + ": " + missing.Count +
                           " object(s) with missing scripts -> " + string.Join(", ", missing.ToArray()));
        }

        // ---- empty references on our own components -------------------------
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;

                var type = behaviour.GetType();
                // Only project scripts; package components have their own conventions.
                if (type.Namespace != null && type.Namespace.StartsWith("Unity")) continue;
                if (type.Assembly.GetName().Name.StartsWith("Unity")) continue;
                if (IsPackageComponent(type)) continue;

                var so = new SerializedObject(behaviour);
                var prop = so.GetIterator();

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (prop.objectReferenceValue != null) continue;
                    if (Optional.Contains(prop.name)) continue;

                    Debug.LogWarning("[Audit] " + name + ": " + type.Name + " on " +
                                     HierarchyPath(behaviour.transform) + " has no " + prop.propertyPath);
                    problems++;
                }
            }
        }

        // ---- screens that must be active to register themselves ---------------
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(MustBeActive, t.name) < 0) continue;
                if (t.gameObject.activeSelf) continue;

                Debug.LogError("[Audit] " + name + ": " + HierarchyPath(t) + " is saved " +
                               "inactive, so its Awake never runs and its static Instance " +
                               "is never registered - whatever opens it will find null.");
                problems++;
            }
        }

        // The play-scene singletons below do not exist in the title scene.
        if (name == "L_MainMenu")
        {
            if (problems == 0) Debug.Log("[Audit] " + name + ": clean.");
            else Debug.Log("[Audit] " + name + ": " + problems + " problem(s).");
            return;
        }

        // ---- singletons the scene needs -------------------------------------
        Require<GameManager>(scene, name, ref problems);
        Require<TimerManager>(scene, name, ref problems);
        Require<KillCounter>(scene, name, ref problems);
        Require<Level>(scene, name, ref problems);
        Require<EnemyManager>(scene, name, ref problems);
        Require<DamagePopup>(scene, name, ref problems);
        Require<MenuController>(scene, name, ref problems);
        Require<HudController>(scene, name, ref problems);
        Require<GameFeel>(scene, name, ref problems);

        if (problems == 0) Debug.Log("[Audit] " + name + ": clean.");
        else Debug.Log("[Audit] " + name + ": " + problems + " problem(s).");
    }

    static void Require<T>(Scene scene, string sceneName, ref int problems) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<T>(true) != null) return;

        Debug.LogError("[Audit] " + sceneName + ": no " + typeof(T).Name + " in the scene.");
        problems++;
    }

    static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
