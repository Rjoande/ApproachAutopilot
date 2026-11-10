using System;
using System.Collections.Generic;
using AtmosphereAutopilot;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// Per-frame flight addon for the Approach controller of the active vessel. AtmosphereAutopilot
    /// updates only the controller in the seat, while an armed APR watches for capture from the
    /// background and its hotkey must work whatever autopilot is flying. It also measures the gear
    /// height at launch, when AA may not have created the controller yet.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ApproachAddon : MonoBehaviour
    {
        const string Tag = "[ApproachAutopilot] ";

        void Awake()
        {
            if (OldVersionCheck.Disabled)
                Destroy(this);
        }

        void Update()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            global::AtmosphereAutopilot.AtmosphereAutopilot aa = global::AtmosphereAutopilot.AtmosphereAutopilot.Instance;
            if (v == null || aa == null)
                return;
            WatchLaunch(v);
            ApproachController apr = GetController(aa.getVesselModules(v));
            if (apr == null)
                return;
            double h;
            if (launch_gear.TryGetValue(v.id, out h))
            {
                launch_gear.Remove(v.id);
                apr.SetGearHeight(h, "measured at launch");
            }

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

        // Gear height measured on the launch site, waiting for the vessel's controller to exist.
        readonly Dictionary<Guid, double> launch_gear = new Dictionary<Guid, double>();
        readonly HashSet<Guid> measured = new HashSet<Guid>();
        float still_time;

        const float SettleTime = 1.0f;      // s parked and settled before measuring

        /// <summary>Parked on the launch site the gear is exactly at zero: the root part height above the ground is the gear height.</summary>
        void WatchLaunch(Vessel v)
        {
            if (measured.Contains(v.id))
                return;
            if (v.situation != Vessel.Situations.PRELAUNCH || v.packed || v.precalc == null || v.precalc.isEasingGravity || v.srfSpeed > 0.1)
            {
                still_time = 0.0f;
                return;
            }
            still_time += Time.deltaTime;
            if (still_time < SettleTime)
                return;
            double h = ApproachController.RootAboveGround(v);
            if (h < 0.0)
                return;
            measured.Add(v.id);
            launch_gear[v.id] = h;
            Debug.Log(Tag + "gear height at launch: " + h.ToString("0.00") + " m (" + v.vesselName + ")");
        }

        static ApproachController GetController(Dictionary<Type, AutopilotModule> modules)
        {
            AutopilotModule m;
            return modules != null && modules.TryGetValue(typeof(ApproachController), out m) ? m as ApproachController : null;
        }
    }
}
