using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// Main-menu check for the DLL this mod was called before the rename (AtmosphereApproach). Both would
    /// register an "Approach controller" in AtmosphereAutopilot, so this one steps aside: it takes itself
    /// out of AA's module and hotkey lists and asks the player to delete the old folder.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class OldVersionCheck : MonoBehaviour
    {
        const string Tag = "[ApproachAutopilot] ";
        const string OldName = "AtmosphereApproach";

        /// <summary>True when the old DLL is installed: this one registers nothing and stays idle.</summary>
        internal static bool Disabled { get; private set; }

        string old_version;
        string old_folder;

        void Awake()
        {
            foreach (AssemblyLoader.LoadedAssembly la in AssemblyLoader.loadedAssemblies)
            {
                if (la.assembly == null || la.assembly.GetName().Name != OldName)
                    continue;
                Version v = la.assembly.GetName().Version;
                old_version = v.Major + "." + v.Minor + "." + v.Build;
                string url = la.url ?? "";
                old_folder = "GameData/" + (url.Length > 0 ? url.Split('/', '\\')[0] : OldName);
                Disabled = true;
                break;
            }
            if (!Disabled)
            {
                Destroy(this);
                return;
            }
            Debug.LogWarning(Tag + "old version detected: " + OldName + " " + old_version + " in " + old_folder +
                ". Approach Autopilot is disabled until that folder is deleted.");
        }

        IEnumerator Start()
        {
            if (!Disabled)
                yield break;
            // AtmosphereAutopilot collects its modules in its own Start and sets Instance when done
            while (global::AtmosphereAutopilot.AtmosphereAutopilot.Instance == null)
                yield return null;
            Unregister(global::AtmosphereAutopilot.AtmosphereAutopilot.Instance);
            yield return null;
            ShowDialog();
        }

        static void Unregister(object aa)
        {
            Assembly mine = typeof(OldVersionCheck).Assembly;
            const BindingFlags Field = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo types_field = aa.GetType().GetField("autopilot_module_types", Field);
            List<Type> types = types_field != null ? types_field.GetValue(aa) as List<Type> : null;
            int modules = types != null ? types.RemoveAll(t => t.Assembly == mine) : -1;

            int hotkeys = -1;
            FieldInfo hk_field = aa.GetType().GetField("hotkeyManager", Field);
            object hk = hk_field != null ? hk_field.GetValue(aa) : null;
            FieldInfo map_field = hk != null ? hk.GetType().GetField("hotkey_map", Field) : null;
            IList map = map_field != null ? map_field.GetValue(hk) as IList : null;
            if (map != null)
            {
                hotkeys = 0;
                for (int i = map.Count - 1; i >= 0; i--)
                {
                    FieldInfo entry = map[i].GetType().GetField("field", Field);
                    FieldInfo hotkey = entry != null ? entry.GetValue(map[i]) as FieldInfo : null;
                    if (hotkey != null && hotkey.DeclaringType.Assembly == mine)
                    {
                        map.RemoveAt(i);
                        hotkeys++;
                    }
                }
            }
            Debug.Log(Tag + "removed from AtmosphereAutopilot: " + modules + " module(s), " + hotkeys + " hotkey(s)" +
                (modules < 0 || hotkeys < 0 ? " (AtmosphereAutopilot internals not found)" : ""));
        }

        void ShowDialog()
        {
            string text = "Old version detected: " + OldName + " " + old_version + ", in " + old_folder + ".\n\n" +
                "AtmosphereApproach is now Approach Autopilot. While the old folder is installed, Approach Autopilot " +
                "stays disabled and the old version keeps flying: the features added after v1.0 are not available.\n\n" +
                "Quit KSP, delete " + old_folder + " and restart.";
            PopupDialog.SpawnPopupDialog(
                new MultiOptionDialog("ApproachAutopilotOldVersion", text, "Approach Autopilot",
                    HighLogic.UISkin, 480f,
                    new DialogGUIButton("OK", () => { }, true)),
                false, HighLogic.UISkin);
        }
    }
}
