namespace TecmoSBGame.State;

/// <summary>
/// Flag for how penalties should be detected and enforced.
///
/// Default is <see cref="Off"/> so existing gameplay behavior is unchanged.
/// </summary>
public enum PenaltyRuleset
{
    /// <summary>
    /// Penalties are completely disabled (no detection, no assessment).
    /// </summary>
    Off = 0,

    /// <summary>
    /// Basic, deterministic penalty detection/enforcement scaffold.
    /// Intended as a stepping stone toward Tecmo-parity.
    /// </summary>
    Basic = 1,
}
