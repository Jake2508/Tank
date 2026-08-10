using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs every generator and builder in dependency order, then the checks.
///
/// The pieces have to happen in this sequence and it is not obvious from the menu:
/// fonts and sprites before anything that lays out a screen, effects before the
/// gameplay pass that binds them into prefabs, and the scene builders before the
/// verifiers that inspect what they produced. Running them out of order mostly works
/// and then silently leaves one screen holding a null.
///
/// Each step is isolated, so one failure reports itself and the rest still run - a
/// half-built project with a clear error beats a build that stopped at step two.
/// </summary>
public static class BuildAll
{
    struct Step
    {
        public string name;
        public Action run;
    }

    [MenuItem("Tools/Tankeo/0 - Build Everything")]
    public static void Run()
    {
        var steps = new[]
        {
            // Content the screens are assembled from.
            new Step { name = "Fonts",           run = TitleScreenFonts.Generate },
            new Step { name = "HUD sprites",     run = HudSprites.Generate },
            new Step { name = "Menu sprites",    run = MenuSprites.Import },
            new Step { name = "Settings sprites", run = SettingsSprites.Generate },
            new Step { name = "SFX",             run = ProceduralSfx.Generate },
            new Step { name = "Effects",         run = EffectsBuilder.Build },

            // Screens and scenes.
            new Step { name = "Title screen",    run = TitleScreenBuilder.Build },
            new Step { name = "Level select",    run = LevelSelectBuilder.Build },
            new Step { name = "Settings",        run = SettingsBuilder.Build },
            new Step { name = "HUD",             run = HudBuilder.Build },
            new Step { name = "Menus",           run = MenusBuilder.Build },

            // Last: they touch the prefabs the scenes above already reference.
            new Step { name = "Gameplay",        run = GameplayBuilder.Build },
            new Step { name = "Speed feel",      run = SpeedFeelBuilder.Build },

            // Checks.
            new Step { name = "Verify gameplay", run = GameplayVerify.Verify },
            new Step { name = "Audit scenes",    run = SceneAudit.Audit },
        };

        int failed = 0;

        foreach (var step in steps)
        {
            try
            {
                step.run();
            }
            catch (Exception e)
            {
                failed++;
                Debug.LogError("[BuildAll] " + step.name + " threw: " + e);
            }
        }

        AssetDatabase.SaveAssets();

        if (failed == 0) Debug.Log("[BuildAll] All " + steps.Length + " steps completed.");
        else Debug.LogError("[BuildAll] " + failed + " of " + steps.Length + " steps threw.");
    }
}
