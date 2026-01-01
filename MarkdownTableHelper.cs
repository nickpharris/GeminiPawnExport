using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace GeminiPawnExport
{
    // --- Data Structures ---

    public enum BlockType
    {
        Text,
        Table
    }

    public abstract class DisplayBlock
    {
        public BlockType Type;
        public abstract float CalculateHeight(float width);
        public abstract void Draw(Rect rect);
    }

    public class TextBlock : DisplayBlock
    {
        public string Content;

        public TextBlock(string text)
        {
            this.Type = BlockType.Text;
            this.Content = text;
        }

        public override float CalculateHeight(float width)
        {
            return Text.CalcHeight(Content, width);
        }

        public override void Draw(Rect rect)
        {
            Widgets.Label(rect, Content);
        }
    }

    public class TableBlock : DisplayBlock
    {
        public List<string> Headers;
        public List<List<string>> Rows;

        public TableBlock()
        {
            this.Type = BlockType.Table;
            Headers = new List<string>();
            Rows = new List<List<string>>();
        }

        public override float CalculateHeight(float width)
        {
            if (Headers.Count == 0) return 0f;

            float colWidth = width / Headers.Count;
            float totalHeight = 30f; // Header height

            foreach (var row in Rows)
            {
                float maxRowH = 24f; // Min row height
                foreach (var cell in row)
                {
                    float h = Text.CalcHeight(cell, colWidth);
                    if (h > maxRowH) maxRowH = h;
                }
                totalHeight += maxRowH;
            }
            return totalHeight + 10f; // Buffer
        }

        public override void Draw(Rect outRect)
        {
            if (Headers.Count == 0) return;

            // Save font
            GameFont originalFont = Text.Font;
            Text.Font = GameFont.Tiny;

            int colCount = Headers.Count;
            float colWidth = outRect.width / colCount;
            float currentY = outRect.y;

            // 1. Draw Headers
            float headerHeight = 30f;
            for (int i = 0; i < colCount; i++)
            {
                Rect cellRect = new Rect(outRect.x + (i * colWidth), currentY, colWidth, headerHeight);
                Widgets.DrawHighlight(cellRect);

                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(cellRect, Headers[i]);
                Text.Anchor = TextAnchor.UpperLeft;

                Widgets.DrawLineVertical(cellRect.x + cellRect.width, cellRect.y, cellRect.height);
            }

            currentY += headerHeight;
            Widgets.DrawLineHorizontal(outRect.x, currentY, outRect.width);

            // 2. Draw Rows
            foreach (var row in Rows)
            {
                // Calc height for this specific row
                float rowHeight = 24f;
                for (int i = 0; i < row.Count && i < colCount; i++)
                {
                    float h = Text.CalcHeight(row[i], colWidth);
                    if (h > rowHeight) rowHeight = h;
                }

                // Draw cells
                for (int i = 0; i < row.Count && i < colCount; i++)
                {
                    Rect cellRect = new Rect(outRect.x + (i * colWidth), currentY, colWidth, rowHeight);
                    // ContractBy provides padding
                    Widgets.Label(cellRect.ContractedBy(4f), row[i]);
                    Widgets.DrawLineVertical(cellRect.x + cellRect.width, cellRect.y, rowHeight);
                }

                currentY += rowHeight;
                Widgets.DrawLineHorizontal(outRect.x, currentY, outRect.width);
            }

            Text.Font = originalFont;
        }
    }

    // --- Parser ---

    public static class MarkdownParser
    {
        public static List<DisplayBlock> Parse(string markdown)
        {
            var blocks = new List<DisplayBlock>();
            if (string.IsNullOrEmpty(markdown)) return blocks;

            var lines = markdown.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            List<string> textBuffer = new List<string>();
            List<string> tableBuffer = new List<string>();

            bool inTable = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                bool isTableLine = trimmed.StartsWith("|"); // Simple check for Markdown table

                if (isTableLine)
                {
                    if (!inTable)
                    {
                        // Switching from Text to Table
                        if (textBuffer.Count > 0)
                        {
                            blocks.Add(CreateTextBlock(textBuffer));
                            textBuffer.Clear();
                        }
                        inTable = true;
                    }
                    tableBuffer.Add(trimmed);
                }
                else
                {
                    if (inTable)
                    {
                        // Switching from Table to Text
                        if (tableBuffer.Count > 0)
                        {
                            var tb = CreateTableBlock(tableBuffer);
                            if (tb != null) blocks.Add(tb);
                            tableBuffer.Clear();
                        }
                        inTable = false;
                    }
                    textBuffer.Add(line); // Keep original whitespace for text
                }
            }

            // Flush remaining buffers
            if (textBuffer.Count > 0) blocks.Add(CreateTextBlock(textBuffer));
            if (tableBuffer.Count > 0)
            {
                var tb = CreateTableBlock(tableBuffer);
                if (tb != null) blocks.Add(tb);
            }

            return blocks;
        }

        private static TextBlock CreateTextBlock(List<string> lines)
        {
            string raw = string.Join("\n", lines);
            // Basic Markdown Cleanup

            // Bold **text** -> <b>text</b>
            // Note: This is a simple regex replacer. Nested tags might fail, but usually fine for LLM output.
            string processed = Regex.Replace(raw, @"\*\*(.*?)\*\*", "<b>$1</b>");

            // Italics *text* -> <i>text</i> (Basic check to avoid bullet points)
            processed = Regex.Replace(processed, @"(?<!\*)\*(?!\*)(.*?)\*", "<i>$1</i>");

            // Bullet points
            processed = Regex.Replace(processed, @"^\s*[\-\*]\s+", " • ", RegexOptions.Multiline);

            // Headers ## Header -> <b>Header</b>
            processed = Regex.Replace(processed, @"^#+\s+(.*)", "<b><size=16>$1</size></b>", RegexOptions.Multiline);

            return new TextBlock(processed);
        }

        private static TableBlock CreateTableBlock(List<string> lines)
        {
            TableBlock block = new TableBlock();

            foreach (var line in lines)
            {
                // Identify lines that look like table rows: "| Cell | Cell |"
                if (line.StartsWith("|"))
                {
                    // Remove leading/trailing pipes and split
                    var cells = line.Split('|')
                        .Skip(1).Take(line.Split('|').Length - 2)
                        .Select(c => CleanCell(c))
                        .ToList();

                    // Detect separator lines (e.g., "|---|---|") and skip them
                    bool isSeparator = cells.All(c => string.IsNullOrWhiteSpace(c) || c.All(ch => ch == '-' || ch == ':' || char.IsWhiteSpace(ch)));

                    if (isSeparator) continue;

                    if (block.Headers.Count == 0)
                        block.Headers = cells;
                    else
                        block.Rows.Add(cells);
                }
            }

            if (block.Headers.Count == 0) return null; // Invalid table
            return block;
        }

        private static string CleanCell(string raw)
        {
            string clean = raw.Trim();
            // Convert Markdown bold to RimWorld RichText within cells
            clean = clean.Replace("**", ""); // Strip stars in tables to save space/complexity
            return clean;
        }
    }
}