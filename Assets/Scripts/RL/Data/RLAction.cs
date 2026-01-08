namespace Vampire.RL
{
    /// <summary>
    /// RL action enumeration as specified in the design document
    /// </summary>
    public enum RLAction
    {
        MoveTowardPlayer = 0,
        MoveAwayFromPlayer = 1,
        MoveLeft = 2,
        MoveRight = 3,
        Attack = 4,
        Coordinate = 5,
        Flank = 6,
        Wait = 7
    }
}