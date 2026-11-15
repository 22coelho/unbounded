namespace Unbounded.Core
{
    /// <summary>
    /// The race numbers the HUD needs. A contract rather than a shared type, so
    /// the UI can display a Lap without depending on the assembly that counts it.
    /// </summary>
    public interface IRaceReadout
    {
        /// <summary>The Lap being driven, counting from one.</summary>
        int CurrentLap { get; }

        /// <summary>How many Laps make a Race.</summary>
        int TotalLaps { get; }

        /// <summary>Whether the car is driving against the Track direction.</summary>
        bool IsWrongWay { get; }
    }
}
