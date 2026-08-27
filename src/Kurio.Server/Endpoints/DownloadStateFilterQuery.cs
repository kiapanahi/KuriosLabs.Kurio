using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Binds the <c>?filter=</c> query parameter of <c>GET /api/downloads</c> to a
///     <see cref="DownloadStateFilter" />.
/// </summary>
/// <remarks>
///     Minimal APIs bind bare enum parameters through the case-<em>sensitive</em>
///     <c>Enum.TryParse(string, out T)</c> overload, whereas the MVC <c>EnumConverter</c> this
///     endpoint used to go through parsed case-<em>insensitively</em>. Without this wrapper
///     <c>?filter=active</c> would stop working while <c>?filter=Active</c> kept working - a
///     silent break for existing clients. Implementing the minimal-API <c>TryParse</c> pattern
///     lets us keep the old, forgiving parse.
/// </remarks>
internal readonly record struct DownloadStateFilterQuery(DownloadStateFilter Value)
{
    /// <summary>
    ///     Parses a query-string value into a <see cref="DownloadStateFilter" />, accepting any
    ///     casing as well as comma-separated flag lists and numeric values.
    /// </summary>
    /// <returns>
    ///     <see langword="false" /> for an unrecognised or empty value, which MVC also rejected
    ///     with a 400.
    /// </returns>
    public static bool TryParse(string? value, IFormatProvider? provider, out DownloadStateFilterQuery result)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<DownloadStateFilter>(value, ignoreCase: true, out var filter))
        {
            result = new DownloadStateFilterQuery(filter);
            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
