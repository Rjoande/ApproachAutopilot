Approach Autopilot (APR) - ILS approach mode for AtmosphereAutopilot
=====================================================================

Adds an "Approach controller" to AtmosphereAutopilot's Autopilot module
manager: arm it on the runway tuned in NavInstruments while Fly-By-Wire
or Cruise Flight keeps flying; at localizer capture it takes over, tracks
localizer and glideslope and flies the approach down to the decision
height. Optional APR speed control flies the throttle down to the speed
you set for the decision height. Flare, touchdown and braking stay with
you.

Formerly AtmosphereApproach: if GameData/AtmosphereApproach is still
installed, delete it (Approach Autopilot stays disabled until you do).

Requires (not bundled):
  - AtmosphereAutopilot 1.6.1   https://github.com/Boris-Barboris/AtmosphereAutopilot
  - NavInstruments Continued 0.8.1+   https://github.com/linuxgurugamer/NavInstruments

Install: copy this folder into GameData, next to AtmosphereAutopilot and
NavInstruments.

License: GPL-3.0 (see LICENSE in the repository).
Source and issues: https://github.com/Rjoande/ApproachAutopilot
