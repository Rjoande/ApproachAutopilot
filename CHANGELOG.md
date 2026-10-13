# Changelog

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
