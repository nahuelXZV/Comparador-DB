namespace Application.Models;

public sealed record ComparisonRequest(
    string OriginConnectionString,
    string DestinationConnectionString,
    string OriginSchema,
    string DestinationSchema,
    bool CompareTables,
    bool CompareColumns,
    bool CompareColumnProperties);
