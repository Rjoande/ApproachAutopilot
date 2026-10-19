using System;
using System.Collections.Generic;
using AtmosphereAutopilot;
using UnityEngine;

namespace ApproachAutopilot
{
    /// <summary>
    /// Dedicated window listing the runways of the current body, sortable and filterable; a click on a
    /// name tunes that runway in NavInstruments, so the HSI follows. Drawn by ApproachController right
    /// after its own window; position and preferences are persisted through the controller.
    /// </summary>
    internal sealed class RunwayListWindow
    {
        // Window id must be unique among every IMGUI window in the game: "AAPS" as bytes.
        const int WindowId = 0x41415053;
        const float Width = 320.0f;
        const float ListHeight = 190.0f;
        const float NameWidth = 80.0f, HdgWidth = 42.0f, DistWidth = 66.0f, InterceptWidth = 72.0f;
        const float FilterWidth = 96.0f, StepWidth = 25.0f, ValueWidth = 80.0f;
        const float RefreshPeriod = 1.0f;
        const double Deg2Rad = Math.PI / 180.0;
        const double Rad2Deg = 180.0 / Math.PI;

        public enum SortKey { Name, Heading, Distance, Intercept }

        readonly Vessel vessel;
        Rect window = new Rect(0.0f, 0.0f, Width, 100.0f);
        bool placed;            // false until a position is restored or the window is first opened
        public bool shown;

        // Preferences, persisted by ApproachController.
        public int sort = (int)SortKey.Distance;
        public bool sort_desc;
        public bool filter_dist, filter_relbrg, filter_intercept;
        public int max_dist_km = 100, relbrg_deg = 120, intercept_deg = 30;

        sealed class Row
        {
            public RunwayInfo rwy;
            public double dist;         // meters, great circle to the localizer
            public double rel_bearing;  // degrees from the track to the runway, + = right
            public double intercept;    // track - runway heading, degrees, -180..180
            public bool visible;
        }

        readonly List<Row> rows = new List<Row>();
        readonly List<Row> visible = new List<Row>();
        bool selected_filtered_out;
        string body_name = "";
        Vector2 scroll;
        float next_refresh;
        bool dirty = true;

        static GUIStyle rowStyle, rowSelectedStyle, numberStyle, numberSelectedStyle;

        public RunwayListWindow(Vessel v) { vessel = v; }

        public float Left { get { return window.x; } set { window.x = value; placed = true; } }
        public float Top { get { return window.y; } set { window.y = value; placed = true; } }

        /// <summary>Draws the window next to the controller's one; called from its OnGUICustom.</summary>
        public void OnGUI(Rect main)
        {
            if (!shown)
                return;
            if (!placed)
            {
                window.x = main.xMax + 5.0f;
                window.y = main.yMin;
                placed = true;
            }
            window.x = Common.Clampf(window.x, 5.0f - window.width, Screen.width - 5.0f);
            window.y = Common.Clampf(window.y, 5.0f - window.height, Screen.height - 5.0f);
            window = GUILayout.Window(WindowId, window, DrawWindow, "Runways - " + body_name);
        }

        void DrawWindow(int id)
        {
            EnsureStyles();
            if (GUI.Button(new Rect(window.width - 16.0f, 1.0f, 15.0f, 16.0f), "x", GUIStyles.toggleButtonStyle))
                shown = false;
            Refresh();
            int selected = NavBridge.SelectedIndex;
            bool locked = NavBridge.SelectionLocked;

            GUILayout.BeginVertical();
            GUILayout.Label("Filter", GUIStyles.labelStyleLeft);
            DrawFilter(ref filter_dist, "dist", ref max_dist_km, 25, 25, 1000, "< ", " km");
            DrawFilter(ref filter_relbrg, "rel bearing", ref relbrg_deg, 15, 15, 180, "", " deg");
            DrawFilter(ref filter_intercept, "intercept", ref intercept_deg, 15, 15, 90, "", " deg");
            GUILayout.Space(6.0f);

            GUILayout.BeginHorizontal();
            DrawSortButton(SortKey.Name, "name", NameWidth);
            DrawSortButton(SortKey.Heading, "hdg", HdgWidth);
            DrawSortButton(SortKey.Distance, "dist", DistWidth);
            DrawSortButton(SortKey.Intercept, "intercept", InterceptWidth);
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Height(ListHeight));
            if (rows.Count == 0)
                GUILayout.Label("no runways on " + body_name, GUIStyles.labelStyleLeft);
            foreach (Row row in visible)
            {
                bool sel = row.rwy.index == selected;
                GUIStyle number = sel ? numberSelectedStyle : numberStyle;
                GUILayout.BeginHorizontal();
                string name = (sel && selected_filtered_out ? "* " : "") + row.rwy.shortID;
                if (GUILayout.Button(name, sel ? rowSelectedStyle : rowStyle, GUILayout.Width(NameWidth)) && !locked)
                    NavBridge.SelectRunway(row.rwy.index);
                GUILayout.Label(row.rwy.hdg.ToString("000"), number, GUILayout.Width(HdgWidth));
                GUILayout.Label((row.dist / 1000.0).ToString("0.0") + " km", number, GUILayout.Width(DistWidth));
                GUILayout.Label(row.intercept.ToString("+0;-0") + " deg", number, GUILayout.Width(InterceptWidth));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(visible.Count + " of " + rows.Count + " shown", GUIStyles.labelStyleLeft);
            if (locked)
                GUILayout.Label("INS target active: selection locked", GUIStyles.labelStyleLeft);
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        void DrawFilter(ref bool on, string label, ref int value, int step, int min, int max, string prefix, string suffix)
        {
            GUILayout.BeginHorizontal();
            bool now = GUILayout.Toggle(on, label, GUIStyles.toggleButtonStyle, GUILayout.Width(FilterWidth));
            if (now != on) { on = now; dirty = true; }
            if (GUILayout.Button("-", GUIStyles.toggleButtonStyle, GUILayout.Width(StepWidth)) && value > min) { value -= step; dirty = true; }
            if (GUILayout.Button("+", GUIStyles.toggleButtonStyle, GUILayout.Width(StepWidth)) && value < max) { value += step; dirty = true; }
            value = Mathf.Clamp(value, min, max);
            GUILayout.Label(prefix + value + suffix, GUIStyles.labelStyleRight, GUILayout.Width(ValueWidth));
            GUILayout.EndHorizontal();
        }

        // Sort buttons double as column headers; a second click on the active key reverses the order.
        void DrawSortButton(SortKey key, string label, float width)
        {
            bool on = sort == (int)key;
            bool now = GUILayout.Toggle(on, label, GUIStyles.toggleButtonStyle, GUILayout.Width(width));
            if (now == on)
                return;
            if (on)
                sort_desc = !sort_desc;
            else
            {
                sort = (int)key;
                sort_desc = false;
            }
            dirty = true;
        }

        static void EnsureStyles()
        {
            if (rowStyle != null)
                return;
            rowStyle = GUIStyles.labelStyleLeft;
            rowSelectedStyle = new GUIStyle(rowStyle);
            rowSelectedStyle.normal.textColor = Color.green;
            rowSelectedStyle.hover.textColor = Color.green;
            numberStyle = GUIStyles.labelStyleRight;
            numberSelectedStyle = new GUIStyle(numberStyle);
            numberSelectedStyle.normal.textColor = Color.green;
        }

        // Geometry is recomputed about once per second while the window is open, and at once after a
        // filter, sort or runway list change.
        void Refresh()
        {
            IList<RunwayInfo> list = NavBridge.Runways;
            if (list.Count != rows.Count || (rows.Count > 0 && !ReferenceEquals(rows[0].rwy, list[0])))
            {
                rows.Clear();
                foreach (RunwayInfo r in list)
                    rows.Add(new Row { rwy = r });
                dirty = true;
            }
            body_name = vessel.mainBody.name;
            if (!dirty && Time.realtimeSinceStartup < next_refresh)
                return;
            dirty = false;
            next_refresh = Time.realtimeSinceStartup + RefreshPeriod;

            double lat1 = vessel.latitude * Deg2Rad, lon1 = vessel.longitude * Deg2Rad;
            double radius = vessel.mainBody.Radius;
            double track = Track();
            foreach (Row row in rows)
            {
                double lat2 = row.rwy.locLatitude * Deg2Rad, lon2 = row.rwy.locLongitude * Deg2Rad;
                double dlon = lon2 - lon1;
                double sinHalfLat = Math.Sin((lat2 - lat1) / 2.0), sinHalfLon = Math.Sin(dlon / 2.0);
                double a = sinHalfLat * sinHalfLat + Math.Cos(lat1) * Math.Cos(lat2) * sinHalfLon * sinHalfLon;
                row.dist = 2.0 * radius * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
                double y = Math.Sin(dlon) * Math.Cos(lat2);
                double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dlon);
                row.rel_bearing = ApproachController.Wrap180(Math.Atan2(y, x) * Rad2Deg - track);
                row.intercept = ApproachController.Wrap180(track - row.rwy.hdg);
                row.visible = (!filter_dist || row.dist <= max_dist_km * 1000.0)
                    && (!filter_relbrg || Math.Abs(row.rel_bearing) <= relbrg_deg / 2.0)
                    && (!filter_intercept || Math.Abs(row.intercept) <= intercept_deg);
            }

            // the tuned runway stays listed (first, starred) even when the filters exclude it
            int selected = NavBridge.SelectedIndex;
            Row selectedRow = null;
            visible.Clear();
            foreach (Row row in rows)
            {
                if (row.rwy.index == selected)
                    selectedRow = row;
                if (row.visible)
                    visible.Add(row);
            }
            visible.Sort(Compare);
            selected_filtered_out = selectedRow != null && !selectedRow.visible;
            if (selected_filtered_out)
                visible.Insert(0, selectedRow);
        }

        /// <summary>Ground track, or the nose heading when too slow for the velocity to mean anything.</summary>
        double Track()
        {
            Vector3d up = (vessel.ReferenceTransform.position - vessel.mainBody.position).normalized;
            Vector3d dir = vessel.srfSpeed > 5.0 ? vessel.srf_velocity : (Vector3d)vessel.ReferenceTransform.up;
            Vector3d h = Vector3d.Exclude(up, dir);
            return ApproachController.Wrap360(Math.Atan2(Vector3d.Dot(h, vessel.east), Vector3d.Dot(h, vessel.north)) * Rad2Deg);
        }

        int Compare(Row a, Row b)
        {
            int c;
            switch ((SortKey)sort)
            {
                case SortKey.Name:
                    c = string.CompareOrdinal(a.rwy.shortID, b.rwy.shortID);
                    if (c == 0)
                        c = string.CompareOrdinal(a.rwy.ident, b.rwy.ident);
                    break;
                case SortKey.Heading:
                    c = a.rwy.hdg.CompareTo(b.rwy.hdg);
                    break;
                case SortKey.Intercept:
                    c = Math.Abs(a.intercept).CompareTo(Math.Abs(b.intercept));
                    break;
                default:
                    c = a.dist.CompareTo(b.dist);
                    break;
            }
            if (sort_desc)
                c = -c;
            return c != 0 ? c : a.rwy.index.CompareTo(b.rwy.index);
        }
    }
}
