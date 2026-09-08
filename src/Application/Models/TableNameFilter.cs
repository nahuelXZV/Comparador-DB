namespace Application.Models;

public sealed class TableNameFilter
{
    private readonly string[] _patterns;

    public TableNameFilter(TableNameFilterMode mode, string? patterns)
    {
        Mode = mode;
        _patterns = (patterns ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public TableNameFilterMode Mode { get; }

    public bool ShouldExclude(string tableName) => _patterns.Any(pattern => Mode switch
    {
        TableNameFilterMode.StartsWith => tableName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
        TableNameFilterMode.EndsWith => tableName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase),
        TableNameFilterMode.Contains => tableName.Contains(pattern, StringComparison.OrdinalIgnoreCase),
        _ => false
    });
}
