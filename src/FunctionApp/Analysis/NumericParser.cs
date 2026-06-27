using System.Globalization;

namespace FunctionApp.Analysis;

/// <summary>
/// Single source of truth for numeric parsing across the deterministic
/// analysis layer.
///
/// Type inference (deciding whether a column is numeric) and statistics
/// (computing values for that column) MUST both parse through this method.
/// Previously they used different cultures — inference parsed with
/// InvariantCulture while statistics fell back to the host's CurrentCulture —
/// so on a non-invariant host the two layers could disagree about both
/// whether a value was numeric and what value it held.
///
/// Culture is fixed to InvariantCulture and grouping separators are NOT
/// permitted. This is deliberate: in a deterministic trust layer, rejecting
/// an ambiguous value (e.g. "1,5", which could mean 1.5 or 15 depending on
/// locale) is safer than silently misinterpreting it. Locale-configurable
/// parsing is a roadmap item; until then, input is expected in invariant
/// numeric format (decimal point '.', no thousands grouping).
/// </summary>
public static class NumericParser
{
    public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private const NumberStyles Styles =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint;

    public static bool TryParse(string? value, out decimal result) =>
        decimal.TryParse(value, Styles, Culture, out result);
}
