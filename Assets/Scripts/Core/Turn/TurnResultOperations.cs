using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Turn
{
    internal static class TurnResultOperations
    {
        internal static void CompactInventories(PlayerState[] players)
        {
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null)
                    players[i].GetInventory().CompactSlots();
        }
        internal static string DuelSummary(int turn, string[] mainNames, string[] subNames,
            PlayerState[] players, float[] temperatures, int winner)
        {
            return $"Turn{turn}" +
                $" | P0: {mainNames[0]}(sub:{subNames[0]}) P1: {mainNames[1]}(sub:{subNames[1]})" +
                $" | P0: {temperatures[0]:F1}→{players[0].Temperature.Value:F1}°" +
                $" P1: {temperatures[1]:F1}→{players[1].Temperature.Value:F1}°" +
                $" | {(winner >= 0 ? $"P{winner} WINS" : "no death")}";
        }
        internal static string MultiSummary(int turn, PlayerState[] players,
            float[] temperatures, RoundEndResult roundEnd)
        {
            var summaryMulti = new System.Text.StringBuilder($"Turn{turn}");
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null)
                    summaryMulti.Append($" | P{i}: {temperatures[i]:F1}→{players[i].Temperature.Value:F1}°");
            summaryMulti.Append(roundEnd.IsRoundOver ? $" | ROUND OVER (winner={roundEnd.WinnerSeat})" : " | continue");
            return summaryMulti.ToString();
        }
    }
}
