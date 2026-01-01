using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Verse;

namespace GeminiPawnExport
{
    public static class GeminiAPIManager
    {
        private static readonly HttpClient client = new HttpClient();
        private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent";

        /// <summary>
        /// Sends the request to Gemini asynchronously and invokes the callback on completion.
        /// </summary>
        public static void SendRequest(string prompt, string pawnData, Action<string> callback)
        {
            string apiKey = GeminiMod.settings.apiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                callback?.Invoke("Error: API Key is missing. Please configure it in Mod Settings.");
                return;
            }

            // Run on a background thread to avoid freezing the UI
            Task.Run(async () =>
            {
                string result = await AskGemini(prompt, pawnData, apiKey);
                callback?.Invoke(result);
            });
        }

        private static async Task<string> AskGemini(string prompt, string data, string apiKey)
        {
            try
            {
                string fullContent = $"{prompt}\n\nDATA:\n{data}";

                // Manual JSON escaping
                string escapedContent = fullContent
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "");

                string jsonPayload = $"{{\"contents\": [{{\"parts\": [{{\"text\": \"{escapedContent}\"}}]}}]}}";

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                string url = $"{Endpoint}?key={apiKey}";

                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return ParseGeminiResponse(responseBody);
                }
                else
                {
                    return $"Error {response.StatusCode}: {responseBody}";
                }
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }

        private static string ParseGeminiResponse(string json)
        {
            var match = Regex.Match(json, "\"text\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

            if (match.Success)
            {
                string encoded = match.Groups[1].Value;
                return Regex.Unescape(encoded);
            }

            return "Error: Could not parse response from Gemini. Raw: " + json;
        }
    }
}