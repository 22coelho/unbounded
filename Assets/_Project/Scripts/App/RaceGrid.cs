namespace Unbounded.App
{
    /// <summary>
    /// The starting Grid. The player takes the back of the Grid and the
    /// opponents line up two abreast ahead, so the Race opens with the whole
    /// field in view and a chase rather than a clear road.
    /// </summary>
    public static class RaceGrid
    {
        /// <summary>How many AI opponents make a full field.</summary>
        public const int OpponentCount = 5;

        /// <summary>Distance between Grid rows, in metres.</summary>
        public const float RowSpacing = 6f;

        /// <summary>How far either side of the Centreline a Grid column sits.</summary>
        public const float ColumnOffset = 2.6f;

        /// <summary>Distance along the Track for an opponent slot, numbered from zero.</summary>
        public static float DistanceFor(int slot) =>
            LapProgressTracker.StartDistance + (slot / 2 + 1) * RowSpacing;

        /// <summary>Lateral offset from the Centreline for an opponent slot; even slots sit left.</summary>
        public static float LateralFor(int slot) => slot % 2 == 0 ? -ColumnOffset : ColumnOffset;
    }
}
