using System;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;

namespace AbsoluteZero.Core.Common
{
    /// <summary>Presentation-only diagnostics. Never changes match rules or input.</summary>
    public static class GameplayDebugDisplay
    {
        public static bool Enabled
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "--az-debug-ui");
                return index >= 0 && index + 1 < args.Length && args[index + 1] == "1";
            }
        }

        public static bool ShowItemNames =>
            Enabled;

        public static string ItemLabel(string name, string uses, bool showName) =>
            showName ? (string.IsNullOrEmpty(uses) ? name : $"{name}\n{uses}") : uses;
    }
}
