using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ApproachAutopilot")]
[assembly: AssemblyDescription("APR (ILS approach) autopilot mode for AtmosphereAutopilot")]
[assembly: AssemblyProduct("ApproachAutopilot")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Rjoande, GPL-3.0")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

[assembly: KSPAssembly("ApproachAutopilot", 1, 1)]
// Loads AtmosphereAutopilot first (0.0.0 = no KSPAssembly declared) and refuses to load without it.
[assembly: KSPAssemblyDependency("AtmosphereAutopilot", 0, 0)]
// Install gate only: NavInstruments is reached by reflection.
[assembly: KSPAssemblyDependency("NavInstrumentsContinued", 0, 8)]
