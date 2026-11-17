# Changelog

## [Unreleased]

### Added
- **Autoland** (off by default, per craft): with *Autoland* on, APR no longer disconnects at the decision height (it only calls *MINIMUMS*) and flies on down to the runway, keeping the localizer with small bank angles. Two styles:
  - **Air Force style** (*Flare* on, default): an automatic flare that slows the descent down to about 0.6 m/s at touchdown.
  - **Navy style** (*Flare* off): no flare, the glideslope is flown all the way to the deck.
- With *APR speed control* on, **RETARD** brings the throttle to idle (5% by default, per craft) at the start of the flare, or at touchdown in Navy style.
- After one second on the ground APR says *TOUCHDOWN* and hands the aircraft back like at minimums; if the aircraft bounces before that, APR keeps flying. Derotation, steering on the runway and braking stay with you.
- **Autoland window** (*settings* next to the Autoland button): flare, touchdown sink rate, idle throttle and the **gear height**, measured by itself when the craft sits on the launch site, or with *Set gear height* whenever you are parked.
- **Aim point per runway**: some runway definitions put the glideslope antenna right on the threshold, so the aircraft touches down at the very start of the runway. In the Autoland window you can move the aim point down the runway for the tuned runway (e.g. 300 m); APR shifts its path gradually below the decision height. Saved per runway.
- Below the decision height APR trusts the ground below (radar) over the published runway elevation, and disconnects with *TERRAIN* if the ground is well above runway level.
- **Pitch authority warning**: if the elevator stays at its stop for a second while APR flies, APR says so (*raise speed at DH*): there is no margin left for the flare.

### Fixed
- An aircraft touching the ground before the decision height no longer leaves APR stuck in the manager: it hands over after one second on the ground.

### Changed
- **Default decision height is now 60 m** (about 200 ft, the usual ILS minimum) instead of 30 m: more room for a gentle flare without ballooning. Craft you already flew with APR keep their saved value; set *decision height* to 60 in the Approach window to switch them.

## v1.1.1

### Changed
- **Runways window**: runways are listed by their full name (e.g. *CMcAuliffe ILS 09L*) instead of the short code; the window is a bit wider, units moved into the column headers, and the distance filter reads *distance*.

## v1.1.0

### Added
- **APR speed control** (off by default): APR flies the throttle from localizer capture on, slowing down gradually so it reaches the desired speed at the decision height (default 80 m/s). *APR speed control* button replaces *Couple AA speed control*.

## v1.0.1

AtmosphereApproach is now **Approach Autopilot**: same mod, same features as v1.0.0, new name.

### Changed
- New folder, `GameData/ApproachAutopilot`, and new repository, [Rjoande/ApproachAutopilot](https://github.com/Rjoande/ApproachAutopilot). **Delete `GameData/AtmosphereApproach` when upgrading**: while it is installed, Approach Autopilot stays disabled and a message in the main menu says so. Settings and hotkey carry over.

## v1.0.0

First complete release: arm, capture, approach and hand-over work as designed, with the runway list and the background arming below.

### Added
- **Runway list window**: click the runway name in the Approach window to open a list of the runways on the current body, sortable by name, heading, distance or intercept angle and filterable by distance, relative bearing and intercept angle.

### Changed
- **Arming no longer selects the Approach controller.** Like a real APR mode, an armed APR waits in the background while whatever was flying (Cruise Flight, Fly-By-Wire or you) keeps flying; at localizer capture it selects itself in the manager and takes over, and it gives the seat back to that controller on localizer loss or disarm. The manager works as usual meanwhile: switch between Fly-By-Wire and Cruise Flight freely.
- Clicking **Approach controller** in the manager arms it (if a runway is tuned) and returns to the previous controller right away.
- The APR hotkey only arms and disarms; it no longer touches the manager or the master switch.
- All AtmosphereApproach messages now show top right, where AtmosphereAutopilot posts its own.

## v0.2.0

### Added
- **APR now flies the approach!** See README for how to use it.

## [alpha pre-release]

### Added
- **Approach controller** registered inside AtmosphereAutopilot: it shows up in the *Autopilot module manager* next to Fly-By-Wire, Cruise Flight and Mouse Director, with its own window and an *APR arm/disarm* entry in AtmosphereAutopilot's Hotkeys manager. AtmosphereAutopilot is untouched and required.
- The Approach window shows the runway and glideslope selected in NavInstruments (with `<` `>` buttons that keep the HSI in sync), the live DME, localizer and glideslope deviations, and the APR state: armed, localizer captured, glideslope captured, minimums. NavInstruments is required and read at run time only.
- Capture rules already in place: no localizer capture when too close to the runway, when flying away from it or when off the beam; glideslope captured only from within its band. The window says why APR is still waiting.
- While the controller is selected, Cruise Flight (default) or Fly-By-Wire keeps flying the aircraft, so it can be used today to fly vectors and watch the approach state.

### Not yet
- The controller does not steer on the localizer or the glideslope, and does not hand over at minimums: this is an integration preview.
