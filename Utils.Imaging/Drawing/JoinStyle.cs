namespace Utils.Drawing;

/// <summary>
/// Determines how two stroke segments are joined at their common endpoint.
/// </summary>
public enum JoinStyle
{
    /// <summary>
    /// The outer edges are extended to their intersection point (miter).
    /// Falls back to <see cref="Bevel"/> when the resulting miter length exceeds
    /// <c>miterLimit × halfWidth</c>.
    /// </summary>
    Miter,

    /// <summary>
    /// The outer corner is cut straight across, connecting the two outer endpoints
    /// with a flat edge at the stroke half-width distance from the join point.
    /// </summary>
    Bevel,

    /// <summary>
    /// The outer corner is rounded with a circular arc of radius equal to
    /// half the stroke width, centred on the join point.
    /// </summary>
    Round,
}
