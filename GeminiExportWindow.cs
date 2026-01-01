using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace GeminiPawnExport
{
    public class MainTabWindow_GeminiExport : MainTabWindow
    {
        // UI State
        private string promptText = "";
        private string rawResponse = "Ready to generate.";
        private List<DisplayBlock> displayBlocks;
        private Vector2 scrollPosition = Vector2.zero;

        // Thread-safe handling
        private object responseLock = new object();
        private string pendingResponse = null;

        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public override void PreOpen()
        {
            base.PreOpen();
            // Load the default prompt from settings when the window opens
            if (string.IsNullOrEmpty(promptText))
            {
                promptText = GeminiMod.settings.defaultPrompt;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // --- 1. Handle Async Responses (Thread Safety) ---
            string newResponse = null;
            lock (responseLock)
            {
                if (pendingResponse != null)
                {
                    newResponse = pendingResponse;
                    pendingResponse = null;
                }
            }
            if (newResponse != null) ProcessResponse(newResponse);


            // --- 2. Layout Definitions ---
            Rect topBarRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);

            Rect promptLabelRect = new Rect(inRect.x, topBarRect.yMax + 10f, inRect.width, 20f);
            Rect promptBoxRect = new Rect(inRect.x, promptLabelRect.yMax, inRect.width, 60f);

            float dividerY = promptBoxRect.yMax + 10f;
            Rect outRect = new Rect(inRect.x, dividerY + 10f, inRect.width, inRect.height - (dividerY + 10f));


            // --- 3. Draw Top Bar ---
            float curX = topBarRect.x;


            //Button to copy the extracted data from Rimworld to the clipboard
            if (Widgets.ButtonText(new Rect(curX, topBarRect.y, 120f, 30f), "Copy Extract"))
            {
                GUIUtility.systemCopyBuffer = GeneratePawnData();
                Messages.Message("Copied extract to clipboard.", MessageTypeDefOf.TaskCompletion, false);
            }

            curX += 130f;

            // Only visible if in Dev Mode, button to put extract into the log
            if (Prefs.DevMode)
            {
                if (Widgets.ButtonText(new Rect(curX, topBarRect.y, 120f, 30f), "Log Extract"))
                {
                    DebugDumpData();
                }
                curX += 130f;
            }

            // Send prompt and data to Gemini
            if (Widgets.ButtonText(new Rect(curX, topBarRect.y, 120f, 30f), "Send to Gemini"))
            {
                SendPawnDataToGemini();
            }
            curX += 130f;

            // Reset to default prompt
            if (Widgets.ButtonText(new Rect(curX, topBarRect.y, 120f, 30f), "Reset Prompt"))
            {
                promptText = GeminiMod.settings.defaultPrompt;
            }
            curX += 130f;

            // Open mod settings
            if (Widgets.ButtonText(new Rect(curX, topBarRect.y, 120f, 30f), "Settings"))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<GeminiMod>()));
            }
            curX += 130f;

            // Copy Gemini response to clipboard
            if (!string.IsNullOrEmpty(rawResponse))
            {

                if (Widgets.ButtonText(new Rect(curX, topBarRect.y, 120f, 30f), "Copy Results"))
                {
                    GUIUtility.systemCopyBuffer = rawResponse;
                    Messages.Message("Copied to clipboard.", MessageTypeDefOf.TaskCompletion, false);
                }
            }

            // --- 4. Draw Prompt Input ---
            Widgets.Label(promptLabelRect, "<b>Analysis Prompt:</b>");
            promptText = Widgets.TextArea(promptBoxRect, promptText);

            // --- 5. Draw Results Area ---
            Widgets.DrawLineHorizontal(inRect.x, dividerY, inRect.width);

            // Calculate content height
            float viewHeight = 0f;
            float viewWidth = outRect.width - 16f; // Account for scrollbar

            if (displayBlocks != null && displayBlocks.Count > 0)
            {
                foreach (var block in displayBlocks)
                {
                    viewHeight += block.CalculateHeight(viewWidth);
                    viewHeight += 10f; // Gap between blocks
                }
            }
            else
            {
                viewHeight = Text.CalcHeight(rawResponse, viewWidth) + 20f;
            }

            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect, true);

            if (displayBlocks != null && displayBlocks.Count > 0)
            {
                float curY = 0f;
                foreach (var block in displayBlocks)
                {
                    float h = block.CalculateHeight(viewWidth);
                    Rect blockRect = new Rect(0f, curY, viewWidth, h);

                    block.Draw(blockRect);

                    curY += h + 10f; // Add gap
                }
            }
            else
            {
                // Fallback for raw text or status messages
                Widgets.Label(viewRect, rawResponse);
            }

            Widgets.EndScrollView();
        }

        private void SendPawnDataToGemini()
        {
            this.rawResponse = "Collecting data and sending to API... please wait.";
            this.displayBlocks = null;

            // CHANGED: Use the helper method to get data
            string pawnData = GeneratePawnData();

            GeminiAPIManager.SendRequest(promptText, pawnData, OnGeminiResponseReceived);
        }

        // --- NEW Helper Method to Generate Data ---
        // extracted from SendPawnDataToGemini so the Debug button can reuse it
        private string GeneratePawnData()
        {
            //Use the new helper class (brought back from the original implementation)
            return GeminiExporter.GenerateReport();

            //StringBuilder sb = new StringBuilder();
            //foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
            //{
            //    sb.AppendLine($"Name: {pawn.Name.ToStringShort}, Skills: Shooting {pawn.skills.GetSkill(SkillDefOf.Shooting).Level}, Melee {pawn.skills.GetSkill(SkillDefOf.Melee).Level}, Traits: {GetTraits(pawn)}");
            //}
            //return sb.ToString();
        }

        // --- NEW Debug Action ---
        private void DebugDumpData()
        {
            string data = GeneratePawnData();

            // Log to RimWorld Console (Dev Log)
            Log.Message("<b>[GeminiExport] Generated Payload:</b>\n" + data);

            // Show a small notification so you know it happened
            Messages.Message("Payload logged to Dev Console.", MessageTypeDefOf.TaskCompletion, false);
        }

        //private string GetTraits(Pawn p)
        //{
        //    if (p.story == null || p.story.traits == null) return "";
        //    List<string> traits = new List<string>();
        //    foreach (var t in p.story.traits.allTraits)
        //    {
        //        traits.Add(t.Label);
        //    }
        //    return string.Join(", ", traits);
        //}

        private void OnGeminiResponseReceived(string response)
        {
            lock (responseLock)
            {
                pendingResponse = response;
            }
        }

        private void ProcessResponse(string response)
        {

            //If dev mode is on, dump the raw response to the log
            if (Prefs.DevMode)
            {
                Log.Message("Raw response from Gemini:\n" + response);
            }

            this.rawResponse = response;
            // Parse full mixed content
            this.displayBlocks = MarkdownParser.Parse(response);
            this.scrollPosition = Vector2.zero;
        }
    }
}