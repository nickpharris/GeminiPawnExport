using MyRimWorldMod;
using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace GeminiPawnExport
{
    public class MainTabWindow_GeminiExport : MainTabWindow
    {
        private string currentPrompt = "Waiting for input...";
        private CachedTableData currentTableData;
        private Vector2 scrollPosition = Vector2.zero; // Tracks scrollbar position

        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            // Title and Controls Area (Top 50 pixels)
            Rect controlsRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);

            if (Widgets.ButtonText(new Rect(controlsRect.x, controlsRect.y, 150f, 30f), "Send to Gemini"))
            {
                SendPawnDataToGemini();
            }

            // Main Content Area (Rest of the window)
            Rect outRect = new Rect(inRect.x, inRect.y + 50f, inRect.width, inRect.height - 50f);

            // Determine content height for the scroll view
            float viewHeight = 0f;
            if (currentTableData != null && currentTableData.IsValid)
            {
                // Calculate table height based on current width
                // We divide by column count, subtracting a bit for the scrollbar width (16f)
                float colWidth = (inRect.width - 20f) / currentTableData.Headers.Count;
                viewHeight = currentTableData.CalculateTotalHeight(colWidth);
            }
            else
            {
                // Fallback text height
                viewHeight = Text.CalcHeight(currentPrompt, inRect.width - 16f) + 20f;
            }

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, viewHeight);

            // Begin Scroll View
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, true);

            if (currentTableData != null && currentTableData.IsValid)
            {
                DrawTable(viewRect);
            }
            else
            {
                Widgets.Label(viewRect, currentPrompt);
            }

            Widgets.EndScrollView();
        }

        private void DrawTable(Rect outRect)
        {
            // 1. Switch to Tiny font for denser data
            GameFont originalFont = Text.Font;
            Text.Font = GameFont.Tiny;

            int colCount = currentTableData.Headers.Count;
            float colWidth = outRect.width / colCount;
            float currentY = outRect.y;

            // --- Draw Headers ---
            float headerHeight = 30f;
            for (int i = 0; i < colCount; i++)
            {
                Rect cellRect = new Rect(outRect.x + (i * colWidth), currentY, colWidth, headerHeight);

                // Highlight header background
                Widgets.DrawHighlight(cellRect);

                // Center text for headers
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(cellRect, currentTableData.Headers[i]);
                Text.Anchor = TextAnchor.UpperLeft;

                // Vertical divider
                Widgets.DrawLineVertical(cellRect.x + cellRect.width, cellRect.y, cellRect.height);
            }

            currentY += headerHeight;
            Widgets.DrawLineHorizontal(outRect.x, currentY, outRect.width);

            // --- Draw Rows ---
            foreach (var row in currentTableData.Rows)
            {
                // 1. Calculate max height required for this row (based on wrapping text)
                float maxRowHeight = 24f; // Minimum height
                for (int i = 0; i < row.Count && i < colCount; i++)
                {
                    float h = Text.CalcHeight(row[i], colWidth);
                    if (h > maxRowHeight) maxRowHeight = h;
                }

                // 2. Draw Cells
                for (int i = 0; i < row.Count && i < colCount; i++)
                {
                    Rect cellRect = new Rect(outRect.x + (i * colWidth), currentY, colWidth, maxRowHeight);

                    // ContractBy(4f) adds padding so text doesn't touch the lines
                    Widgets.Label(cellRect.ContractedBy(4f), row[i]);

                    Widgets.DrawLineVertical(cellRect.x + cellRect.width, cellRect.y, maxRowHeight);
                }

                currentY += maxRowHeight;
                Widgets.DrawLineHorizontal(outRect.x, currentY, outRect.width);
            }

            // Restore original font
            Text.Font = originalFont;
        }

        private void SendPawnDataToGemini()
        {
            this.currentPrompt = "Generating data... please wait.";
            this.currentTableData = null; // Clear old table

            // Collect Pawn Data
            StringBuilder sb = new StringBuilder();
            foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
            {
                sb.AppendLine($"Name: {pawn.Name.ToStringShort}, Skills: Shooting {pawn.skills.GetSkill(SkillDefOf.Shooting).Level}, Melee {pawn.skills.GetSkill(SkillDefOf.Melee).Level}, Traits: {GetTraits(pawn)}");
            }

            // Call API
            // Note: Ensure 'GeminiAPIManager' matches your actual class name
            GeminiAPIManager.SendRequest(sb.ToString(), OnGeminiResponse);
        }

        private string GetTraits(Pawn p)
        {
            if (p.story == null || p.story.traits == null) return "";
            List<string> traits = new List<string>();
            foreach (var t in p.story.traits.allTraits)
            {
                traits.Add(t.Label);
            }
            return string.Join(", ", traits);
        }

        // Callback function
        private void OnGeminiResponse(string response)
        {
            this.currentPrompt = response;
            // Parse the markdown immediately upon receiving it
            this.currentTableData = MarkdownTableParser.Parse(response);

            // Reset scroll to top when new data arrives
            this.scrollPosition = Vector2.zero;
        }
    }
}