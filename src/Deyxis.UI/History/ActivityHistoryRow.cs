namespace Deyxis.UI.History;

public sealed record ActivityHistoryRow(
    string ProviderId,
    string Category,
    string State,
    string Title,
    string Timestamp);
