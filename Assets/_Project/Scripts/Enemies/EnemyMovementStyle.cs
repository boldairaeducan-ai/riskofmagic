namespace RiskOfMagic.Enemies
{
    public enum EnemyMovementStyle : byte
    {
        /// <summary>Straight at the target.</summary>
        Direct,
        /// <summary>Approaches from the target's sides/back, then commits.</summary>
        Flank,
        /// <summary>Keeps a distance band, backs off when approached, stops to attack.</summary>
        Kite
    }
}
