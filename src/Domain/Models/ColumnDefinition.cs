namespace Domain.Models;

public sealed record ColumnDefinition(
    string Name,
    int Ordinal,
    string DataType,
    int? MaxLength,
    byte? Precision,
    byte? Scale,
    bool IsNullable,
    bool IsIdentity,
    decimal? IdentitySeed,
    decimal? IdentityIncrement,
    string? DefaultExpression);
