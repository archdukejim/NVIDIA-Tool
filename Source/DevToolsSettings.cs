using Verse;

namespace NvidiaGpuMonitor
{
    /// <summary>
    /// Persistent settings for NVIDIA GPU Monitor.
    /// Saved to the RimWorld config folder automatically.
    /// </summary>
    public class DevToolsSettings : ModSettings
    {
        /// <summary>
        /// When true (default), show VRAM status dialog on every game load.
        /// When false, only show if VRAM is critically low.
        /// </summary>
        public bool alwaysNotifyVram = true;

        /// <summary>
        /// Opt-in: talk to a local/remote LM Studio (OpenAI-compatible) endpoint to show
        /// the loaded model and estimate its VRAM footprint. Off by default.
        /// </summary>
        public bool lmStudioEnabled = false;

        /// <summary>LM Studio endpoint used when <see cref="lmStudioEnabled"/> is set.</summary>
        public string lmStudioEndpoint = "http://127.0.0.1:1234";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref alwaysNotifyVram, "alwaysNotifyVram", true);
            Scribe_Values.Look(ref lmStudioEnabled, "lmStudioEnabled", false);
            Scribe_Values.Look(ref lmStudioEndpoint, "lmStudioEndpoint", "http://127.0.0.1:1234");
            base.ExposeData();
        }
    }
}
