using System;
using System.Collections.Generic;
using AtmosphereAutopilot;
using UnityEngine;

namespace AtmosphereApproach
{
    /// <summary>
    /// Per-frame flight addon for the Approach controller of the active vessel. AtmosphereAutopilot
    /// updates only the controller in the seat, while an armed APR watches for capture from the
    /// background and its hotkey must work whatever autopilot is flying.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ApproachAddon : MonoBehaviour
    {
        const string Tag = "[AtmosphereApproach] ";

        void Update()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            global::AtmosphereAutopilot.AtmosphereAutopilot aa = global::AtmosphereAutopilot.AtmosphereAutopilot.Instance;
            if (v == null || aa == null)
                return;
            ApproachController apr = GetController(aa.getVesselModules(v));
            if (apr == null)
                return;

            if (!apr.Active)
                apr.BackgroundUpdate();
            else if (apr.bounce_pending)
                apr.Bounce();

            KeyCode key = ApproachController.apr_arm_key;
            if (key == KeyCode.None || !Input.GetKeyDown(key))
                return;
            if (FlightDriver.Pause || !InputLockManager.IsUnlocked(ControlTypes.KEYBOARDINPUT))
                return;
            apr.Armed = !apr.Armed;
            Debug.Log(Tag + "APR hotkey fired (" + key + "), armed = " + apr.Armed);
        }

        static ApproachController GetController(Dictionary<Type, AutopilotModule> modules)
        {
            AutopilotModule m;
            return modules != null && modules.TryGetValue(typeof(ApproachController), out m) ? m as ApproachController : null;
        }
    }
}
