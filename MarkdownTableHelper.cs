using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace GeminiPawnExport
{
    // Holds the pre-digested table data
    public class CachedTableData
    {
        public List<string> Headers = new List<string>();
        public List<List<string>> Rows = new List<List<string>>();
        public bool IsValid => Headers.Count > 0 || Rows.Count > 0;

        // Helper to calculate total height for scrolling
        public float CalculateTotalHeight(float columnWidth)
        {
            float total = 30f; // Header height
            foreach (var row in Rows)
            {
                float maxRowH = 0f;
                // Check every cell in the row to find the tallest text
                foreach (var cell in row)
                {
                    float h = Text.CalcHeight(cell, columnWidth);
                    if (h > maxRowH) maxRowH = h;
                }
                // Ensure minimum row height
                total += Math.Max(maxRowH, 24f);
            }
            return total;
        }
    }

    public static class MarkdownTableParser
    {
        public static CachedTableData Parse(string markdown)
        {
            var data = new CachedTableData();
            if (string.IsNullOrEmpty(markdown)) return data;

            var lines = markdown.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string trimmed = line.Trim();

                // Identify lines that look like table rows: "| Cell | Cell |"
                if (trimmed.StartsWith("|") && trimmed.EndsWith("|"))
                {
                    // Remove leading/trailing pipes and split
                    var cells = trimmed.Split('|')
                        .Where((val, index) => index != 0 && index != trimmed.Length - 1)
                        .Select(c => CleanCell(c))
                        .ToList();

                    // Detect separator lines (e.g., "|---|---|") and skip them
                    if (cells.All(c => c.All(ch => ch == '-' || ch == ':' || char.IsWhiteSpace(ch))))
                        continue;

                    // Assume first valid row is header, rest are data
                    if (data.Headers.Count == 0)
                        data.Headers = cells;
                    else
                        data.Rows.Add(cells);
                }
            }
            return data;
        }

        private static string CleanCell(string raw)
        {
            string clean = raw.Trim();
            // Convert Markdown bold to RimWorld RichText
            clean = clean.Replace("**", "<b>");
            // If there was closing bold, it's ambiguous in simple replace, 
            // but RimWorld ignores malformed tags gracefully usually.
            // A safer specific replace:
            // "<b>Text<b>" -> "<b>Text</b>" logic is complex, 
            // so we'll just strip stars for safety or assume simple pairs.
            // For now, let's just strip stars to keep it clean:
            clean = clean.Replace("**", "");
            return clean;
        }
    }
}