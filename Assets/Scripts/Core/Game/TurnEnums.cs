namespace AbsoluteZero.Core.Game
{
    public enum TurnPhase : byte
    {
        WaitingForPlayers = 0,
        PrepTurn,
        AttackTurn,
        GameOver
    }

    public enum ActionType : byte
    {
        None = 0,
        Attack,
        Defend,
        Charge
    }
}
