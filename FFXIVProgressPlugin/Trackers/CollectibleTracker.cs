using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Generic tracker for simple "owned vs. not owned" collectible categories (Mounts, Minions, Titles,
/// Emotes, Orchestrion Rolls, Facewear, Triple Triad Cards, Bardings) that all follow the same shape:
/// enumerate a single Lumina sheet and ask Dalamud's IUnlockState whether each row is unlocked.
///
/// None of these sheets (nor their companion "Transient" sheets, checked individually per category)
/// expose an expansion/patch field the way ContentFinderCondition.RequiredExVersion or Quest.Expansion
/// do, so Expansion is always left empty here rather than guessed - see the per-category registration
/// comments in <see cref="ContentTrackerRegistry"/> for what was checked for each one.
/// </summary>
public sealed class CollectibleTracker<TRow> : IContentTracker
    where TRow : struct, IExcelRow<TRow>
{
    private readonly Func<TRow, string> getName;
    private readonly Func<IUnlockState, TRow, bool> isUnlocked;
    private readonly Func<IUnlockState, bool>? isReady;
    private readonly Action<IPluginLog>? onNotReady;
    private readonly HashSet<string> spotCheckNames;

    public string CategoryId { get; }

    public string DisplayName { get; }

    public bool DefaultEnabled { get; }

    public CollectibleTracker(
        string categoryId,
        string displayName,
        bool defaultEnabled,
        Func<TRow, string> getName,
        Func<IUnlockState, TRow, bool> isUnlocked,
        Func<IUnlockState, bool>? isReady = null,
        Action<IPluginLog>? onNotReady = null,
        IEnumerable<string>? spotCheckNames = null)
    {
        CategoryId = categoryId;
        DisplayName = displayName;
        DefaultEnabled = defaultEnabled;
        this.getName = getName;
        this.isUnlocked = isUnlocked;
        this.isReady = isReady;
        this.onNotReady = onNotReady;
        this.spotCheckNames = new HashSet<string>(spotCheckNames ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            if (isReady != null && !isReady(unlockState))
            {
                log.Warning("[{Category}] Unlock data isn't loaded yet; skipping this sync", CategoryId);

                try
                {
                    onNotReady?.Invoke(log);
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Failed to trigger data load", CategoryId);
                }

                return items;
            }

            var sheet = dataManager.GetExcelSheet<TRow>();
            if (sheet == null)
            {
                log.Warning("[{Category}] Sheet was unavailable from Lumina", CategoryId);
                return items;
            }

            var rowCount = 0;
            var ownedCount = 0;

            foreach (var row in sheet)
            {
                rowCount++;

                try
                {
                    var name = getName(row);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var completed = isUnlocked(unlockState, row);
                    if (completed)
                        ownedCount++;

                    if (spotCheckNames.Contains(name))
                    {
                        log.Information(
                            "[{Category}] Spot check '{Name}': RowId={RowId}, Owned={Owned}",
                            CategoryId, name, row.RowId, completed);
                    }

                    items.Add(new TrackedItem(row.RowId, name, CategoryId, completed, string.Empty, 0));
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a row due to an error", CategoryId);
                }
            }

            log.Information(
                "[{Category}] rows enumerated: {Total}, owned: {Owned}",
                CategoryId, rowCount, ownedCount);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
