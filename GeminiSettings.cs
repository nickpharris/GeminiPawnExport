using Verse;

namespace MyRimWorldMod
{
    public class GeminiSettings : ModSettings
    {
        public string apiKey = "";
        public string defaultPrompt = "Analyze the following RimWorld colonist data. Identify inefficiencies in weapon and armor allocation based on pawn skills (Shooting/Melee) and traits. Suggest specific reallocations to optimize defense.";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref defaultPrompt, "defaultPrompt", "Analyze the following RimWorld colonist data. Identify inefficiencies in weapon and armor allocation based on pawn skills (Shooting/Melee) and traits. Suggest specific reallocations to optimize defense.");
            base.ExposeData();
        }
    }
}