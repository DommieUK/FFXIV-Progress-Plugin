using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVProgressPlugin.Trackers;

namespace FFXIVProgressPlugin.Sync;

/// <summary>
/// Builds a full <see cref="SnapshotPayload"/> from the enabled content trackers and the current
/// character's identity. Must be called on the game's Framework thread - it delegates to trackers
/// that read live game memory, which is unsafe to touch from a background thread.
/// </summary>
public static class SnapshotBuilder
{
    public static SnapshotPayload Build(
        Configuration config,
        ContentTrackerRegistry registry,
        IDataManager dataManager,
        IUnlockState unlockState,
        IPlayerState playerState,
        IPluginLog log)
    {
        var categories = new Dictionary<string, CategoryPayload>();
        var totalAll = 0;
        var completedAll = 0;

        foreach (var tracker in registry.All)
        {
            if (!registry.IsEnabled(config, tracker))
                continue;

            IReadOnlyList<TrackedItem> items;
            try
            {
                items = tracker.BuildItems(dataManager, unlockState, log);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Tracker {Category} threw while building items; skipping it for this sync", tracker.CategoryId);
                continue;
            }

            var total = items.Count;
            var completed = items.Count(i => i.Completed);
            var percent = total == 0 ? 0d : Math.Round(completed * 100.0 / total, 2);

            categories[tracker.CategoryId] = new CategoryPayload(items, new CategorySummary(total, completed, percent));

            totalAll += total;
            completedAll += completed;
        }

        var overallPercent = totalAll == 0 ? 0d : Math.Round(completedAll * 100.0 / totalAll, 2);
        var overall = new CategorySummary(totalAll, completedAll, overallPercent);

        var characterId = ResolveCharacterId(config, playerState, log);

        return new SnapshotPayload(
            characterId,
            config.IdentifierMode.ToString(),
            DateTime.UtcNow,
            categories,
            overall);
    }

    private static string ResolveCharacterId(Configuration config, IPlayerState playerState, IPluginLog log)
    {
        if (config.IdentifierMode == CharacterIdentifierMode.CustomProfileId)
            return config.CustomProfileId;

        try
        {
            if (!playerState.IsLoaded)
                return string.Empty;

            var name = playerState.CharacterName;
            var world = playerState.HomeWorld.IsValid ? playerState.HomeWorld.Value.Name.ExtractText() : string.Empty;
            return string.IsNullOrEmpty(world) ? name : $"{name}@{world}";
        }
        catch (Exception ex)
        {
            log.Debug(ex, "Failed to resolve character name/world identifier");
            return string.Empty;
        }
    }
}
