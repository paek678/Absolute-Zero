#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using AbsoluteZero.Core.Match;

namespace AbsoluteZero.Core.Solo
{
    // Explicit development-only match configuration. Never mutates the shared asset.
    internal sealed class BalanceTrialRule : IGameModeRule
    {
        readonly GameModeRuleSnapshot _base;
        public int KillsToWin { get; }
        public int MaxRounds { get; }
        BalanceTrialRule(IGameModeRule rule, int kills, int rounds)
        { _base = GameModeRuleSnapshot.From(rule); KillsToWin = kills; MaxRounds = rounds; }
        internal static IGameModeRule Wrap(IGameModeRule rule)
        {
            if (PlaytestTelemetry.Arg("--az-playtest-drive", "") != "1") return rule;
            string k = PlaytestTelemetry.Arg("--az-trial-kills", "");
            string r = PlaytestTelemetry.Arg("--az-trial-rounds", "");
            if (k.Length == 0 && r.Length == 0) return rule;
            if (!int.TryParse(k, out int kills) || kills < 1 || kills > 5
                || !int.TryParse(r, out int rounds) || rounds < 1 || rounds > 5)
                throw new ArgumentException("Balance trial requires kills and rounds in [1,5]");
            return new BalanceTrialRule(rule, kills, rounds);
        }
        public WinConditionType WinCondition => _base.WinCondition;
        public int WinsRequired => _base.WinsRequired;
        public float PrepPhaseDuration => _base.PrepPhaseDuration;
        public int InitialRandomItems => _base.InitialRandomItems;
        public int MaxRandomItems => _base.MaxRandomItems;
        public int DeathmatchGrantCount => _base.DeathmatchGrantCount;
        public bool IsWindbreakerUnlimited => _base.IsWindbreakerUnlimited;
        public bool IsTarotAllowed => _base.IsTarotAllowed;
        public bool EnableGhostSystem => _base.EnableGhostSystem;
        public bool EnableReconnect => _base.EnableReconnect;
        public float GraceTimerSeconds => _base.GraceTimerSeconds;
    }
}
#endif
