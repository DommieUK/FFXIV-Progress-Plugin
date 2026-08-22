using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks Achievement completion via Dalamud's IUnlockState.IsAchievementComplete, which wraps the
/// game's own achievement unlock state.
///
/// Expansion is left empty: the Achievement sheet has no version/patch field, and neither does
/// AchievementCategory or AchievementKind (its only related sheets) - AchievementKind is a thematic
/// grouping like "Battle" or "Character", not a release version. There's no clean way to derive which
/// expansion an achievement belongs to from data the game itself exposes, so this is left blank rather
/// than guessed.
/// </summary>
public sealed class AchievementTracker : IContentTracker
{
    public string CategoryId => "Achievements";

    public string DisplayName => "Achievements";

    public bool DefaultEnabled => true;

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            if (!unlockState.IsAchievementListLoaded)
            {
                log.Debug("[{Category}] Achievement list isn't loaded yet; skipping this sync", CategoryId);
                return items;
            }

            var sheet = dataManager.GetExcelSheet<Achievement>();
            if (sheet == null)
                return items;

            foreach (var achievement in sheet)
            {
                try
                {
                    var name = achievement.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var completed = unlockState.IsAchievementComplete(achievement);

                    items.Add(new TrackedItem(achievement.RowId, name, CategoryId, completed, string.Empty, 0));
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped an Achievement row due to an error", CategoryId);
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
