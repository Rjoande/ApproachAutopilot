*Approach Autopilot: your first automatic ILS approach, step by step.*

This manual walks you through one complete approach to **KSC runway 27**, from take-off to the moment the autopilot hands the aircraft back to you, a few seconds before touchdown. It takes about ten minutes of game time. Once you have flown it, the [Quick reference](#quick-reference) at the bottom is all you need.

> **What APR does, in one sentence:** you fly towards the runway and arm APR; when you meet the runway centerline (the *localizer*) APR takes over, lines up, descends on a fixed slope (the *glideslope*) and gives the controls back to you at the *decision height*. The flare, the touchdown and the brakes are yours.

---

## Contents

1. [Before you fly](#1-before-you-fly)
2. [APR is a mode, not an autopilot](#2-apr-is-a-mode-not-an-autopilot)
3. [Open the Approach window](#3-open-the-approach-window)
4. [Choose the runway](#4-choose-the-runway)
5. [Fly to the intercept](#5-fly-to-the-intercept)
6. [Arm APR](#6-arm-apr)
7. [Localizer capture: APR takes over](#7-localizer-capture-apr-takes-over)
8. [Glideslope capture and descent](#8-glideslope-capture-and-descent)
9. [Minimums: hand-over to the pilot](#9-minimums-hand-over-to-the-pilot)
10. [Speed](#10-speed)
11. [Disarming and taking over](#11-disarming-and-taking-over)
12. [When APR says no](#12-when-apr-says-no)
13. [Settings](#13-settings)
14. [Quick reference](#quick-reference)

---

## 1. Before you fly

You need three mods in `GameData`:

- **AtmosphereAutopilot** 1.6.1
- **NavInstruments Continued** 0.8.1 or later
- **Approach Autopilot**

For your first approach pick an aircraft that AtmosphereAutopilot's **Cruise Flight** already flies well: a small jet, nothing exotic. With a jet APR can also fly the throttle for you (see [Speed](#10-speed)).

Take off from KSC and climb to about **1500 m**. Fly **east, out over the sea**: runway 27 is approached from the east, and flat water makes the first try easier.

![Departing KSC eastbound over the sea](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-01-departure.jpg)

---

## 2. APR is a mode, not an autopilot

On a real airliner you don't "switch to" the approach autopilot: you keep flying with heading and altitude modes, and you **arm** APR. The aircraft carries on as before until it meets the localizer; only then does APR take over.

Approach Autopilot works the same way inside AtmosphereAutopilot:

- **Before capture** your usual controller flies: **Cruise Flight**, **Fly-By-Wire**, or you by hand. Switch between them in the manager as often as you like; an armed APR keeps watching in the background.
- **At localizer capture** APR selects itself in the manager and flies the approach.
- **If the localizer is lost**, APR gives the aircraft back to the controller that was flying before, and stays armed.
- **At minimums**, Fly-By-Wire gets the aircraft.

So you will mostly *look at* the Approach window, not select the Approach controller.

---

## 3. Open the Approach window

1. Open AtmosphereAutopilot's **Autopilot module manager**: **Shift + P** (Shift + your AA master key).
2. Switch the **master** on and select **Cruise Flight**. Set its heading and altitude as usual.
3. Click the **GUI** button next to **Approach controller**. Don't click the name: the window is all you need.

![Autopilot module manager: Cruise Flight selected, GUI button of Approach controller](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-02-manager.jpg)

> Clicked the name anyway? No harm done: APR arms (if a runway is tuned) and gives the seat straight back to the controller that was flying, with a message saying so.

Here is the Approach window:

![The Approach window, annotated](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-03-window-tour.png)

| # | Line | What it tells you |
|---|---|---|
| 1 | **Runway** `<` `[SC27]` `>` | The runway APR will fly to. Click the name to open the **Runways** list; `<` `>` step through the runways. Below: heading, elevation and full name. |
| 2 | **GS** `<` `>` | The glideslope angle (3.0 deg is standard). |
| 3 | **APR** button | Arms and disarms APR. Its label shows the state: *off*, *ARMED*, *LOC captured - GS armed*, *LOC + GS captured*, *MINIMUMS*. |
| 4 | Gate line | What APR is waiting for, or why it won't capture. **Read this line first.** |
| 5 | **DME / LOC / GS** | Distance to the runway, and how far you are off the localizer and the glideslope (degrees). |
| 6 | **spd / target** | Your speed, the speed to fly now and how far off you are (*on speed*, *+12 fast*, *-5 slow*). See [Speed](#10-speed). |
| 7 | **above rwy / throttle** | Height above the runway, and who works the throttle: *APR*, *AA* (AtmosphereAutopilot's speed control) or *pilot*. |
| 8 | **track / rwy / intercept / xtk** | Your ground track, the runway heading, your intercept angle and your distance from the centerline (*R* / *L* = right / left of it). |
| 9 | **GS at ... / intercept hdg** | Intercept helpers, shown while armed: the glideslope altitude at the outer marker and a heading that joins the centerline. |
| 10 | Bottom line | Who is flying: *Cruise Flight flies*, *Standard Fly-By-Wire flies*, *pilot flies*, or *APR flies* with the commanded heading and vertical speed. |

The numbers are live even when APR is off: the window doubles as a simple ILS display.

---

## 4. Choose the runway

### The quick way: `<` `>`

Step with the **Runway** `<` `>` buttons until you see **SC27** (heading 270, elevation 70 m). Leave **GS** at **3.0 deg**. That's it.

### The Runways window

With a runway pack (or just to see what's around) click the runway name: the **Runways** window opens next to the Approach window.

![The Runways window, annotated](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-04-runways.jpg)

- **Columns**: name, runway heading, distance, intercept angle (the angle between your track and the runway). Click a column header to sort by it; click it again to reverse the order.
- **Click a runway name** to tune it. The window stays open, and the tuned runway is shown in green.
- **Filters** (each one on/off, adjusted with `-` `+`, all combined):

| Filter | Keeps runways... | Typical use |
|---|---|---|
| **dist** | closer than the distance shown | "what can I reach from here?" |
| **rel bearing** | inside a cone ahead of you, with the total width shown | "runways in front of me", not behind |
| **intercept** | whose heading is within the angle shown of your track | "runways I am already roughly lined up with" |

**rel bearing** looks at *where* the runway is, **intercept** at *how it is oriented*. Together they answer the classic question: *which runway ahead of me can I land on without turning much?*

> Example: flying back west towards KSC, switch on **rel bearing 120** and **intercept 30**, sort by **dist**: KSC 27 is at the top, with the Island runway 27 further down.

- A runway marked **`* `** is the tuned one, kept at the top even when the filters would hide it.
- The footer shows how many runways pass the filters.
- The selection is the same as the NavInstruments HSI: change it in either place and the other follows.

> **INS target active: selection locked** — a stock waypoint is active, and NavInstruments navigates to it instead of a runway (shown as **INS** in the Approach window). Deactivate the waypoint to choose a runway again.

The Runways window closes with its `x`, by clicking the runway name again, or together with the Approach window. It remembers its position, filters and sorting.

---

## 5. Fly to the intercept

This part is flown by Cruise Flight (or by you). The goal is to reach the runway's **extended centerline** in a position where APR can take over. Three rules:

1. **Far enough:** at least **5 km** from the runway (the *min final DME* setting). 10 to 15 km is comfortable.
2. **Below the glideslope:** APR joins the glideslope **from below**, like a real ILS. Look at the *GS at 10.0 km: ... m MSL* line and stay **100-200 m lower** than that. For SC27 that's around **400-500 m**.
3. **At a gentle angle:** cross the centerline at **30 degrees or less**, heading towards the runway. The *intercept hdg* in the window is a good heading to fly.

![Intercept geometry: below the glideslope, 30 degree intercept, more than 5 km out](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-05-geometry.png)

In practice, from the sea east of KSC:

- Set Cruise Flight to **altitude 450 m**.
- Fly away from the runway until DME reads **15 km** or more, a few km to one side of the centerline.
- Turn towards the centerline, using the **intercept hdg** shown in the window as Cruise Flight's heading.

![Flying the intercept heading: Approach window and Cruise Flight window](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-06-vectors.jpg)

---

## 6. Arm APR

You can arm at any time, long before the centerline. Three ways, all equivalent:

- the **APR** button in the Approach window;
- the **APR arm/disarm** hotkey, if you bound one in AtmosphereAutopilot's *Hotkeys manager* (recommended);
- clicking **Approach controller** in the manager (it arms and gives the seat straight back).

Top right of the screen: **APR armed on SC27 - waiting for LOC**. The button reads **APR ARMED**, and **Cruise Flight keeps flying**: nothing changes in the cockpit.

![APR ARMED: message top right, gate line "waiting LOC", Cruise Flight still flying](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-07-armed.jpg)

The gate line tells you what APR is waiting for:

| Gate line | Meaning | What to do |
|---|---|---|
| *waiting LOC (dev +1.8 deg)* | All good: APR will capture when you reach the centerline. | Keep flying. |
| *too close: DME 3.2 km < 5.0 km, fly out and re-intercept* | Too close to set up a stable approach. | Fly out and come back. |
| *intercept angle 120 deg, flying away from the runway* | You are not heading towards the runway. | Turn towards it. |

While armed you can still change Cruise Flight's heading and altitude, switch to Fly-By-Wire and back, or even switch the master off and fly by hand: APR keeps watching, and switches the master back on at capture.

---

## 7. Localizer capture: APR takes over

When the localizer deviation enters the capture band (2 degrees by default):

- Top right: **APR: localizer captured, holding 450 m**.
- In the manager, **Approach controller** becomes the active controller.
- The button reads **LOC captured - GS armed**; the bottom line switches to **APR flies: hdg ... hold ... m V/S ...**.
- The aircraft turns smoothly onto the centerline and **holds the altitude it had at capture**.

The gate line now reads **below GS -0.40 deg, holding 450 m**: exactly what you want. You fly level, under the glideslope, waiting for the beam to come down to you.

![LOC captured: Approach controller active in the manager, "below GS" in the gate line](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-08-loc.jpg)

---

## 8. Glideslope capture and descent

When the glideslope deviation reaches the capture band (0.35 degrees by default):

- Top right: **APR: glideslope captured**. The button reads **LOC + GS captured**.
- The aircraft pitches down and descends on the glideslope, tracking the centerline at the same time. The bottom line shows the commanded vertical speed.

![LOC + GS captured: both deviations near zero](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-09-gs.jpg)

Now **watch your speed**: on a 3 degree slope a jet tends to accelerate. Pull the throttle back (see [Speed](#10-speed)).

![Final approach, runway ahead](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-10-final_2.jpg)

---

## 9. Minimums: hand-over to the pilot

At the **decision height** (30 m above the runway by default) APR disconnects:

- Top right: **MINIMUMS - autopilot disconnected**.
- Fly-By-Wire takes the aircraft, with its AoA and G limits to help you; APR is off.

Now it's yours: **throttle to idle, flare, touch down, brakes**.

![MINIMUMS: message top right, threshold just ahead](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-11-minimums.jpg)

![Touchdown](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-12-touchdown_1.jpg)

Congratulations, that was your first automatic approach!

> Prefer a clean disconnect? Tick **Minimums: master off (else FBW)**: at minimums AtmosphereAutopilot switches off completely and you fly with stock SAS or by hand.

---

## 10. Speed

An approach is flown **slowing down**: you want to arrive at the decision height at your landing speed, not at cruise speed. APR computes the right speed for every point of the approach and shows it on the **spd** line, whether or not it flies the throttle.

### The speed to fly

Set **speed at DH (m/s)** to the speed you want at minimums (default 80 m/s; saved per craft). From there APR works backwards: the further you are from the decision height, the faster you may still be, with a gentle deceleration of 1 m/s per second.

| Distance to the DH point | Highest speed (with 80 m/s at DH) |
|---|---|
| 10 km | 162 m/s |
| 5 km | 128 m/s |
| 2 km | 102 m/s |
| at DH | 80 m/s |

The **target** on the spd line is your current speed (or the speed you had at localizer capture, or AtmosphereAutopilot's speed setpoint if its speed control is on), capped by that limit and never below the speed at DH. So if you arrive slow, APR doesn't ask you to speed up; if you arrive fast, the target comes down smoothly as you get closer.

### Who works the throttle

The **throttle:** line tells you:

- **APR**: tick **APR speed control** and, from localizer capture on, APR flies the throttle to the target. Throttle only: **airbrakes stay yours**, and you may need them on a slippery aircraft.
- **AA**: before capture, AtmosphereAutopilot's speed control (from Cruise Flight or Fly-By-Wire) is holding its setpoint.
- **pilot**: you. Follow the spd line: *on speed* is within 3 m/s, otherwise it says how many m/s *fast* or *slow* you are.

![spd and throttle lines with APR speed control on](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-14-speed.png)

APR speed control works with **jets and rockets** only: AtmosphereAutopilot's thrust controller has no model for propellers or rotors. Propeller and rotor aircraft fly the approach exactly like jets; just keep the speed by hand, following the spd line.

### Too fast for the glideslope

A fast aircraft can't lose its energy on a 3 degree slope. With APR speed control on, if you meet the glideslope more than 15% above the speed limit for that distance, APR **doesn't start the descent**: the gate line reads *too fast for GS: 180 m/s, max 164, slowing down*, and APR keeps holding altitude on the localizer while it slows down.

The warning already shows while you are still below the beam, so there is time to fix it: deploy the airbrakes. If you are slow enough when the beam reaches you, APR captures it as usual. If you fly through the beam still too fast, you end up above it: fly out and re-intercept, or enable *Allow GS capture from above*.

> Above Mach 1 the spd line turns **red** and reads *slow down*: supersonic approaches are not supported yet.

---

## 11. Disarming and taking over

| You do | Before capture (ARMED) | After capture (APR flying) |
|---|---|---|
| Press **APR** / the hotkey | APR off; your controller keeps flying. | APR off; the controller that flew before capture takes over. |
| Select another controller in the manager | Nothing changes: APR stays armed. | APR off; the selected controller flies. |
| Switch the master off | APR stays armed and switches the master on at capture. | APR off; you fly by hand. |

Every disarm shows **APR off** top right.

---

## 12. When APR says no

APR behaves like a real approach mode: if it can't fly a stable approach, it won't capture, and it tells you why.

| What you see | Why | What to do |
|---|---|---|
| *APR not armed: no runway tuned on Kerbin* | No runway for the current planet. | Choose a runway (`<` `>` or the Runways window). |
| *too close: DME ... km, fly out and re-intercept* | Closer than *min final DME*. | Fly out and come back. |
| *intercept angle ... deg, flying away from the runway* | Heading away from the runway. | Turn towards it, 30 degrees or less from the runway heading. |
| *above GS ...: no capture from above, get below the beam* | Glideslope capture from above is refused by default. | Descend below the beam, or tick *Allow GS capture from above* under *Advanced*. |
| *APR: localizer lost*, button back to **ARMED** | Drifted too far off the centerline (3x the capture band). | Your previous controller flies again and APR is still armed: fly back to the centerline. |
| *too fast for GS: ... m/s, max ..., slowing down* | Too fast to lose the energy on the glideslope (APR speed control on). | Airbrakes. See [Too fast for the glideslope](#too-fast-for-the-glideslope). |
| *APR: glideslope lost, holding ... m*, button back to **LOC captured** | Too far off the glideslope. | APR holds the current altitude on the localizer and recaptures from below. |
| *APR off: runway lost* | The runway disappeared (waypoint activated, body changed). | Choose the runway again and re-arm. |
| *INS target active: selection locked* in the Runways window | A stock waypoint is active. | Deactivate it. |
| *NavInstruments not found* in the Approach window | NavInstruments is missing or didn't load. | Install NavInstruments Continued 0.8.1+. |
| No *Approach controller* in the manager | Approach Autopilot didn't load, or is disabled because the old GameData/AtmosphereApproach folder is still installed (the main menu says so). | Check that AtmosphereAutopilot and NavInstruments are installed, then look for `[ApproachAutopilot]` in `KSP.log`. |

![Gate line examples: too close, above GS](https://github.com/Rjoande/AtmosphereApproach/blob/main/images/fm-13-refusals.png)

---

## 13. Settings

The defaults are tuned for a standard 3 degree ILS approach: you shouldn't need to change them for your first flights.

**Main window**

| Setting | Default | What it does |
|---|---|---|
| decision height (m) | 30 | Height above the runway where APR disconnects. Saved per craft. |
| LOC capture (deg) | 2 | Localizer capture band. |
| GS capture (deg) | 0.35 | Glideslope capture band. |
| min final DME (m) | 5000 | No localizer capture closer than this. |
| APR speed control | off | APR flies the throttle from localizer capture on (jets and rockets). |
| speed at DH (m/s) | 80 | Speed to reach at the decision height. Saved per craft. |
| Minimums: master off (else FBW) | off | What happens at minimums. |
| Director / Thrust controller GUI | off | Open the underlying AtmosphereAutopilot windows. |

**Advanced** (for tuning; leave them alone unless an aircraft misbehaves)

| Setting | Default | What it does |
|---|---|---|
| max intercept (deg) | 30 | Steepest intercept APR flies onto the centerline. |
| lateral time const (s) | 10 | How quickly APR corrects centerline errors (lower = sharper). |
| lookahead min / max (m) | 300 / 3000 | How far ahead APR aims along the centerline. |
| height gain / V/S gain (1/s) | 0.15 / 0.3 | How firmly APR corrects altitude and vertical-speed errors. |
| FPA margin (deg) | 3 | Extra flight-path angle allowed when correcting onto the glideslope. |
| director strength mult | 0.6 | Softens AA's director during the approach. Saved per craft. |
| Allow GS capture from above | off | Capture the glideslope when coming from above. |
| Decision height on radar altitude | on | Disconnect at the decision height above the ground below, if that comes first (hilly approaches). |
| GS capture speed margin (%) | 15 | How much faster than the speed limit you may be and still capture the glideslope (APR speed control only). |
| speed envelope decel (m/s2) | 1 | Deceleration behind the speed limit: higher = you may stay fast for longer. |

**Runways window**: filters, thresholds, sorting and window position are remembered.

Everything is saved in AtmosphereAutopilot's own settings files, together with your other AA settings.

---

## Quick reference

1. **Shift + P**, master on, **Cruise Flight** selected. Open the **Approach controller** GUI.
2. Choose the **runway** (`<` `>`, or click its name for the Runways window) and the **GS**.
3. Fly **more than 5 km** out, **below the glideslope**, towards the centerline at **30 deg or less** (use *intercept hdg*).
4. **Arm** APR (button or hotkey). Read the gate line. Cruise Flight keeps flying.
5. *LOC captured*: APR takes over, turns onto the centerline and holds altitude.
6. *LOC + GS captured*: APR descends. **Speed**: APR speed control, or follow the spd line with the throttle.
7. *MINIMUMS*: APR hands over to you. Flare, touch down, brake.

---

*Coming in later versions: approach speed management (Vref at minimums), go-around, automatic vectors to final, unpowered and steep glide approaches for shuttle-style landings. This manual will grow with them.*
