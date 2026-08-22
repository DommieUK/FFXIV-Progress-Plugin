namespace FFXIVProgressPlugin.Trackers;

public sealed record TrackedItem(
    uint Id,
    string Name,
    string Category,
    bool Completed,
    string Expansion,
    int Level);

public sealed record CategorySummary(int Total, int Completed, double Percent);
