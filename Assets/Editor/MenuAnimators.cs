using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// The menu controllers were authored with Has Exit Time enabled on their
/// trigger-driven Any State transitions. Has Exit Time makes a transition wait
/// until the *current* state reaches a normalised time before it will fire, which
/// is meaningless for a trigger: SetTrigger("fadeOut") sat unhandled for ~0.15s
/// while the incoming screen had already faded most of the way in, and both states
/// then restarted from zero - reading on screen as the title screen fading out and
/// flashing back.
///
/// Can Transition To Self is deliberately left ON. A trigger is only cleared by the
/// transition that consumes it, so a self-transition that is blocked would leave
/// "fadeIn" set forever and fire it the moment the state changed - the same flash,
/// just later. With no exit time the self-transition fires at normalised time ~0,
/// so restarting the state it just entered is invisible.
/// </summary>
public static class MenuAnimators
{
    public const string MainMenuController = "Assets/Animation/MainMenuUI.controller";
    public const string LevelSelectController = "Assets/Animation/LevelSelectUI.controller";

    [MenuItem("Tools/Tankeo/6 - Fix Menu Fade Transitions")]
    public static void FixAll()
    {
        FixTriggerTransitions(MainMenuController);
        FixTriggerTransitions(LevelSelectController);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Clears Has Exit Time on every Any State transition driven by a trigger.</summary>
    public static void FixTriggerTransitions(string controllerPath)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            Debug.LogWarning("[MenuAnimators] Controller not found: " + controllerPath);
            return;
        }

        int fixedCount = 0;
        foreach (var layer in controller.layers)
        {
            var machine = layer.stateMachine;
            if (machine == null) continue;

            foreach (var transition in machine.anyStateTransitions)
            {
                if (!transition.hasExitTime) continue;
                transition.hasExitTime = false;
                transition.exitTime = 0f;
                fixedCount++;
            }
        }

        if (fixedCount == 0) return;

        EditorUtility.SetDirty(controller);
        Debug.Log("[MenuAnimators] Cleared Has Exit Time on " + fixedCount +
                  " Any State transition(s) in " + controllerPath);
    }
}
