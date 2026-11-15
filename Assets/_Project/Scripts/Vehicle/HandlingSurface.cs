namespace Unbounded.Vehicle
{
    /// <summary>
    /// How much grip and drive the ground affords. The handling model treats
    /// these as explicit multipliers so a change of surface is a tuning
    /// decision rather than an emergent physics effect.
    /// </summary>
    public readonly struct HandlingSurface
    {
        public HandlingSurface(
            float engineForceMultiplier,
            float rollingResistanceMultiplier,
            float lateralGripMultiplier)
        {
            EngineForceMultiplier = engineForceMultiplier;
            RollingResistanceMultiplier = rollingResistanceMultiplier;
            LateralGripMultiplier = lateralGripMultiplier;
        }

        public readonly float EngineForceMultiplier;
        public readonly float RollingResistanceMultiplier;
        public readonly float LateralGripMultiplier;

        /// <summary>Asphalt: the baseline every other surface is tuned against.</summary>
        public static HandlingSurface Road => new HandlingSurface(1f, 1f, 1f);

        /// <summary>Grass: little drive, heavy rolling resistance, and little grip.</summary>
        public static HandlingSurface Grass => new HandlingSurface(0.35f, 8f, 0.55f);
    }
}
