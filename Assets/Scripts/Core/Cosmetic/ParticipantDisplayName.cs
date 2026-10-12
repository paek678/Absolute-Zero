using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AbsoluteZero.Core.Cosmetic
{
    public static class ParticipantDisplayName
    {
        static readonly Regex Human = new(@"^[가-힣a-zA-Z0-9]+$", RegexOptions.Compiled);
        public static bool TryHuman(string raw, out string validated)
        {
            validated = null;
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 128) return false;
            string text;
            try { text = raw.Trim().Normalize(NormalizationForm.FormC); }
            catch (ArgumentException) { return false; }
            if (!Human.IsMatch(text) || new StringInfo(text).LengthInTextElements is < 2 or > 8) return false;
            validated = text; return true;
        }
        public static string Bot(string raw)
        {
            var value = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(raw ?? "");
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                bool invalid = false;
                foreach (char c in element) if (char.IsControl(c) || c == '<' || c == '>') invalid = true;
                if (invalid) continue;
                if (Encoding.UTF8.GetByteCount(value.ToString()) + Encoding.UTF8.GetByteCount(element) > 60) break;
                value.Append(element);
            }
            return value.Length == 0 ? "봇" : value.ToString();
        }
        public static string Fallback(int seat) => $"플레이어 {seat + 1}";

        public static string Lobby(string nickname, string participantId)
        {
            if (TryHuman(nickname, out var name)) return name;
            string shortId = string.IsNullOrEmpty(participantId) ? "Unknown"
                : participantId.Substring(0, System.Math.Min(6, participantId.Length));
            return $"Player_{shortId}";
        }
    }
}
