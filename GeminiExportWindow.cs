using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace MyRimWorldMod
{
    public class MainTabWindow_GeminiExport : MainTabWindow
    {
        // 1. WINDOW SIZE: Increased width (720x600)
        public override Vector2 RequestedTabSize => new Vector2(720f, 600f);

        private string currentPrompt = "";
        private string geminiResponse = "";
        private bool isScanning = false;

        // Scroll positions
        private Vector2 scrollPosition = Vector2.zero;       // For the response area
        private Vector2 promptScrollPosition = Vector2.zero; // For the input box

        public override void PreOpen()
        {
            base.PreOpen();
            if (GeminiMod.settings != null && string.IsNullOrEmpty(currentPrompt))
            {
                currentPrompt = GeminiMod.settings.defaultPrompt;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // --- HEADER ---
            Text.Font = GameFont.Medium;
            listing.Label("Gemini Export & Analysis");
            Text.Font = GameFont.Small;
            listing.GapLine();

            // --- EXPORT BUTTONS ---
            Rect btnRect = listing.GetRect(30f);
            Rect btnLeft = btnRect.LeftHalf();
            Rect btnRight = btnRect.RightHalf();

            if (Widgets.ButtonText(btnLeft, "Copy Data to Clipboard"))
            {
                string report = GeminiExporter.GenerateReport();
                GUIUtility.systemCopyBuffer = report;
                Messages.Message("Colonist data copied to clipboard!", MessageTypeDefOf.PositiveEvent, false);
            }

            // 2. DEV MODE CHECK
            if (Prefs.DevMode)
            {
                if (Widgets.ButtonText(btnRight, "Print to Debug Log"))
                {
                    string report = GeminiExporter.GenerateReport();
                    Log.Message(report);
                    Messages.Message("Colonist data printed to log.", MessageTypeDefOf.NeutralEvent, false);
                }
            }

            listing.Gap();

            // --- GEMINI PROMPT SECTION ---
            listing.Label("Ask Gemini:");

            // 3. FIXED SCROLLABLE PROMPT BOX
            // We reserve a fixed height on the listing for the box
            Rect promptRect = listing.GetRect(90f);

            // Calculate the height the text *wants* to be
            // We subtract 16f from width to account for the scrollbar
            float promptContentHeight = Text.CalcHeight(currentPrompt, promptRect.width - 16f);

            // The view rect is either the calculated height OR the box height (whichever is bigger)
            Rect promptViewRect = new Rect(0, 0, promptRect.width - 16f, Mathf.Max(promptContentHeight, promptRect.height));

            // Create the scroll view
            Widgets.BeginScrollView(promptRect, ref promptScrollPosition, promptViewRect);
            currentPrompt = Widgets.TextArea(promptViewRect, currentPrompt);
            Widgets.EndScrollView();

            listing.Gap(5f);

            // --- ANALYZE BUTTON ---
            if (isScanning)
            {
                listing.Label("Analyzing... (Please wait)");
            }
            else
            {
                if (listing.ButtonText("Analyze Colony via Gemini API"))
                {
                    if (GeminiMod.settings == null || string.IsNullOrEmpty(GeminiMod.settings.apiKey))
                    {
                        geminiResponse = "Error: Please set your API Key in Mod Settings first.";
                    }
                    else
                    {
                        StartGeminiRequest();
                    }
                }
            }

            listing.GapLine();

            // --- RESPONSE HEADER & COPY BUTTON ---
            Rect responseHeaderRect = listing.GetRect(24f);
            Widgets.Label(responseHeaderRect.LeftHalf(), "Response:");

            if (Widgets.ButtonText(responseHeaderRect.RightHalf(), "Copy Response Text"))
            {
                GUIUtility.systemCopyBuffer = geminiResponse;
                Messages.Message("Response copied to clipboard!", MessageTypeDefOf.PositiveEvent, false);
            }

            listing.End(); // End listing to handle the main scroll view manually

            // --- RESPONSE AREA (SCROLLABLE) ---
            float usedHeight = listing.CurHeight;
            Rect outRect = new Rect(0, usedHeight, inRect.width, inRect.height - usedHeight);

            float contentHeight = Text.CalcHeight(geminiResponse, outRect.width - 20f);
            Rect viewRect = new Rect(0, 0, outRect.width - 16f, contentHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Widgets.Label(viewRect, geminiResponse);
            Widgets.EndScrollView();
        }

        private void StartGeminiRequest()
        {
            isScanning = true;
            geminiResponse = "Sending data to Gemini...";
            string data = GeminiExporter.GenerateReport();
            string apiKey = GeminiMod.settings.apiKey;

            Task.Run(async () =>
            {
                string result = await GeminiAPIManager.AskGemini(currentPrompt, data, apiKey);
                string formatted = FormatMarkdownToRichText(result);

                geminiResponse = formatted;
                isScanning = false;
            });
        }

        private string FormatMarkdownToRichText(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            string text = raw;

            // 1. Headers (### Header) -> Bold + Larger
            text = Regex.Replace(text, @"^#{1,6}\s?(.*)$", "\n<b><size=16>$1</size></b>", RegexOptions.Multiline);

            // 2. Bold (**text**) -> <b>text</b>
            text = Regex.Replace(text, @"\*\*(.*?)\*\*", "<b>$1</b>");

            // 3. Italics (*text*) -> <i>text</i>
            text = Regex.Replace(text, @"(?<!\*)\*(?!\*)(.*?)(?<!\*)\*(?!\*)", "<i>$1</i>");

            // 4. Bullet Points (* Item or - Item) -> Clean Indented Bullets
            text = Regex.Replace(text, @"^[\*\-]\s+(.*)$", "  • $1", RegexOptions.Multiline);

            // 5. Horizontal Rule (---) -> Visual Separator Line
            text = Regex.Replace(text, @"^([-*_]){3,}\s*$", "<color=#666666>────────────────────────────────────────</color>", RegexOptions.Multiline);

            return text;
        }
    }

    // ------------------------------------------------------------------
    // EXISTING EXPORT LOGIC
    // ------------------------------------------------------------------
    public static class GeminiExporter
    {
        public static string GenerateReport()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("### COLONIST ROSTER - OPTIMIZED HEALTH STATUS ###");

            List<Pawn> pawnsToAnalyze = Find.Selector.SelectedPawns
                .Where(p => p.IsColonist && p.Map == Find.CurrentMap)
                .ToList();

            if (pawnsToAnalyze.Count == 0)
            {
                if (Find.CurrentMap != null)
                {
                    pawnsToAnalyze = Find.CurrentMap.mapPawns.FreeColonists.ToList();
                    sb.AppendLine("(No specific colonists selected. Exporting ALL free colonists.)");
                }
                else
                {
                    sb.AppendLine("(No map loaded.)");
                }
            }

            foreach (Pawn p in pawnsToAnalyze)
            {
                sb.AppendLine($"ID: {p.Name.ToStringShort}");
                string roleRaw = p.MainDesc(true);
                string roleClean = Regex.Replace(roleRaw, "<[^>]+>", string.Empty);
                sb.AppendLine($" - Role: {roleClean}");

                int shoot = p.skills.GetSkill(SkillDefOf.Shooting).Level;
                int melee = p.skills.GetSkill(SkillDefOf.Melee).Level;
                int animals = p.skills.GetSkill(SkillDefOf.Animals).Level;

                sb.AppendLine($" - Shooting: {shoot}");
                sb.AppendLine($" - Melee: {melee}");
                sb.AppendLine($" - Animals: {animals}");

                float moveSpeed = p.GetStatValue(StatDefOf.MoveSpeed);
                sb.AppendLine($" - Move Speed: {moveSpeed:F2} c/s");

                if (p.story != null && p.story.traits != null)
                {
                    var traits = p.story.traits.allTraits.Select(t => t.LabelCap);
                    sb.AppendLine($" - Traits: {string.Join(", ", traits)}");
                }

                sb.Append(PawnHealthPrinter.GetHealthReport(p));

                if (p.equipment.Primary != null)
                    sb.AppendLine($" - Currently Holding: {p.equipment.Primary.LabelCap}");
                else
                    sb.AppendLine($" - Currently Holding: None");

                if (p.apparel != null && p.apparel.WornApparel.Count > 0)
                {
                    var wornList = p.apparel.WornApparel.Select(a => a.LabelCap);
                    sb.AppendLine($" - Apparel: {string.Join(", ", wornList)}");
                }
                else
                    sb.AppendLine($" - Apparel: Nudist / None");

                sb.AppendLine();
            }

            if (Find.CurrentMap != null)
            {
                sb.AppendLine("### TOTAL AVAILABLE ARMORY ###");
                List<Thing> weapons = Find.CurrentMap.listerThings.AllThings
                    .Where(t => t.def.IsWeapon && t.def.stuffProps == null && !t.def.destroyOnDrop && t.Spawned)
                    .ToList();
                PrintGroupedThings(sb, weapons);

                sb.AppendLine();
                sb.AppendLine("### TOTAL AVAILABLE APPAREL ###");
                List<Thing> apparel = Find.CurrentMap.listerThings.AllThings
                    .Where(t => t.def.IsApparel && !t.def.destroyOnDrop && t.Spawned)
                    .ToList();
                PrintGroupedThings(sb, apparel);
            }

            return sb.ToString();
        }

        private static void PrintGroupedThings(StringBuilder sb, List<Thing> items)
        {
            if (items.Count > 0)
            {
                var grouped = items.GroupBy(t => t.LabelCap).OrderBy(g => g.Key);
                foreach (var group in grouped)
                {
                    int count = group.Count();
                    sb.AppendLine($"- {group.Key} {(count > 1 ? $"(x{count} In Storage/Map)" : "(In Storage/Map)")}");
                }
            }
            else
            {
                sb.AppendLine("- None found on map.");
            }
        }
    }

    public static class PawnHealthPrinter
    {
        public static string GetHealthReport(Pawn p)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($" - Bio-Stats (Current): ");
            sb.Append($"Moving {p.health.capacities.GetLevel(PawnCapacityDefOf.Moving):P0}, ");
            sb.Append($"Sight {p.health.capacities.GetLevel(PawnCapacityDefOf.Sight):P0}, ");
            sb.Append($"Manipulation {p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation):P0}");
            sb.AppendLine();

            float totalPain = p.health.hediffSet.PainTotal;
            if (totalPain > 0.01f)
                sb.AppendLine($" - Pain Factor: {totalPain:P0} total pain.");

            List<string> permanentFactors = new List<string>();
            List<string> temporaryFactors = new List<string>();

            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (!h.Visible) continue;
                if (h is Hediff_MissingPart && IsPartReplacedByBionic(p, h.Part)) continue;

                string label = h.LabelCap;
                if (h.Part != null) label += $" ({h.Part.Label})";

                if (IsPermanent(h)) permanentFactors.Add(label);
                else
                {
                    if (h is Hediff_Injury injury) label += $" [{Math.Round(injury.Severity, 1)} dmg]";
                    temporaryFactors.Add(label);
                }
            }

            if (permanentFactors.Count > 0) sb.AppendLine($" - Permanent/Implants: {string.Join(", ", permanentFactors)}");
            if (temporaryFactors.Count > 0) sb.AppendLine($" - Temporary Injuries: {string.Join(", ", temporaryFactors)}");
            else sb.AppendLine($" - Temporary Injuries: None");

            return sb.ToString();
        }

        private static bool IsPartReplacedByBionic(Pawn p, BodyPartRecord part)
        {
            if (part == null) return false;
            BodyPartRecord current = part;
            while (current != null)
            {
                if (p.health.hediffSet.hediffs.Any(x => x.Part == current && x is Hediff_AddedPart)) return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsPermanent(Hediff h)
        {
            return (h is Hediff_AddedPart || h is Hediff_Implant || h.def.chronic || (h is Hediff_Injury i && i.IsPermanent()) || h is Hediff_MissingPart);
        }
    }
}