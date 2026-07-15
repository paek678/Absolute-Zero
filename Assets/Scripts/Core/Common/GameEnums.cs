namespace AbsoluteZero.Core.Common
{
    public enum TurnPhase : byte
    {
        WaitingForPlayers = 0,
        PrepPhase = 1,
        AttackPhase = 2,
        ResolutionPhase = 3,
        RoundOver = 4
    }
}
