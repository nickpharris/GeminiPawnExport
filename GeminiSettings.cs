using Microsoft.SqlServer.Server;
using RimWorld;
using Verse;

namespace GeminiPawnExport
{
    public class GeminiSettings : ModSettings
    {
        // 1. Define the default value once as a constant.
        private const string DefaultPromptText =
            "In Rimworld, how should I optimally allocate all of my weapons to all of my pawns. " +
            "Please take into account which injuries (if any) are temporary vs permanent, " +
            "and allocate based on when they have recovered their fitness from any temporary injuries. " +
            "Make any recommendations about how I should reallocate clothing / armor (inc. recon helmets) " +
            "based on weapons strategy - considering their roles once they have recovered from any temporary battle injuries. " +
            "Format all output using Unity Rich Text formatting; if tables are included, format those using Markdown (not HTML-style).";

        private const string DefaultModel = "gemini-3-flash-preview";

        public string apiKey = "";

        // 2. Initialize the field using the constant.
        public string defaultPrompt = DefaultPromptText;
        public string model = DefaultModel;

        public override void ExposeData()
        {
            // The third argument is the default value used if the setting is missing in the XML file.
            Scribe_Values.Look(ref model, "model", DefaultModel);
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref defaultPrompt, "defaultPrompt", DefaultPromptText);

            base.ExposeData();
        }
    }
}