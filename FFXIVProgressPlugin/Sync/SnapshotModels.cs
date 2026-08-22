using System;
using System.Collections.Generic;
using FFXIVProgressPlugin.Trackers;

namespace FFXIVProgressPlugin.Sync;

public sealed record CategoryPayload(IReadOnlyList<TrackedItem> Items, CategorySummary Summary);

public sealed record SnapshotPayload(
    string CharacterId,
    string IdentifierMode,
    DateTime GeneratedAtUtc,
    Dictionary<string, CategoryPayload> Categories,
    CategorySummary Overall);
