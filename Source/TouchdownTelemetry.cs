using System;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// Landing log for the active vessel, whoever flies it: below StartHeight of gear height it writes a
    /// sample every SamplePeriod, plus a line at every ground contact and lift-off, until AfterContact
    /// seconds on the ground. Used to compare hand-flown and automatic landings. The same numbers are
    /// shown in green at the top of the screen, under the altimeter.
    /// </summary>
    internal sealed class TouchdownTelemetry
    {
        const string Tag = "[ApproachAutopilot] tlm ";
        const double StartHeight = 100.0;   // m of gear height to start recording
        const double ArmHeight = 120.0;     // m to climb above before the next recording
        const double SamplePeriod = 0.25;   // s
        const double AfterContact = 5.0;    // s on the ground before the recording ends
        const double Rad2Deg = 180.0 / Math.PI;

        Guid vessel_id;
        bool armed, recording, on_ground;
        double t, next_sample, ground_time;
        int contacts;
        double first_contact_vs;
        double last_pitch;
        bool has_pitch;

        // on-screen copy: live sample, last contact, summary kept ShowAfter seconds after the end
        const float ShowAfter = 10.0f;
        string screen_sample = "", screen_contact = "";
        float show_until;
        static GUIStyle style, shadow;

        public void FixedUpdate(Vessel v, ApproachController apr, double known_gear)
        {
            if (v.id != vessel_id)
            {
                vessel_id = v.id;
                recording = has_pitch = false;
                armed = !v.LandedOrSplashed;    // a quickload on short final records at once
            }
            if (v.packed)
                return;

            double dt = TimeWarp.fixedDeltaTime;
            double root = ApproachController.RootAboveGround(v);
            bool gear_known = apr != null && apr.gear_height_set;
            double gear = gear_known ? apr.gear_height : known_gear;
            double h = root >= 0.0 ? root - gear : double.PositiveInfinity;
            bool landed = v.LandedOrSplashed;

            Vector3 up = FlightGlobals.getUpAxis(v.mainBody, v.vesselTransform.position);
            double pitch = Math.Asin(Mathf.Clamp(Vector3.Dot(v.ReferenceTransform.up, up), -1.0f, 1.0f)) * Rad2Deg;
            double q = has_pitch && dt > 0.0 ? (pitch - last_pitch) / dt : 0.0;
            last_pitch = pitch;
            has_pitch = true;

            if (!recording)
            {
                if (!landed && h > ArmHeight)
                    armed = true;
                if (!armed || landed || h >= StartHeight)
                    return;
                recording = true;
                armed = on_ground = false;
                t = next_sample = 0.0;
                contacts = 0;
                screen_contact = "";
                Debug.Log(Tag + "start: " + v.vesselName + ", gear height " + gear.ToString("0.00") + " m (" +
                    (gear_known ? "APR" : known_gear > 0.0 ? "launch" : "unknown") + "), sign: pitch/q/aoa + = nose up, pcmd as KSP pitch input");
            }

            t += dt;
            if (landed != on_ground)
            {
                on_ground = landed;
                if (landed)
                {
                    contacts++;
                    ground_time = 0.0;
                    if (contacts == 1)
                        first_contact_vs = v.verticalSpeed;
                    Debug.Log(Tag + Line(v, apr, "CONTACT " + contacts, h, pitch, q));
                    screen_contact = "CONTACT " + contacts + "   V/S " + v.verticalSpeed.ToString("0.00") + "   spd " + v.srfSpeed.ToString("0") +
                        "   pitch " + pitch.ToString("+0.0;-0.0") + "   q " + q.ToString("+0.0;-0.0");
                }
                else
                    Debug.Log(Tag + Line(v, apr, "AIRBORNE", h, pitch, q));
            }
            if (on_ground)
                ground_time += dt;

            if (t >= next_sample)
            {
                next_sample += SamplePeriod;
                Debug.Log(Tag + Line(v, apr, apr == null ? "-" : apr.state.ToString(), h, pitch, q));
                screen_sample = "h " + (double.IsInfinity(h) ? "-" : h.ToString("0.0")) + " m   V/S " + v.verticalSpeed.ToString("0.0") +
                    "   spd " + v.srfSpeed.ToString("0") + "   pitch " + pitch.ToString("+0.0;-0.0") + "   AoA " + PitchAoA(v).ToString("+0.0;-0.0") +
                    "   thr " + (v.ctrlState.mainThrottle * 100.0f).ToString("0") + "%" +
                    (apr != null && apr.state == ApproachController.ApproachState.Flare ? "   V/S cmd " + apr.desired_vspeed.ToString("0.0") : "");
            }

            if (on_ground && ground_time >= AfterContact)
            {
                Debug.Log(Tag + "end: " + contacts + " contact(s), first at V/S " + first_contact_vs.ToString("0.00") + " m/s");
                recording = false;
                screen_sample = "touchdown: " + contacts + " contact(s), first at V/S " + first_contact_vs.ToString("0.00") + " m/s";
                show_until = Time.realtimeSinceStartup + ShowAfter;
            }
            else if (!on_ground && h > ArmHeight)
            {
                Debug.Log(Tag + "end: climbing away, " + contacts + " contact(s)");
                recording = false;
                armed = true;
                screen_sample = "climbing away, " + contacts + " contact(s)";
                show_until = Time.realtimeSinceStartup + ShowAfter;
            }
        }

        /// <summary>Green text centered under the altimeter, while recording and for a while after.</summary>
        public void OnGUI()
        {
            if (!recording && Time.realtimeSinceStartup > show_until)
                return;
            float scale = GameSettings.UI_SCALE > 0.0f ? GameSettings.UI_SCALE : 1.0f;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.UpperCenter;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = new Color(0.3f, 1.0f, 0.3f);
                shadow = new GUIStyle(style);
                shadow.normal.textColor = Color.black;
            }
            style.fontSize = shadow.fontSize = Mathf.RoundToInt(13.0f * scale);
            string text = screen_contact.Length > 0 ? screen_sample + "\n" + screen_contact : screen_sample;
            Rect r = new Rect(0.0f, 80.0f * scale, Screen.width, 60.0f * scale);
            GUI.Label(new Rect(r.x + 1.0f, r.y + 1.0f, r.width, r.height), text, shadow);
            GUI.Label(r, text, style);
        }

        string Line(Vessel v, ApproachController apr, string what, double h, double pitch, double q)
        {
            return t.ToString("0.00").PadLeft(6) + " " + what +
                "  h " + (double.IsInfinity(h) ? "-" : h.ToString("0.0")) +
                "  vs " + v.verticalSpeed.ToString("0.00") +
                "  spd " + v.srfSpeed.ToString("0.0") +
                "  pitch " + pitch.ToString("+0.0;-0.0") +
                "  q " + q.ToString("+0.0;-0.0") +
                "  aoa " + PitchAoA(v).ToString("+0.0;-0.0") +
                "  thr " + (v.ctrlState.mainThrottle * 100.0f).ToString("0") +
                "  pcmd " + v.ctrlState.pitch.ToString("+0.00;-0.00") +
                (apr != null && apr.state == ApproachController.ApproachState.Flare ? "  vscmd " + apr.desired_vspeed.ToString("0.00") : "");
        }

        /// <summary>Pitch angle of attack in degrees, measured as AtmosphereAutopilot does (nose = transform up, belly = forward).</summary>
        static double PitchAoA(Vessel v)
        {
            Transform rt = v.ReferenceTransform;
            Vector3 vel = v.srf_velocity;
            Vector3 proj = Vector3.Project(vel, rt.up) + Vector3.Project(vel, rt.forward);
            if (proj.sqrMagnitude < 1.0f)
                return 0.0;
            double aoa = Math.Asin(Mathf.Clamp(Vector3.Dot(rt.forward, proj.normalized), -1.0f, 1.0f)) * Rad2Deg;
            return Vector3.Dot(proj, rt.up) < 0.0f ? 180.0 - aoa : aoa;
        }
    }
}
