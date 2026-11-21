using System;
using System.Collections.Generic;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// Which landing gear wheels of a vessel touch the ground, read from the stock wheel modules
    /// (landing legs excluded). A vessel without stock wheels has Count 0: the caller then falls
    /// back on the vessel's own landed flag.
    /// </summary>
    internal sealed class WheelContact
    {
        readonly List<ModuleWheelBase> wheels = new List<ModuleWheelBase>();
        Guid vessel_id;
        int part_count = -1;

        public int Count { get { return wheels.Count; } }
        public int Grounded { get; private set; }

        public void Update(Vessel v)
        {
            if (v.id != vessel_id || v.parts.Count != part_count)
            {
                vessel_id = v.id;
                part_count = v.parts.Count;
                wheels.Clear();
                foreach (ModuleWheelBase w in v.FindPartModulesImplementing<ModuleWheelBase>())
                    if (w.wheelType != WheelType.LEG)
                        wheels.Add(w);
            }
            int n = 0;
            for (int i = 0; i < wheels.Count; i++)
                if (wheels[i] != null && wheels[i].isGrounded)
                    n++;
            Grounded = n;
        }

        /// <summary>On its wheels, or simply landed when the vessel has no stock wheels.</summary>
        public bool OnWheels(Vessel v) { return Count > 0 ? Grounded > 0 : v.LandedOrSplashed; }
    }

    /// <summary>
    /// Control laws for an aircraft rolling on its wheels, where AtmosphereAutopilot commands nothing:
    /// the elevator lowers the nose at a set rate starting from the command it had in flight, the
    /// ailerons keep the wings level. Kept apart from the approach state machine: the caller feeds the
    /// attitude and the control state every physics frame.
    /// </summary>
    internal sealed class GroundLaws
    {
        const double Deg2Rad = Math.PI / 180.0;
        const double Rad2Deg = 180.0 / Math.PI;

        const int RingSize = 64;
        const double TrimWindow = 0.5;          // s of flight command averaged into the starting elevator
        const double DerotationDelay = 0.3;     // s on the wheels before the nose starts down
        const double NoseDownMargin = 0.5;      // deg above the parked pitch that counts as nose down
        const double NoseDownTime = 0.3;        // s
        const double SteadyRate = 0.5;          // deg/s, pitch rate of a nose that has stopped
        const double SteadyTime = 1.0;          // s
        const double RestMargin = 2.0;          // deg above the parked pitch where a stopped nose is still down
        const double InputStop = 0.99;          // |input| counted as the elevator at its stop
        const double MinDynPressure = 60.0;     // below this AtmosphereAutopilot's aero model is void
        const double MinAuthority = 1e-4;       // same threshold AtmosphereAutopilot uses
        const double MaxScale = 1.25;
        const double MaxInputRate = 4.0;        // control units per second
        const double FadeTime = 2.0;            // s, fallback: starting elevator faded out
        const double FallbackRateGain = 1.0;    // fallback: control units per rad/s of pitch rate

        // tuning, set by the owner
        public double kp = 4.0;                 // 1/s2
        public double kd = 4.0;                 // 1/s
        public double ki = 0.0;                 // 1/s3
        public double derotation_rate = 2.0;    // deg/s
        public double ground_pitch;             // deg, pitch when parked on all wheels
        public bool ground_pitch_known;

        // last frame flown in the air
        readonly float[] ring = new float[RingSize];
        int ring_head, ring_count;
        float flight_pitch_cmd, flight_roll_cmd;
        double pitch_authority, roll_authority, flight_dyn_pressure;

        // on the ground
        double trim, integral, contact_time, ground_time, down_time, steady_time;
        float pitch_out, roll_out;

        public double PitchRef { get; private set; }
        public double Trim { get { return trim; } }
        public bool ModelValid { get; private set; }
        public bool NoseDown { get { return down_time >= NoseDownTime || steady_time >= SteadyTime; } }
        /// <summary>Nose down called because the nose stopped moving, not because it reached the parked pitch.</summary>
        public bool NoseStopped { get { return down_time < NoseDownTime && steady_time >= SteadyTime; } }
        public bool Lowering { get { return contact_time >= DerotationDelay; } }

        public void Reset() { ring_head = ring_count = 0; }

        /// <summary>
        /// Called every frame the autopilot flies in the air, after it wrote its commands: authorities
        /// are rad/s2 of angular acceleration per unit of control input, positive like the input.
        /// </summary>
        public void RecordFlight(FlightCtrlState cntrl, double pitch_auth, double roll_auth, double dyn_pressure)
        {
            ring[ring_head] = cntrl.pitch;
            ring_head = (ring_head + 1) % RingSize;
            ring_count = Math.Min(ring_count + 1, RingSize);
            flight_pitch_cmd = cntrl.pitch;
            flight_roll_cmd = cntrl.roll;
            pitch_authority = pitch_auth;
            roll_authority = roll_auth;
            flight_dyn_pressure = dyn_pressure;
        }

        /// <summary>First wheel contact: the laws start from the attitude and the commands of that moment.</summary>
        public void Begin(double pitch_deg, double dt)
        {
            int n = Math.Min(ring_count, Math.Max(1, (int)Math.Round(TrimWindow / Math.Max(dt, 0.001))));
            double sum = 0.0;
            for (int i = 1; i <= n; i++)
                sum += ring[(ring_head - i + RingSize) % RingSize];
            trim = n > 0 ? sum / n : 0.0;
            pitch_out = ring_count > 0 ? flight_pitch_cmd : 0.0f;
            roll_out = ring_count > 0 ? flight_roll_cmd : 0.0f;
            PitchRef = pitch_deg;
            integral = contact_time = ground_time = down_time = steady_time = 0.0;
        }

        /// <summary>
        /// Writes pitch and roll. Angles in degrees (pitch + = nose up, roll + = right wing down), rates in
        /// rad/s with the same signs. An axis the pilot is deflecting is left alone for that frame.
        /// all_wheels: every wheel is on the ground, the only sign of a lowered nose when the parked pitch is unknown.
        /// </summary>
        public void Apply(FlightCtrlState cntrl, double pitch, double pitch_rate, double roll, double roll_rate,
            double dyn_pressure, bool on_wheels, bool all_wheels, double dt)
        {
            ground_time += dt;
            contact_time = on_wheels ? contact_time + dt : 0.0;
            bool lowering = contact_time >= DerotationDelay;
            if (lowering)
            {
                double step = Math.Max(derotation_rate, 0.0) * dt;
                if (!ground_pitch_known)
                    PitchRef -= step;
                else if (PitchRef > ground_pitch)
                    PitchRef = Math.Max(PitchRef - step, ground_pitch);
                else
                    PitchRef = Math.Min(PitchRef + step, ground_pitch);
            }

            bool aero = flight_dyn_pressure >= MinDynPressure && dyn_pressure >= MinDynPressure;
            double scale = aero ? Math.Min(dyn_pressure / flight_dyn_pressure, MaxScale) : 0.0;

            // pitch
            double error = (PitchRef - pitch) * Deg2Rad;
            double u;
            ModelValid = aero && pitch_authority >= MinAuthority;
            if (ModelValid)
            {
                bool saturated = Math.Abs(pitch_out) >= 1.0f;
                if (ki > 0.0 && !saturated)
                    integral += ki * error * dt;
                else if (ki <= 0.0)
                    integral = 0.0;
                u = trim * scale + (kp * error - kd * pitch_rate + integral) / (pitch_authority * scale);
            }
            else
                u = trim * Math.Max(0.0, 1.0 - ground_time / FadeTime) - FallbackRateGain * pitch_rate;
            pitch_out = Output(cntrl.pitch, cntrl.pitchTrim, pitch_out, u, dt);
            cntrl.pitch = pitch_out;

            // roll: wings level while the ailerons still bite
            if (aero && roll_authority >= MinAuthority)
            {
                double r = (-kp * roll * Deg2Rad - kd * roll_rate) / (roll_authority * scale);
                roll_out = Output(cntrl.roll, cntrl.rollTrim, roll_out, r, dt);
                cntrl.roll = roll_out;
            }
            else
                roll_out = cntrl.roll;

            // nose down: at the parked pitch, or stopped where the elevator can take it no lower (resting a
            // little above the parked pitch, elevator at its nose-down stop, or every wheel on the ground)
            bool at_ground_pitch = ground_pitch_known && pitch <= ground_pitch + NoseDownMargin;
            down_time = on_wheels && at_ground_pitch ? down_time + dt : 0.0;
            bool ref_done = ground_pitch_known && Math.Abs(PitchRef - ground_pitch) < 1e-6;
            bool resting = (ref_done && pitch <= ground_pitch + RestMargin) || pitch_out <= -InputStop ||
                (!ground_pitch_known && all_wheels);
            bool stopped = Math.Abs(pitch_rate) * Rad2Deg < SteadyRate && resting;
            steady_time = on_wheels && lowering && stopped ? steady_time + dt : 0.0;
        }

        /// <summary>Rate-limited command, or the pilot's own input when the axis is being deflected.</summary>
        static float Output(float input, float input_trim, float last, double wanted, double dt)
        {
            if (input != input_trim)
                return Mathf.Clamp(input, -1.0f, 1.0f);
            double step = MaxInputRate * dt;
            double u = Math.Max(-1.0, Math.Min(1.0, wanted));
            if (double.IsNaN(u))
                u = last;
            return (float)(last + Math.Max(-step, Math.Min(step, u - last)));
        }
    }
}
