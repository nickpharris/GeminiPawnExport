using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Verse;

namespace MyRimWorldMod
{
    public static class GeminiAPIManager
    {
        private static readonly HttpClient client = new HttpClient();
        private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent";

        public static async Task<string> AskGemini(string prompt, string data, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return "Error: API Key is missing. Please configure it in Mod Settings.";
            }

            try
            {
                // Combine prompt and data
                string fullContent = $"{prompt}\n\nDATA:\n{data}";

                // Escape quotes for JSON
                string jsonContent = fullContent.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

                // Construct JSON payload manually to avoid Newtonsoft.Json dependency
                string jsonPayload = $"{{\"contents\": [{{\"parts\": [{{\"text\": \"{jsonContent}\"}}]}}]}}";

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
            // Simple regex to extract the "text" field from the JSON response
            // Response structure: "text": "ACTUAL CONTENT"
            // We look for the pattern and extract the group.

            // Regex explanation: Match "text": " then capture everything until the next unescaped quote
            var match = Regex.Match(json, "\"text\":\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

            if (match.Success)
            {
                string encoded = match.Groups[1].Value;
                // Unescape JSON characters (like \n, \", etc.)
                return Regex.Unescape(encoded);
            }

            return "Error: Could not parse response from Gemini.";
        }
    }
}