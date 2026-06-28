namespace Utils.Mathematics;

/// <summary>
/// Specifies the grammatical gender used when converting a number to its text representation.
/// </summary>
public enum NumberGender
{
    /// <summary>Masculine gender (default for most languages).</summary>
    Masculine,

    /// <summary>Feminine gender (e.g. "une" instead of "un" in French).</summary>
    Feminine,

    /// <summary>Neuter gender.</summary>
    Neuter,
}
