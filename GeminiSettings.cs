using Verse;

namespace GeminiPawnExport
{
    public class GeminiSettings : ModSettings
    {
        public string apiKey = "";
        // Updated prompt to explicitly request a table format
        public string defaultPrompt = "Analyze the following RimWorld colonist data. Identify inefficiencies in weapon and armor allocation based on pawn skills (Shooting/Melee) and traits. Suggest specific reallocations to optimize defense. Please output the results as a Markdown table.";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref defaultPrompt, "defaultPrompt", "Analyze the following RimWorld colonist data. Identify inefficiencies in weapon and armor allocation based on pawn skills (Shooting/Melee) and traits. Suggest specific reallocations to optimize defense. Please output the results as a Markdown table.");
            base.ExposeData();
        }
    }
}