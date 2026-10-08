using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HealthTrack.Models.Training;

public sealed class PositiveQuantityAttribute : ValidationAttribute
{
    public PositiveQuantityAttribute() : base("Enter a positive quantity, such as 1 cup or 150 g.") { }

    public override bool IsValid(object? value)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text)) return true; // Required handles empty values.
        var match = Regex.Match(text.Trim(), @"^[+-]?\d+(?:[.,]\d+)?");
        if (!match.Success) return !text.TrimStart().StartsWith('-');
        return decimal.TryParse(match.Value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var quantity) && quantity > 0;
    }
}
