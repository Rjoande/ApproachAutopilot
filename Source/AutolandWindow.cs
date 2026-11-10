using System;
using AtmosphereAutopilot;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// Autoland settings, opened from the Approach window: flare (Air Force style) or none (Navy style),
    /// RETARD idle, flare tuning and the gear height, measured on the ground. Drawn by ApproachController
    /// after its own window; everything is stored on the controller.
    /// </summary>
    internal sealed class AutolandWindow
    {
        // Window id must be unique among every IMGUI window in the game: "AAPL" as bytes.
        const int WindowId = 0x4141504C;
        const float Width = 300.0f;
        const double ParkedSpeed = 0.5;     // m/s, slowest speed that still counts as moving

        readonly ApproachController apr;
        Rect window = new Rect(0.0f, 0.0f, Width, 100.0f);
        bool placed;
        public bool shown;
        bool show_advanced, shrink;

        public AutolandWindow(ApproachController controller) { apr = controller; }

        public float Left { get { return window.x; } set { window.x = value; placed = true; } }
        public float Top { get { return window.y; } set { window.y = value; placed = true; } }

        /// <summary>Draws the window left of the controller's one; called from its OnGUICustom.</summary>
        public void OnGUI(Rect main)
        {
            if (!shown)
                return;
            if (!placed)
            {
                window.x = main.xMin - Width - 5.0f;
                window.y = main.yMin;
                placed = true;
            }
            window.x = Common.Clampf(window.x, 5.0f - window.width, Screen.width - 5.0f);
            window.y = Common.Clampf(window.y, 5.0f - window.height, Screen.height - 5.0f);
            window = GUILayout.Window(WindowId, window, DrawWindow, "Autoland");
            if (shrink)
            {
                shrink = false;
                window.height = 0.0f;
            }
        }

        void DrawWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16.0f, 1.0f, 15.0f, 16.0f), "x", GUIStyles.toggleButtonStyle))
                shown = false;
            Vessel v = apr.Vessel;
            GUILayout.BeginVertical();
            apr.autoland = GUILayout.Toggle(apr.autoland, apr.AutolandText(), GUIStyles.toggleButtonStyle);
            apr.flare = GUILayout.Toggle(apr.flare, apr.flare ? "Flare: on (Air Force style)" : "Flare: off (Navy style)", GUIStyles.toggleButtonStyle);
            if (apr.flare)
            {
                ApproachController.DrawNumber("touchdown sink (m/s)", ref apr.touchdown_sink, "G3");
                ApproachController.DrawNumber("flare time const (s)", ref apr.flare_tau, "G3");
            }
            ApproachController.DrawNumber("RETARD idle throttle (%)", ref apr.idle_throttle, "G3");
            if (!apr.apr_speed_control)
                GUILayout.Label("  RETARD needs APR speed control", GUIStyles.labelStyleLeft);

            GUILayout.Space(6.0f);
            double gear = apr.gear_height;
            ApproachController.DrawNumber("gear height (m)", ref apr.gear_height, "0.00");
            if (apr.gear_height != gear)
                apr.gear_height_set = true;
            bool parked = v.LandedOrSplashed && v.srfSpeed < ParkedSpeed && apr.ground_dist >= 0.0;
            GUI.enabled = parked;
            if (GUILayout.Button(parked ? "Set gear height: " + apr.ground_dist.ToString("0.00") + " m" : "Set gear height (park first)", GUIStyles.toggleButtonStyle))
            {
                apr.SetGearHeight(apr.ground_dist, "set by the pilot");
                MessageManager.post_status_message("APR: gear height " + apr.ground_dist.ToString("0.00") + " m");
            }
            GUI.enabled = true;
            if (!apr.gear_height_set)
                GUILayout.Label("gear height not measured: park and press Set", GUIStyles.labelStyleLeft);

            GUILayout.Space(6.0f);
            GUILayout.Label(StatusText(v), GUIStyles.labelStyleLeft);

            bool was_advanced = show_advanced;
            show_advanced = GUILayout.Toggle(show_advanced, "Advanced", GUIStyles.toggleButtonStyle);
            if (show_advanced)
            {
                ApproachController.DrawNumber("final intercept (deg)", ref apr.final_intercept_deg, "G3");
                ApproachController.DrawNumber("flare gain (1/s)", ref apr.flare_gain, "G3");
                ApproachController.DrawNumber("flare height min (m)", ref apr.flare_h_min, "G3");
                ApproachController.DrawNumber("flare height max (m)", ref apr.flare_h_max, "G3");
                ApproachController.DrawNumber("touchdown confirm (s)", ref apr.touchdown_confirm, "G3");
            }
            else if (was_advanced)
                shrink = true;
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        string StatusText(Vessel v)
        {
            if (v.LandedOrSplashed)
                return apr.ground_dist < 0.0 ? "no ground found below" : "gear above ground " + (apr.ground_dist - apr.gear_height).ToString("0.0") + " m";
            if (NavBridge.SelectedRunway == null)
                return "no runway tuned";
            string text = "gear above rwy " + apr.gear_above.ToString("0.0") + " m";
            if (apr.state == ApproachController.ApproachState.Flare)
                return text + "   V/S cmd " + apr.desired_vspeed.ToString("+0.0;-0.0");
            if (apr.flare)
                return text + "   flare at " + apr.FlareHeight().ToString("0.0") + " m";
            return text;
        }
    }
}
