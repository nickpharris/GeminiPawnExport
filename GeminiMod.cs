using UnityEngine;
using Verse;

namespace GeminiPawnExport
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

            listingStandard.Label("Gemini API Model:");
            settings.model = listingStandard.TextEntry(settings.model);

            listingStandard.Gap();

            listingStandard.Label("Default Analysis Prompt:");
            // Increased height for prompt entry
            settings.defaultPrompt = listingStandard.TextEntry(settings.defaultPrompt, 10);

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Gemini Export";
        }
    }
}