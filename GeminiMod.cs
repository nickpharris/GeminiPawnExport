using UnityEngine;
using Verse;

namespace MyRimWorldMod
{
    public class GeminiMod : Mod
    {
        public static GeminiSettings settings;

        public GeminiMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<GeminiSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.Label("Gemini API Key (Required):");
            settings.apiKey = listingStandard.TextEntry(settings.apiKey);

            listingStandard.Gap();

            listingStandard.Label("Default Analysis Prompt:");
            settings.defaultPrompt = listingStandard.TextEntry(settings.defaultPrompt, 4);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Gemini Export";
        }
    }
}