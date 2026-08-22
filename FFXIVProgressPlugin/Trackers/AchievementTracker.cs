using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks Achievement completion via Dalamud's IUnlockState.IsAchievementComplete, which wraps the
/// game's own achievement unlock state. The Achievement sheet doesn't expose an expansion, so that
/// field is left empty rather than guessed.
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
