using System;
using System.Collections.Generic;
using AtmosphereAutopilot;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// APR mode: arms on the runway tuned in NavInstruments, captures localizer then glideslope and
    /// flies the approach down to the decision height, where it hands the aircraft back to the pilot.
    /// Listed by AtmosphereAutopilot as one of its high-level autopilots. Armed, it only watches
    /// from the background while Cruise Flight or Fly-By-Wire keeps flying; at localizer capture it
    /// selects itself in the manager and steers through AA's Director controller.
    /// </summary>
    public sealed class ApproachController : StateController
    {
        const string Tag = "[ApproachAutopilot] ";
        const double Deg2Rad = Math.PI / 180.0;
        const double Rad2Deg = 180.0 / Math.PI;

        // Window id must be unique among every IMGUI window in the game: "AAPR" as bytes.
        internal ApproachController(Vessel v) : base(v, "Approach controller", 0x41415052)
        {
            window.width = 300.0f;
            runway_list = new RunwayListWindow(v);
            autoland_window = new AutolandWindow(this);
        }

        FlightModel imodel;
        DirectorController dir_c;
        ProgradeThrustController thrust_c;
        StandardFlyByWire fbw;
        CruiseController cruise;
        TopModuleManager manager;
        readonly RunwayListWindow runway_list;
        readonly AutolandWindow autoland_window;
        internal Vessel Vessel { get { return vessel; } }

        public override void InitializeDependencies(Dictionary<Type, AutopilotModule> modules)
        {
            imodel = modules[typeof(FlightModel)] as FlightModel;
            dir_c = modules[typeof(DirectorController)] as DirectorController;
            thrust_c = modules[typeof(ProgradeThrustController)] as ProgradeThrustController;
            fbw = modules[typeof(StandardFlyByWire)] as StandardFlyByWire;
            cruise = modules[typeof(CruiseController)] as CruiseController;
            manager = modules[typeof(TopModuleManager)] as TopModuleManager;
        }

        #region State

        // Final, Flare and Touchdown: below the decision height with autoland on (Touchdown also on any
        // ground contact while APR flies). Minimums: handing the aircraft back, for one frame.
        public enum ApproachState { Off, Armed, Localizer, Glideslope, Final, Flare, Touchdown, Minimums }

        public ApproachState state = ApproachState.Off;

        /// <summary>Why APR is still waiting (shown in the window), empty when nothing blocks it.</summary>
        public string gate = "";

        public bool Armed
        {
            get { return state != ApproachState.Off; }
            set
            {
                if (value == Armed)
                    return;
                if (value)
                    Arm();
                else
                    Disarm(null);
            }
        }

        /// <summary>Arms on the tuned runway, or says why not. Repeats the message when already armed.</summary>
        void Arm()
        {
            RunwayInfo rwy = NavBridge.SelectedRunway;
            if (rwy == null || rwy.body != vessel.mainBody.name)
            {
                MessageManager.post_status_message("APR not armed: no runway tuned on " + vessel.mainBody.name);
                Debug.Log(Tag + "arm refused: no runway on " + vessel.mainBody.name);
                return;
            }
            if (!Armed)
            {
                state = ApproachState.Armed;
                gate = "";
                Debug.Log(Tag + "armed on " + rwy.shortID);
            }
            MessageManager.post_status_message("APR armed on " + rwy.shortID + " - waiting for LOC");
        }

        void Disarm(string reason)
        {
            state = ApproachState.Off;
            gate = "";
            MessageManager.post_status_message(reason == null ? "APR off" : "APR off: " + reason);
            Debug.Log(Tag + "disarmed" + (reason == null ? "" : " (" + reason + ")"));
            if (Active)
                HandBack(false);
        }

        #endregion

        #region Who flies

        // True while this module steers through the Director, from localizer capture on.
        bool steering;

        // Controller to give the seat back to (Cruise Flight or Fly-By-Wire): the one flying when
        // APR engaged or was selected by hand. Fly-By-Wire when nothing better was flying.
        Type return_to = typeof(StandardFlyByWire);

        // Engage() sets engaging so OnActivate knows the capture put this module in the seat; a manual
        // selection sets bounce_pending instead, and the flight addon hands the seat back next frame.
        bool engaging;
        internal bool bounce_pending;

        // Set around hand-backs that leave this module armed: OnDeactivate then keeps the state.
        bool keep_state;

        // The manager updates its private active_controller field only after Activate() of the new
        // controller: read during OnActivate it still names the controller that was flying.
        static readonly System.Reflection.FieldInfo manager_active_field = typeof(TopModuleManager).GetField(
            "active_controller", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        void RememberReturn()
        {
            object flying = manager_active_field != null ? manager_active_field.GetValue(manager) : null;
            return_to = flying == cruise ? typeof(CruiseController) : typeof(StandardFlyByWire);
        }

        /// <summary>Localizer captured from the background: take the manager's seat.</summary>
        void Engage()
        {
            RememberReturn();
            engaging = true;
            manager.activateAutopilot(typeof(ApproachController));
            global::AtmosphereAutopilot.AtmosphereAutopilot.Instance.mainMenuGUIUpdate();
        }

        /// <summary>Gives the seat back to the controller that flew before; the APR state survives if asked.</summary>
        void HandBack(bool stay_armed)
        {
            if (!Active)
                return;
            Debug.Log(Tag + "handing control back to " + return_to.Name + (stay_armed ? ", still armed" : ""));
            keep_state = stay_armed;
            manager.activateAutopilot(return_to);
            keep_state = false;
            global::AtmosphereAutopilot.AtmosphereAutopilot.Instance.mainMenuGUIUpdate();
        }

        /// <summary>Selected by hand: nothing to fly before capture, so the addon calls this next frame.</summary>
        internal void Bounce()
        {
            bounce_pending = false;
            HandBack(true);
        }

        void StartSteering()
        {
            if (steering)
                return;
            dir_c.Activate();
            thrust_c.Activate();
            imodel.Activate();
            steering = true;
            retard = false;
            pitch_limit_time = 0.0;
            pitch_limit_warned = false;
        }

        void StopSteering()
        {
            if (!steering)
                return;
            dir_c.Deactivate();
            thrust_c.Deactivate();
            imodel.Deactivate();
            steering = false;
        }

        #endregion

        #region Parameters

        [VesselSerializable("decision_height")]
        [AutoGuiAttr("decision height (m)", true, "G4")]
        public double decision_height = 60.0;

        [GlobalSerializable("loc_capture_deg")]
        [AutoGuiAttr("LOC capture (deg)", true, "G3")]
        public double loc_capture_deg = 2.0;

        [GlobalSerializable("gs_capture_deg")]
        [AutoGuiAttr("GS capture (deg)", true, "G3")]
        public double gs_capture_deg = 0.35;

        [GlobalSerializable("min_final_dme")]
        [AutoGuiAttr("min final DME (m)", true, "G5")]
        public double min_final_dme = 5000.0;

        [GlobalSerializable("apr_speed_control")]
        [AutoGuiAttr("APR speed control", true)]
        public bool apr_speed_control = false;

        [VesselSerializable("vref")]
        [AutoGuiAttr("speed at DH (m/s)", true, "G4")]
        public double vref = 80.0;

        [GlobalSerializable("handover_master_off")]
        [AutoGuiAttr("Minimums: master off (else FBW)", true)]
        public bool handover_master_off = false;

        // Advanced parameters: serialized like the others, drawn by hand behind the "Advanced" toggle.
        [GlobalSerializable("max_intercept_deg")]
        public double max_intercept_deg = 30.0;

        [GlobalSerializable("tau_lat")]
        public double tau_lat = 10.0;

        [GlobalSerializable("lookahead_min")]
        public double lookahead_min = 300.0;

        [GlobalSerializable("lookahead_max")]
        public double lookahead_max = 3000.0;

        [GlobalSerializable("height_gain")]
        public double height_gain = 0.15;

        [GlobalSerializable("vspeed_gain")]
        public double vspeed_gain = 0.3;

        [GlobalSerializable("fpa_margin")]
        public double fpa_margin = 3.0;

        [VesselSerializable("strength_mult")]
        public double strength_mult = 0.6;

        [GlobalSerializable("allow_gs_from_above")]
        public bool allow_gs_from_above = false;

        [GlobalSerializable("use_radar_dh")]
        public bool use_radar_dh = true;

        [GlobalSerializable("gs_speed_margin")]
        public double gs_speed_margin = 15.0;

        [GlobalSerializable("speed_decel")]
        public double speed_decel = 1.0;

        // Autoland, drawn by AutolandWindow. Flare on = "Air Force style", off = "Navy style" (no flare).
        [VesselSerializable("autoland")]
        public bool autoland = false;

        [VesselSerializable("flare")]
        public bool flare = true;

        [VesselSerializable("idle_throttle")]
        public double idle_throttle = 5.0;          // percent, RETARD target with APR speed control

        [VesselSerializable("touchdown_sink")]
        public double touchdown_sink = 0.6;         // m/s

        [VesselSerializable("flare_tau")]
        public double flare_tau = 3.0;              // s

        // Height of the root part above the ground when parked: the gear is then exactly at zero.
        [VesselSerializable("gear_height")]
        public double gear_height = 0.0;

        [VesselSerializable("gear_height_set")]
        public bool gear_height_set = false;

        [GlobalSerializable("final_intercept_deg")]
        public double final_intercept_deg = 5.0;

        [GlobalSerializable("flare_gain")]
        public double flare_gain = 1.0;

        [GlobalSerializable("flare_h_min")]
        public double flare_h_min = 3.0;

        [GlobalSerializable("flare_h_max")]
        public double flare_h_max = 25.0;

        [GlobalSerializable("touchdown_confirm")]
        public double touchdown_confirm = 1.0;

        // Aim point offsets per runway (m down the runway beyond the glideslope antenna), for runways whose
        // published antenna sits on the threshold. Stored as "body/ident:metres|..." in the global settings.
        readonly Dictionary<string, double> aim_offsets = new Dictionary<string, double>();

        [GlobalSerializable("aim_offsets")]
        public string AimOffsetsText
        {
            get
            {
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, double> kv in aim_offsets)
                    if (kv.Value != 0.0)
                        parts.Add(kv.Key + ":" + kv.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                return string.Join("|", parts.ToArray());
            }
            set
            {
                aim_offsets.Clear();
                if (string.IsNullOrEmpty(value))
                    return;
                foreach (string part in value.Split('|'))
                {
                    int colon = part.LastIndexOf(':');
                    double m;
                    if (colon > 0 && double.TryParse(part.Substring(colon + 1), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out m))
                        aim_offsets[part.Substring(0, colon)] = m;
                }
            }
        }

        static string RunwayKey(RunwayInfo rwy) { return rwy.body + "/" + rwy.ident; }

        /// <summary>Aim point offset of the tuned runway, 0 when none is set.</summary>
        internal double AimOffset()
        {
            RunwayInfo rwy = NavBridge.SelectedRunway;
            double m;
            return rwy != null && aim_offsets.TryGetValue(RunwayKey(rwy), out m) ? m : 0.0;
        }

        internal void SetAimOffset(double m)
        {
            RunwayInfo rwy = NavBridge.SelectedRunway;
            if (rwy == null)
                return;
            aim_offsets[RunwayKey(rwy)] = m;
            Debug.Log(Tag + "aim point offset " + m.ToString("0") + " m on " + RunwayKey(rwy));
        }

        const double AimRampDist = 600.0;   // m flown below minimums while the aim point moves to its offset
        double final_along;                 // along_gs when Final began

        [GlobalSerializable("autoland_x")]
        public float AutolandLeft { get { return autoland_window.Left; } set { autoland_window.Left = value; } }
        [GlobalSerializable("autoland_y")]
        public float AutolandTop { get { return autoland_window.Top; } set { autoland_window.Top = value; } }

        bool show_advanced = false;
        bool shrink_window = false;

        // Polled by ApproachAddon in every flight frame, not here: AA only updates the active module.
        [GlobalSerializable("apr_arm_key")]
        [AutoHotkeyAttr("APR arm/disarm")]
        internal static KeyCode apr_arm_key = KeyCode.None;

        // Runway list window: position and preferences, persisted with the controller's own settings.
        [GlobalSerializable("rwy_list_x")]
        public float RwyListLeft { get { return runway_list.Left; } set { runway_list.Left = value; } }
        [GlobalSerializable("rwy_list_y")]
        public float RwyListTop { get { return runway_list.Top; } set { runway_list.Top = value; } }
        [GlobalSerializable("rwy_sort")]
        public int RwySort { get { return runway_list.sort; } set { runway_list.sort = value; } }
        [GlobalSerializable("rwy_sort_desc")]
        public bool RwySortDesc { get { return runway_list.sort_desc; } set { runway_list.sort_desc = value; } }
        [GlobalSerializable("rwy_filter_dist")]
        public bool RwyFilterDist { get { return runway_list.filter_dist; } set { runway_list.filter_dist = value; } }
        [GlobalSerializable("rwy_max_dist_km")]
        public int RwyMaxDistKm { get { return runway_list.max_dist_km; } set { runway_list.max_dist_km = value; } }
        [GlobalSerializable("rwy_filter_relbrg")]
        public bool RwyFilterRelBrg { get { return runway_list.filter_relbrg; } set { runway_list.filter_relbrg = value; } }
        [GlobalSerializable("rwy_relbrg_deg")]
        public int RwyRelBrgDeg { get { return runway_list.relbrg_deg; } set { runway_list.relbrg_deg = value; } }
        [GlobalSerializable("rwy_filter_intercept")]
        public bool RwyFilterIntercept { get { return runway_list.filter_intercept; } set { runway_list.filter_intercept = value; } }
        [GlobalSerializable("rwy_intercept_deg")]
        public int RwyInterceptDeg { get { return runway_list.intercept_deg; } set { runway_list.intercept_deg = value; } }

        #endregion

        #region Lifecycle

        protected override void OnActivate()
        {
            NavBridge.EnsureNavAids();
            if (engaging)
            {
                engaging = false;
                StartSteering();
                Debug.Log(Tag + "engaged, flying the approach");
                return;
            }
            // selected by hand from the manager: arm if a runway is tuned, then give the seat back
            RememberReturn();
            Arm();
            bounce_pending = true;
        }

        protected override void OnDeactivate()
        {
            StopSteering();
            engaging = false;
            bounce_pending = false;
            if (!keep_state && state != ApproachState.Off)
            {
                state = ApproachState.Off;
                gate = "";
                MessageManager.post_status_message("APR off");
                Debug.Log(Tag + "disarmed: controller changed");
            }
        }

        /// <summary>Per-frame work out of the seat: nav numbers for the window, capture watch when armed.</summary>
        internal void BackgroundUpdate()
        {
            UpdateNavigation();
            if (Armed && !vessel.LandedOrSplashed)
                UpdateState();
        }

        #endregion

        #region Navigation

        NavData nav;
        double track;               // ground track, degrees
        double intercept;           // track - runway course, degrees, -180..180
        double height_above_runway;
        double cross_track;         // meters from the extended centerline, + = right of course
        Vector3d up, runway_dir, runway_right;

        internal double ground_dist = -1.0;     // root part to the ground below, m, -1 when out of reach
        internal double gear_above;             // gear above the runway: ground below if at runway level, else runway elevation, m

        void UpdateNavigation()
        {
            ground_dist = RootAboveGround(vessel);
            nav = NavBridge.Update();
            RunwayInfo rwy = NavBridge.SelectedRunway;
            if (!nav.valid || rwy == null)
                return;
            up = (vessel.ReferenceTransform.position - vessel.mainBody.position).normalized;
            Vector3d hv = Vector3d.Exclude(up, vessel.srf_velocity);
            track = Wrap360(Math.Atan2(Vector3d.Dot(hv, vessel.east), Vector3d.Dot(hv, vessel.north)) * Rad2Deg);
            intercept = Wrap180(track - nav.runwayHeading);
            height_above_runway = vessel.altitude - rwy.altMSL;

            // runway course and its right-hand normal in the local horizontal plane (course projected
            // at the vessel by NavInstruments, so no spherical correction is needed here)
            double hdg = nav.runwayHeading * Deg2Rad;
            runway_dir = Vector3d.Exclude(up, vessel.north * Math.Cos(hdg) + vessel.east * Math.Sin(hdg)).normalized;
            runway_right = Vector3d.Exclude(up, vessel.east * Math.Cos(hdg) - vessel.north * Math.Sin(hdg)).normalized;
            Vector3d loc = vessel.mainBody.GetWorldSurfacePosition(rwy.locLatitude, rwy.locLongitude, rwy.altMSL);
            cross_track = Vector3d.Dot(vessel.ReferenceTransform.position - loc, runway_right);
            Vector3d gs_ant = vessel.mainBody.GetWorldSurfacePosition(rwy.gsLatitude, rwy.gsLongitude, rwy.altMSL);
            along_gs = Vector3d.Dot(gs_ant - vessel.ReferenceTransform.position, runway_dir);

            // Radar first: the ground below is trusted within RunwayLevelTolerance of the published runway
            // elevation (which can be metres off) and whenever it is higher still (terrain_high); only ground
            // far below runway level (the sea floor, a valley) falls back to the published elevation.
            double root_alt = FlightGlobals.getAltitudeAtPos(vessel.vesselTransform.position, vessel.mainBody);
            double ground_vs_runway = root_alt - ground_dist - rwy.altMSL;
            bool use_radar = ground_dist >= 0.0 && ground_vs_runway >= -RunwayLevelTolerance;
            terrain_high = ground_dist >= 0.0 && ground_vs_runway > RunwayLevelTolerance;
            gear_above = (use_radar ? ground_dist : root_alt - rwy.altMSL) - gear_height;
        }

        const double RunwayLevelTolerance = 25.0;   // m

        internal double along_gs;   // m along the runway course to the glideslope antenna, + = antenna still ahead
        internal bool terrain_high; // ground below well above runway level

        /// <summary>
        /// Distance from the root part down to the ground along the local vertical, on the layer that holds
        /// terrain and runways (the one KSP uses for its own terrain height); -1 beyond 2 km or when nothing is hit.
        /// </summary>
        internal static double RootAboveGround(Vessel v)
        {
            Vector3 pos = v.vesselTransform.position;
            RaycastHit hit;
            if (Physics.Raycast(pos, -FlightGlobals.getUpAxis(v.mainBody, pos), out hit, 2000.0f, 1 << 15, QueryTriggerInteraction.Ignore))
                return hit.distance;
            return -1.0;
        }

        internal void SetGearHeight(double h, string source)
        {
            gear_height = h;
            gear_height_set = true;
            Debug.Log(Tag + "gear height " + h.ToString("0.00") + " m (" + source + ") on " + vessel.vesselName);
        }

        /// <summary>Gear height at which the flare starts: where the flare law meets the current sink rate.</summary>
        internal double FlareHeight()
        {
            double h = Math.Max(flare_tau, 0.5) * (-vessel.verticalSpeed - touchdown_sink);
            return Common.Clamp(h, flare_h_min, Math.Max(flare_h_min, flare_h_max));
        }

        double HeightForMinimums()
        {
            return use_radar_dh ? Math.Min(height_above_runway, vessel.radarAltitude) : height_above_runway;
        }

        /// <summary>Glideslope altitude (MSL) at the given distance from the antenna.</summary>
        double GlideslopeAltitude(RunwayInfo rwy, double dist)
        {
            return rwy.altMSL + dist * Math.Tan(nav.glideslope * Deg2Rad) + dist * dist / (2.0 * vessel.mainBody.Radius);
        }

        #endregion

        #region Speed

        double loc_capture_speed;   // m/s, base speed on the approach when AA's speed control is off

        /// <summary>Highest speed that still slows down to vref at the decision height, decelerating at speed_decel.</summary>
        double SpeedEnvelope()
        {
            // DME is measured from the glideslope antenna, where the beam starts: the beam reaches DH at DH / sin(gs) from it
            double to_dh = Math.Max(0.0, nav.dme - decision_height / Math.Sin(Math.Max(nav.glideslope, 0.5) * Deg2Rad));
            return Math.Sqrt(vref * vref + 2.0 * Math.Max(speed_decel, 0.0) * to_dh);
        }

        /// <summary>Speed to fly now: AA's setpoint (or the speed at localizer capture) capped by the envelope, never below vref.</summary>
        double TargetSpeed()
        {
            double v_base;
            if (thrust_c.spd_control_enabled)
                v_base = thrust_c.setpoint.mps();
            else if (state != ApproachState.Off && state != ApproachState.Armed)
                v_base = loc_capture_speed;
            else
                v_base = vessel.srfSpeed;
            return Math.Max(vref, Math.Min(v_base, SpeedEnvelope()));
        }

        /// <summary>Glideslope capture refused while APR flies the throttle and the energy cannot be lost by DH.</summary>
        bool TooFastForGlideslope()
        {
            return apr_speed_control && vessel.srfSpeed > SpeedEnvelope() * (1.0 + gs_speed_margin / 100.0);
        }

        /// <summary>Throttle only: AA's speed control would also work the Brakes group, which stays with the pilot.</summary>
        void ApplySpeed(FlightCtrlState cntrl)
        {
            bool use_breaks = thrust_c.use_breaks;
            thrust_c.use_breaks = false;
            thrust_c.ApplyControl(cntrl, (float)TargetSpeed());
            thrust_c.use_breaks = use_breaks;
        }

        const double RetardTime = 2.0;      // s, from the current throttle to idle

        bool retard;
        double retard_from, retard_elapsed;
        float last_throttle;                // last throttle APR commanded

        /// <summary>RETARD: throttle to idle, in RetardTime or at once (on the ground). Only when APR flies the throttle.</summary>
        void StartRetard(bool at_once)
        {
            if (!apr_speed_control)
                return;
            if (retard)
            {
                if (at_once)
                    retard_elapsed = RetardTime;
                return;
            }
            retard = true;
            retard_from = last_throttle;
            retard_elapsed = at_once ? RetardTime : 0.0;
            MessageManager.post_status_message("APR: RETARD");
            Debug.Log(Tag + "retard from " + (retard_from * 100.0).ToString("0") + "% to " + idle_throttle.ToString("0") + "%");
        }

        float RetardThrottle()
        {
            retard_elapsed += TimeWarp.fixedDeltaTime;
            double k = Math.Min(1.0, retard_elapsed / RetardTime);
            double idle = Common.Clamp(idle_throttle / 100.0, 0.0, 1.0);
            return (float)(retard_from + (idle - retard_from) * k);
        }

        #endregion

        #region State machine

        double hold_altitude;       // altitude held while on the localizer, waiting for the glideslope
        ApproachState resume_state; // state to go back to if the aircraft bounces off the ground
        double contact_time;        // s of continuous ground contact
        string handover_message = "";

        void UpdateState()
        {
            if (state == ApproachState.Off)
                return;
            RunwayInfo rwy = NavBridge.SelectedRunway;
            if (!nav.valid || rwy == null || rwy.body != vessel.mainBody.name)
            {
                Disarm("runway lost");
                return;
            }
            double loc = Math.Abs(nav.locDeviation);
            double gs = Math.Abs(nav.gsDeviation);
            if (vessel.LandedOrSplashed && (state == ApproachState.Localizer || state == ApproachState.Glideslope ||
                state == ApproachState.Final || state == ApproachState.Flare))
            {
                resume_state = state;
                contact_time = 0.0;
                state = ApproachState.Touchdown;
                Debug.Log(Tag + "ground contact in " + resume_state + " (V/S " + vessel.verticalSpeed.ToString("0.00") +
                    " m/s, spd " + vessel.srfSpeed.ToString("0") + " m/s, gear above " + gear_above.ToString("0.0") + " m)");
                if (autoland)
                    StartRetard(true);
                return;
            }
            switch (state)
            {
                case ApproachState.Armed:
                    if (nav.dme < min_final_dme)
                        gate = "too close: DME " + (nav.dme / 1000.0).ToString("0.0") + " km < " + (min_final_dme / 1000.0).ToString("0.0") + " km, fly out and re-intercept";
                    else if (Math.Abs(intercept) > 90.0)
                        gate = "intercept angle " + intercept.ToString("0") + " deg, flying away from the runway";
                    else if (loc > loc_capture_deg)
                        gate = "waiting LOC (dev " + nav.locDeviation.ToString("+0.0;-0.0") + " deg)";
                    else
                    {
                        hold_altitude = vessel.altitude;
                        loc_capture_speed = vessel.srfSpeed;
                        Transition(ApproachState.Localizer, "localizer captured, holding " + hold_altitude.ToString("0") + " m");
                        if (Active)
                            StartSteering();
                        else
                            Engage();
                    }
                    break;

                case ApproachState.Localizer:
                    if (loc > 3.0 * loc_capture_deg)
                    {
                        Transition(ApproachState.Armed, "localizer lost");
                        HandBack(true);
                    }
                    else if (gs <= gs_capture_deg && (nav.gsDeviation <= 0.0f || allow_gs_from_above))
                    {
                        if (TooFastForGlideslope())
                            TooFastGate();
                        else
                            Transition(ApproachState.Glideslope, "glideslope captured");
                    }
                    else if (nav.gsDeviation > 0.0f)
                        gate = "above GS " + nav.gsDeviation.ToString("+0.00") + " deg" +
                            (allow_gs_from_above ? "" : ": no capture from above, get below the beam");
                    else if (TooFastForGlideslope())
                        TooFastGate();
                    else
                        gate = "below GS " + nav.gsDeviation.ToString("0.00") + " deg, holding " + hold_altitude.ToString("0") + " m";
                    break;

                case ApproachState.Glideslope:
                    if (loc > 3.0 * loc_capture_deg)
                    {
                        Transition(ApproachState.Armed, "localizer lost");
                        HandBack(true);
                    }
                    else if (gs > 3.0 * gs_capture_deg)
                    {
                        hold_altitude = vessel.altitude;
                        Transition(ApproachState.Localizer, "glideslope lost, holding " + hold_altitude.ToString("0") + " m");
                    }
                    else if (HeightForMinimums() < decision_height)
                    {
                        if (!autoland)
                            HandOverNext("MINIMUMS", "MINIMUMS - autopilot disconnected");
                        else if (terrain_high)
                            HandOverNext("terrain at minimums", "TERRAIN - autopilot disconnected");
                        else
                        {
                            final_along = along_gs;
                            Transition(ApproachState.Final, "MINIMUMS, aim point +" + AimOffset().ToString("0") + " m");
                        }
                    }
                    break;

                // below the decision height the glideslope deviation is no longer watched: near the
                // antenna its angle grows large while the vertical error stays small
                case ApproachState.Final:
                case ApproachState.Flare:
                    if (loc > 3.0 * loc_capture_deg)
                        HandOverNext("localizer lost below minimums", "LOC lost - autopilot disconnected");
                    else if (terrain_high)
                        HandOverNext("terrain below minimums", "TERRAIN - autopilot disconnected");
                    else if (state == ApproachState.Final && flare && gear_above <= FlareHeight())
                    {
                        Transition(ApproachState.Flare, "FLARE");
                        StartRetard(false);
                    }
                    break;

                case ApproachState.Touchdown:
                    if (!vessel.LandedOrSplashed)
                    {
                        state = resume_state;
                        MessageManager.post_status_message("APR: bounce");
                        Debug.Log(Tag + "airborne again after " + contact_time.ToString("0.00") + " s, back to " + state);
                    }
                    else
                    {
                        contact_time += TimeWarp.fixedDeltaTime;
                        if (contact_time >= touchdown_confirm)
                            HandOverNext("TOUCHDOWN", "TOUCHDOWN - autopilot disconnected");
                    }
                    break;

                case ApproachState.Minimums:
                    break;
            }
        }

        void TooFastGate()
        {
            bool first = !gate.StartsWith("too fast");
            gate = "too fast for GS: " + vessel.srfSpeed.ToString("0") + " m/s, max " +
                (SpeedEnvelope() * (1.0 + gs_speed_margin / 100.0)).ToString("0") + ", slowing down";
            if (first)
                Debug.Log(Tag + "glideslope capture blocked, " + gate + " (DME " + nav.dme.ToString("0") + " m)");
        }

        void Transition(ApproachState next, string message)
        {
            state = next;
            gate = "";
            MessageManager.post_status_message("APR: " + message);
            Debug.Log(Tag + message + " (DME " + nav.dme.ToString("0") + " m, LOC " + nav.locDeviation.ToString("0.00") +
                ", GS " + nav.gsDeviation.ToString("0.00") + ", cross-track " + cross_track.ToString("+0;-0") +
                " m, above rwy " + height_above_runway.ToString("0") + " m, gear above " + gear_above.ToString("0.0") +
                " m, V/S " + vessel.verticalSpeed.ToString("0.0") + " m/s, spd " + vessel.srfSpeed.ToString("0") +
                " m/s, target " + TargetSpeed().ToString("0") + ")");
        }

        /// <summary>Queues the hand-over: ApplyControl performs it on the same frame.</summary>
        void HandOverNext(string reason, string message)
        {
            handover_message = message;
            Transition(ApproachState.Minimums, reason);
        }

        /// <summary>Decision height, touchdown or localizer lost below minimums: give the aircraft back as configured.</summary>
        void Handover()
        {
            MessageManager.post_status_message(handover_message);
            Debug.Log(Tag + "handover (" + handover_message + "): " + (handover_master_off ? "master off" : "Fly-By-Wire") +
                " (above rwy " + height_above_runway.ToString("0") + " m, radar " + vessel.radarAltitude.ToString("0") +
                " m, gear above " + gear_above.ToString("0.0") + " m, throttle " + (last_throttle * 100.0f).ToString("0") + "%)");
            // both paths call this module's Deactivate(), which releases the Director
            state = ApproachState.Off;
            gate = "";
            retard = false;
            if (handover_master_off)
                manager.Active = false;
            else
                manager.activateAutopilot(typeof(StandardFlyByWire));
            global::AtmosphereAutopilot.AtmosphereAutopilot.Instance.mainMenuGUIUpdate();
        }

        internal static double Wrap360(double a)
        {
            a %= 360.0;
            return a < 0.0 ? a + 360.0 : a;
        }

        internal static double Wrap180(double a)
        {
            a = Wrap360(a);
            return a > 180.0 ? a - 360.0 : a;
        }

        #endregion

        #region Control

        double desired_heading;     // degrees, shown in the window
        internal double desired_vspeed;     // m/s, shown in the window

        public override void ApplyControl(FlightCtrlState cntrl)
        {
            if (bounce_pending)
                return;
            UpdateNavigation();
            UpdateState();
            if (!Active)
                return;
            if (state == ApproachState.Minimums)
            {
                Handover();
                return;
            }
            // on the ground AA has nothing to fly with: only the throttle stays at idle after RETARD
            if (state == ApproachState.Touchdown || vessel.LandedOrSplashed)
            {
                if (retard)
                    cntrl.mainThrottle = last_throttle = RetardThrottle();
                return;
            }
            if (steering)
                Steer(cntrl);
        }

        /// <summary>
        /// Lateral: pure pursuit on the extended centerline, intercept angle limited. Vertical: altitude
        /// hold on the localizer, straight-beam tracking on the glideslope. Both handed to the Director
        /// as a velocity direction plus the acceleration Cruise Flight would ask for.
        /// </summary>
        void Steer(FlightCtrlState cntrl)
        {
            if (apr_speed_control)
            {
                if (retard)
                    cntrl.mainThrottle = RetardThrottle();
                else
                    ApplySpeed(cntrl);
                last_throttle = cntrl.mainThrottle;
            }
            bool below_minimums = state == ApproachState.Final || state == ApproachState.Flare;

            Vector3d planet2ves = vessel.ReferenceTransform.position - vessel.mainBody.position;
            Vector3d hor_v = Vector3d.Exclude(up, imodel.surface_v);
            double vh = Math.Max(hor_v.magnitude, 1.0);
            double vz = Vector3d.Dot(imodel.surface_v, up);

            // lateral
            double lookahead = Common.Clamp(tau_lat * vh, lookahead_min, lookahead_max);
            double max_psi = below_minimums ? Math.Min(final_intercept_deg, max_intercept_deg) : max_intercept_deg;
            double psi = Common.Clamp(-Math.Atan2(cross_track, lookahead) * Rad2Deg, max_psi);
            Vector3d heading_dir = runway_dir * Math.Cos(psi * Deg2Rad) + runway_right * Math.Sin(psi * Deg2Rad);
            desired_heading = Wrap360(nav.runwayHeading + psi);

            // vertical
            double margin_vz = vh * Math.Tan(fpa_margin * Deg2Rad);
            double vert_acc_mag;
            if (state == ApproachState.Localizer)
            {
                desired_vspeed = Common.Clamp(height_gain * (hold_altitude - vessel.altitude), margin_vz);
                vert_acc_mag = vspeed_gain * (desired_vspeed - vz);
            }
            else if (state == ApproachState.Flare)
            {
                // exponential flare: sink rate proportional to gear height, plus the touchdown sink rate;
                // -vz / tau is the rate of change of that command, fed forward
                double tau = Math.Max(flare_tau, 0.5);
                desired_vspeed = -(Math.Max(gear_above, 0.0) / tau + touchdown_sink);
                vert_acc_mag = -vz / tau + flare_gain * (desired_vspeed - vz);
            }
            else if (state == ApproachState.Final)
            {
                // below minimums the beam is replaced by the same straight path computed from the runway
                // geometry (the beam angles blow up near the antenna), moved down the runway by the aim
                // point offset over AimRampDist so that the change is gradual
                double tan_gs = Math.Tan(nav.glideslope * Deg2Rad);
                double offset = AimOffset();
                double k = Common.Clamp((final_along - along_gs) / AimRampDist, 0.0, 1.0);
                double shift_rate = k > 0.0 && k < 1.0 ? offset * vh / AimRampDist : 0.0;
                double path_error = height_above_runway - (along_gs + offset * k) * tan_gs;    // m, + = above the path
                desired_vspeed = (-vh + shift_rate) * tan_gs - height_gain * path_error;
                desired_vspeed = Common.Clamp(desired_vspeed, -vh * Math.Tan((nav.glideslope + fpa_margin) * Deg2Rad), margin_vz);
                vert_acc_mag = vspeed_gain * (desired_vspeed - vz);
            }
            else
            {
                double gs_error = nav.dme * Math.Tan(nav.gsDeviation * Deg2Rad);               // m, + = above the beam
                double fpa_nom = nav.glideslope + nav.dme / vessel.mainBody.Radius * Rad2Deg;  // straight beam over a curved surface
                desired_vspeed = -vh * Math.Tan(fpa_nom * Deg2Rad) - height_gain * gs_error;
                desired_vspeed = Common.Clamp(desired_vspeed, -vh * Math.Tan((nav.glideslope + fpa_margin) * Deg2Rad), margin_vz);
                vert_acc_mag = vspeed_gain * (desired_vspeed - vz);
            }

            Vector3d desired_velocity = (heading_dir * vh + up * desired_vspeed).normalized;
            Vector3d level_acc = -up * hor_v.sqrMagnitude / planet2ves.magnitude;
            Vector3d vert_acc = up * vert_acc_mag;

            double old_strength = dir_c.strength;
            if (state != ApproachState.Flare)
                dir_c.strength *= strength_mult;
            dir_c.ApplyControl(cntrl, desired_velocity, level_acc + vert_acc);
            dir_c.strength = old_strength;
            WatchPitchAuthority(cntrl);
        }

        const double PitchLimitInput = 0.95;    // |pitch input| counted as the elevator at its stop
        const double PitchLimitTime = 1.0;      // s at the stop before the warning

        double pitch_limit_time;
        bool pitch_limit_warned;
        internal bool PitchLimited { get { return pitch_limit_time >= PitchLimitTime; } }

        /// <summary>Warns once per approach when the elevator stays at its stop: no margin left for the flare.</summary>
        void WatchPitchAuthority(FlightCtrlState cntrl)
        {
            if (Math.Abs(cntrl.pitch) >= PitchLimitInput)
                pitch_limit_time += TimeWarp.fixedDeltaTime;
            else
                pitch_limit_time = 0.0;
            if (!PitchLimited || pitch_limit_warned)
                return;
            pitch_limit_warned = true;
            MessageManager.post_status_message("APR: pitch authority limit - raise speed at DH");
            Debug.Log(Tag + "pitch authority limit in " + state + ": pitch input " + cntrl.pitch.ToString("0.00") + ", AoA " +
                (imodel.AoA(0) * Rad2Deg).ToString("0.0") + " deg, spd " + vessel.srfSpeed.ToString("0") + " m/s, V/S " +
                vessel.verticalSpeed.ToString("0.0") + " m/s, above rwy " + height_above_runway.ToString("0") + " m");
        }

        #endregion

        #region GUI

        [AutoGuiAttr("Director controller GUI", true)]
        public bool DircGUI { get { return dir_c.IsShown(); } set { if (value) dir_c.ShowGUI(); else dir_c.UnShowGUI(); } }

        [AutoGuiAttr("Thrust controller GUI", true)]
        public bool PTCGUI { get { return thrust_c.IsShown(); } set { if (value) thrust_c.ShowGUI(); else thrust_c.UnShowGUI(); } }

        // Runs after GUILayout.Window has returned: a height reset made inside _drawGUI is overwritten
        // by the window's return value, so the window is shrunk here and IMGUI refits it next frame.
        protected override void OnGUICustom()
        {
            runway_list.OnGUI(window);
            autoland_window.OnGUI(window);
            if (!shrink_window)
                return;
            shrink_window = false;
            window.height = 0.0f;
        }

        public override void UnShowGUI()
        {
            base.UnShowGUI();
            runway_list.shown = false;
            autoland_window.shown = false;
        }

        protected override void _drawGUI(int id)
        {
            close_button();
            GUILayout.BeginVertical();
            if (!NavBridge.Available)
                GUILayout.Label("NavInstruments not found", GUIStyles.labelStyleLeft);
            else
                DrawNavigation();
            GUILayout.Space(8.0f);
            AutoGUI.AutoDrawObject(this);
            GUILayout.BeginHorizontal();
            autoland = GUILayout.Toggle(autoland, AutolandText(), GUIStyles.toggleButtonStyle);
            autoland_window.shown = GUILayout.Toggle(autoland_window.shown, "settings", GUIStyles.toggleButtonStyle, GUILayout.Width(70.0f));
            GUILayout.EndHorizontal();
            bool was_advanced = show_advanced;
            show_advanced = GUILayout.Toggle(show_advanced, "Advanced", GUIStyles.toggleButtonStyle);
            if (show_advanced)
                DrawAdvanced();
            else if (was_advanced)
                shrink_window = true;
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void DrawNavigation()
        {
            RunwayInfo rwy = NavBridge.SelectedRunway;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Runway", GUIStyles.labelStyleLeft, GUILayout.Width(55.0f));
            if (GUILayout.Button("<", GUIStyles.toggleButtonStyle, GUILayout.Width(25.0f)))
                NavBridge.StepRunway(-1);
            runway_list.shown = GUILayout.Toggle(runway_list.shown, rwy == null ? "no runway" : (rwy.isINSTarget ? "INS " : "") + rwy.shortID, GUIStyles.toggleButtonStyle, GUILayout.Width(90.0f));
            if (GUILayout.Button(">", GUIStyles.toggleButtonStyle, GUILayout.Width(25.0f)))
                NavBridge.StepRunway(1);
            GUILayout.EndHorizontal();
            if (rwy != null)
                GUILayout.Label("        hdg " + rwy.hdg.ToString("000") + "   elev " + rwy.altMSL.ToString("0") + " m   " + rwy.ident, GUIStyles.labelStyleLeft);

            GUILayout.BeginHorizontal();
            GUILayout.Label("GS", GUIStyles.labelStyleLeft, GUILayout.Width(55.0f));
            if (GUILayout.Button("<", GUIStyles.toggleButtonStyle, GUILayout.Width(25.0f)))
                NavBridge.StepGlideslope(-1);
            GUILayout.Label(nav.glideslope.ToString("0.0") + " deg", GUIStyles.labelStyleLeft, GUILayout.Width(90.0f));
            if (GUILayout.Button(">", GUIStyles.toggleButtonStyle, GUILayout.Width(25.0f)))
                NavBridge.StepGlideslope(1);
            GUILayout.EndHorizontal();

            GUILayout.Space(4.0f);
            Armed = GUILayout.Toggle(Armed, "APR  " + StateText(), GUIStyles.toggleButtonStyle);
            if (gate.Length > 0)
                GUILayout.Label(gate, GUIStyles.labelStyleLeft);

            if (nav.valid && rwy != null)
            {
                GUILayout.Label("DME " + (nav.dme / 1000.0).ToString("0.0") + " km   LOC " + nav.locDeviation.ToString("+0.0;-0.0") +
                    " deg   GS " + nav.gsDeviation.ToString("+0.00;-0.00") + " deg", GUIStyles.labelStyleLeft);
                DrawSpeed();
                GUILayout.Label("above rwy " + height_above_runway.ToString("0") + " m   throttle: " + ThrottleOwner(), GUIStyles.labelStyleLeft);
                GUILayout.Label("track " + track.ToString("000") + "   rwy " + nav.runwayHeading.ToString("000") + "   intercept " + intercept.ToString("+0;-0") +
                    " deg   xtk " + (Math.Abs(cross_track) / 1000.0).ToString("0.00") + (cross_track >= 0.0 ? " km R" : " km L"), GUIStyles.labelStyleLeft);
                if (state == ApproachState.Armed)
                {
                    double d = Math.Max(rwy.outerMarkerDist, min_final_dme);
                    GUILayout.Label("GS at " + (d / 1000.0).ToString("0.0") + " km: " + GlideslopeAltitude(rwy, d).ToString("0") + " m MSL   intercept hdg " +
                        Wrap360(nav.runwayHeading + (cross_track >= 0.0 ? -max_intercept_deg : max_intercept_deg)).ToString("000"), GUIStyles.labelStyleLeft);
                }
            }
            GUILayout.Label(FlyingText(), GUIStyles.labelStyleLeft);
            if (steering && PitchLimited)
                GUILayout.Label("PITCH LIMIT: elevator at its stop, raise speed at DH", RedLabel());
        }

        static GUIStyle RedLabel()
        {
            if (red_label == null)
            {
                red_label = new GUIStyle(GUIStyles.labelStyleLeft);
                red_label.normal.textColor = Color.red;
            }
            return red_label;
        }

        string StateText()
        {
            switch (state)
            {
                case ApproachState.Armed: return "ARMED";
                case ApproachState.Localizer: return "LOC captured - GS armed";
                case ApproachState.Glideslope: return "LOC + GS captured";
                case ApproachState.Final: return "FINAL - " + (flare ? "flare armed" : "no flare");
                case ApproachState.Flare: return "FLARE";
                case ApproachState.Touchdown: return "TOUCHDOWN";
                case ApproachState.Minimums: return "MINIMUMS";
                default: return "off";
            }
        }

        internal string AutolandText()
        {
            if (!autoland)
                return "Autoland off";
            return flare ? "Autoland: Air Force style" : "Autoland: Navy style";
        }

        string FlyingText()
        {
            if (!steering)
                return (cruise.Active ? cruise.ModuleName : fbw.Active ? fbw.ModuleName : "pilot") + " flies";
            if (state == ApproachState.Touchdown)
                return "on the ground: " + contact_time.ToString("0.0") + " of " + touchdown_confirm.ToString("0.0") + " s";
            if (state == ApproachState.Localizer)
                return "APR flies: hdg " + desired_heading.ToString("000") + "   hold " + hold_altitude.ToString("0") + " m   V/S " + desired_vspeed.ToString("+0.0;-0.0");
            return "APR flies: hdg " + desired_heading.ToString("000") + "   V/S " + desired_vspeed.ToString("+0.0;-0.0") + " m/s";
        }

        static GUIStyle red_label;

        /// <summary>Current and suggested speed, shown whether or not APR flies the throttle; red above Mach 1.</summary>
        void DrawSpeed()
        {
            double spd = vessel.srfSpeed;
            if (vessel.mach > 1.0)
            {
                if (red_label == null)
                {
                    red_label = new GUIStyle(GUIStyles.labelStyleLeft);
                    red_label.normal.textColor = Color.red;
                }
                GUILayout.Label("spd " + spd.ToString("0") + " m/s   M " + vessel.mach.ToString("0.00") + "   slow down", red_label);
                return;
            }
            double target = TargetSpeed();
            double error = spd - target;
            string text = Math.Abs(error) <= 3.0 ? "on speed" : error.ToString("+0;-0") + (error > 0.0 ? " fast" : " slow");
            GUILayout.Label("spd " + spd.ToString("0") + " m/s   target " + target.ToString("0") + " m/s   " + text, GUIStyles.labelStyleLeft);
        }

        string ThrottleOwner()
        {
            if (steering)
                return apr_speed_control ? "APR" : "pilot";
            return thrust_c.Active && thrust_c.spd_control_enabled ? "AA" : "pilot";
        }

        void DrawAdvanced()
        {
            DrawNumber("max intercept (deg)", ref max_intercept_deg, "G3");
            DrawNumber("lateral time const (s)", ref tau_lat, "G3");
            DrawNumber("lookahead min (m)", ref lookahead_min, "G4");
            DrawNumber("lookahead max (m)", ref lookahead_max, "G4");
            DrawNumber("height gain (1/s)", ref height_gain, "G3");
            DrawNumber("V/S gain (1/s)", ref vspeed_gain, "G3");
            DrawNumber("FPA margin (deg)", ref fpa_margin, "G3");
            DrawNumber("director strength mult", ref strength_mult, "G3");
            allow_gs_from_above = GUILayout.Toggle(allow_gs_from_above, "Allow GS capture from above", GUIStyles.toggleButtonStyle);
            use_radar_dh = GUILayout.Toggle(use_radar_dh, "Decision height on radar altitude", GUIStyles.toggleButtonStyle);
            DrawNumber("GS capture speed margin (%)", ref gs_speed_margin, "G3");
            DrawNumber("speed envelope decel (m/s2)", ref speed_decel, "G3");
        }

        internal static void DrawNumber(string label, ref double value, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUIStyles.labelStyleLeft);
            string text = GUILayout.TextField(value.ToString(format), GUIStyles.textBoxStyle);
            double parsed;
            if (double.TryParse(text, out parsed))
                value = parsed;
            GUILayout.EndHorizontal();
        }

        #endregion
    }
}
