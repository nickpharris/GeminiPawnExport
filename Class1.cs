using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace MyRimWorldMod
{
    public static class DebugTools
    {
        // ------------------------------------------------------------------
        // 1. THE BUTTON (Entry Point)
        // ------------------------------------------------------------------
        [DebugAction("General", "Export Pawn Weapons Plan", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ExportPawnWeaponsPlan()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("### COLONIST ROSTER - OPTIMIZED HEALTH STATUS ###");

            // --- SELECTION LOGIC ---
            // 1. Get currently selected pawns that are colonists
            List<Pawn> pawnsToAnalyze = Find.Selector.SelectedPawns
                .Where(p => p.IsColonist && p.Map == Find.CurrentMap)
                .ToList();

            // 2. If NONE are selected, fallback to ALL free colonists on the map
            if (pawnsToAnalyze.Count == 0)
            {
                pawnsToAnalyze = Find.CurrentMap.mapPawns.FreeColonists.ToList();
            }

            // Loop through the determined list
            foreach (Pawn p in pawnsToAnalyze)
            {
                sb.AppendLine($"ID: {p.Name.ToStringShort}");

                // Role cleaning
                string roleRaw = p.MainDesc(true);
                string roleClean = Regex.Replace(roleRaw, "<[^>]+>", string.Empty);
                sb.AppendLine($" - Role: {roleClean}");

                // --- SKILLS ---
                int shoot = p.skills.GetSkill(SkillDefOf.Shooting).Level;
                int melee = p.skills.GetSkill(SkillDefOf.Melee).Level;
                int animals = p.skills.GetSkill(SkillDefOf.Animals).Level;

                sb.AppendLine($" - Shooting: {shoot}");
                sb.AppendLine($" - Melee: {melee}");
                sb.AppendLine($" - Animals: {animals}");

                // --- CRITICAL STATS ---
                float moveSpeed = p.GetStatValue(StatDefOf.MoveSpeed);
                sb.AppendLine($" - Move Speed: {moveSpeed:F2} c/s");

                // --- TRAITS ---
                var traits = p.story.traits.allTraits.Select(t => t.LabelCap);
                sb.AppendLine($" - Traits: {string.Join(", ", traits)}");

                // --- HEALTH REPORT ---
                sb.Append(PawnHealthPrinter.GetHealthReport(p));

                // --- EQUIPMENT (Weapons) ---
                if (p.equipment.Primary != null)
                {
                    sb.AppendLine($" - Currently Holding: {p.equipment.Primary.LabelCap}");
                }
                else
                {
                    sb.AppendLine($" - Currently Holding: None");
                }

                // --- APPAREL (Armor/Clothing/Belts) ---
                if (p.apparel != null && p.apparel.WornApparel.Count > 0)
                {
                    // WornApparel order is usually draw order; we list them all
                    var wornList = p.apparel.WornApparel.Select(a => a.LabelCap); // LabelCap includes Quality
                    sb.AppendLine($" - Apparel: {string.Join(", ", wornList)}");
                }
                else
                {
                    sb.AppendLine($" - Apparel: Nudist / None");
                }

                sb.AppendLine();
            }

            // ==========================================
            //           MAP INVENTORY SECTIONS
            // ==========================================

            // --- 1. WEAPONS ---
            sb.AppendLine("### TOTAL AVAILABLE ARMORY ###");
            List<Thing> weapons = Find.CurrentMap.listerThings.AllThings
                .Where(t => t.def.IsWeapon
                            && t.def.stuffProps == null // Excludes wood logs/steel bars
                            && !t.def.destroyOnDrop
                            && t.Spawned)
                .ToList();

            PrintGroupedThings(sb, weapons);

            // --- 2. APPAREL (includes Shield Belts) ---
            sb.AppendLine(); // Spacer
            sb.AppendLine("### TOTAL AVAILABLE APPAREL ###");
            List<Thing> apparel = Find.CurrentMap.listerThings.AllThings
                .Where(t => t.def.IsApparel
                            && !t.def.destroyOnDrop
                            && t.Spawned)
                .ToList();

            PrintGroupedThings(sb, apparel);

            Log.Message(sb.ToString());
        }

        // Helper to print lists cleanly
        private static void PrintGroupedThings(StringBuilder sb, List<Thing> items)
        {
            if (items.Count > 0)
            {
                var grouped = items
                    .GroupBy(t => t.LabelCap)
                    .OrderBy(g => g.Key);

                foreach (var group in grouped)
                {
                    int count = group.Count();
                    if (count > 1)
                        sb.AppendLine($"- {group.Key} (x{count} In Storage/Map)");
                    else
                        sb.AppendLine($"- {group.Key} (In Storage/Map)");
                }
            }
            else
            {
                sb.AppendLine("- None found on map.");
            }
        }
    }

    // ------------------------------------------------------------------
    // 2. THE LOGIC (Health Printer)
    // ------------------------------------------------------------------
    public static class PawnHealthPrinter
    {
        public static string GetHealthReport(Pawn p)
        {
            StringBuilder sb = new StringBuilder();

            // A. Native Stats
            sb.Append($" - Bio-Stats (Current): ");
            sb.Append($"Moving {p.health.capacities.GetLevel(PawnCapacityDefOf.Moving):P0}, ");
            sb.Append($"Sight {p.health.capacities.GetLevel(PawnCapacityDefOf.Sight):P0}, ");
            sb.Append($"Manipulation {p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation):P0}");
            sb.AppendLine();

            // B. Pain Analysis
            float totalPain = p.health.hediffSet.PainTotal;
            float tempPain = 0f;

            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (!IsPermanent(h)) tempPain += h.PainOffset;
            }

            if (totalPain > 0.01f)
            {
                sb.AppendLine($" - Pain Factor: {totalPain:P0} total pain (approx. {tempPain:P0} is temporary).");
            }

            // C. Health Factors (Filtered)
            List<string> permanentFactors = new List<string>();
            List<string> temporaryFactors = new List<string>();

            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (!h.Visible) continue;

                // Skip "Missing Part" if replaced by Bionic
                if (h is Hediff_MissingPart && IsPartReplacedByBionic(p, h.Part))
                {
                    continue;
                }

                string label = h.LabelCap;
                if (h.Part != null) label += $" ({h.Part.Label})";

                if (IsPermanent(h))
                {
                    permanentFactors.Add(label);
                }
                else
                {
                    if (h is Hediff_Injury injury)
                    {
                        label += $" [{Math.Round(injury.Severity, 1)} dmg]";
                    }
                    temporaryFactors.Add(label);
                }
            }

            if (permanentFactors.Count > 0)
                sb.AppendLine($" - Permanent/Implants: {string.Join(", ", permanentFactors)}");

            if (temporaryFactors.Count > 0)
                sb.AppendLine($" - Temporary Injuries: {string.Join(", ", temporaryFactors)}");
            else
                sb.AppendLine($" - Temporary Injuries: None");

            return sb.ToString();
        }

        private static bool IsPartReplacedByBionic(Pawn p, BodyPartRecord part)
        {
            if (part == null) return false;
            BodyPartRecord current = part;
            while (current != null)
            {
                bool hasAddedPart = p.health.hediffSet.hediffs.Any(x => x.Part == current && x is Hediff_AddedPart);
                if (hasAddedPart) return true;
                current = current.parent;
            }
            return false;
        }

        private static bool IsPermanent(Hediff h)
        {
            if (h is Hediff_AddedPart || h is Hediff_Implant) return true;
            if (h.def.chronic) return true;
            if (h is Hediff_Injury injury && injury.IsPermanent()) return true;
            if (h is Hediff_MissingPart) return true;
            return false;
        }
    }
}