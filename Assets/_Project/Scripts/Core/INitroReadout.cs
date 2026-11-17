namespace Unbounded.Core
{
    /// <summary>
    /// The Nitro state the HUD needs. A contract rather than a shared type, so
    /// the UI can draw the meter without depending on the handling assembly.
    /// </summary>
    public interface INitroReadout
    {
        /// <summary>Charge left in the tank, from 0 to 1.</summary>
        float NitroCharge { get; }

        /// <summary>Whether Nitro is being spent this step.</summary>
        bool IsBoosting { get; }

        /// <summary>Whether the tank ran dry and must recharge before it fires again.</summary>
        bool IsNitroLockedOut { get; }
    }
}
