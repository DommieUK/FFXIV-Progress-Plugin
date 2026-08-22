using System;
using System.Collections.Generic;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Owns the list of available <see cref="IContentTracker"/> instances. Add new categories here
/// (Deep Dungeon floors, Chocobo Racing, Triple Triad, Hunt marks, relic steps, ...) without touching
/// the sync/snapshot logic - anything implementing IContentTracker just needs to be added to this list.
/// </summary>
public sealed class ContentTrackerRegistry
{
    public IReadOnlyList<IContentTracker> All { get; }

    public ContentTrackerRegistry()
    {
        All = new List<IContentTracker>
        {
            new DutyContentTracker(
                "Dungeons",
                "Dungeons",
                defaultEnabled: true,
                contentTypeMatches: t => t.Equals("Dungeons", StringComparison.OrdinalIgnoreCase)),

            new DutyContentTracker(
                "Trials",
                "Trials",
                defaultEnabled: true,
                contentTypeMatches: t => t.Equals("Trials", StringComparison.OrdinalIgnoreCase)),

            new DutyContentTracker(
                "Raids",
                "Raids",
                defaultEnabled: true,
                contentTypeMatches: t => t.Equals("Raids", StringComparison.OrdinalIgnoreCase)),

            new DutyContentTracker(
                "AllianceRaids",
                "Alliance Raids",
                defaultEnabled: true,
                contentTypeMatches: t => t.Contains("Alliance", StringComparison.OrdinalIgnoreCase)),

            new DutyContentTracker(
                "VariantCriterion",
                "Variant & Criterion Dungeons",
                defaultEnabled: true,
                contentTypeMatches: t => t.Contains("Variant", StringComparison.OrdinalIgnoreCase)
                                          || t.Contains("Criterion", StringComparison.OrdinalIgnoreCase)),

            new MainScenarioQuestTracker(),

            new AchievementTracker(),
        };
    }

    public bool IsEnabled(Configuration config, IContentTracker tracker)
        => config.EnabledCategories.TryGetValue(tracker.CategoryId, out var enabled) ? enabled : tracker.DefaultEnabled;
}
