using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using NativeUIState = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks completion of instanced duties (Dungeons, Trials, Raids, Alliance Raids, Variant &amp; Criterion
/// Dungeons, ...) that appear in the Duty Finder (or, for Variant &amp; Criterion dungeons, the separate
/// V&amp;C Dungeon Finder).
///
/// The game does not expose a simple "has this ContentFinderCondition been cleared" flag directly on the
/// ContentFinderCondition sheet. What it does expose is FFXIVClientStructs' UIState.IsInstanceContentCompleted,
/// a native function keyed by InstanceContent row id (a separate sheet from ContentFinderCondition). Most
/// duties link to their InstanceContent row via ContentFinderCondition.Content when ContentLinkType == 1 -
/// this tracker resolves that link at runtime (pulled live from Lumina every sync - never a hardcoded id
/// table) and asks the client directly whether that instance content has been completed.
///
/// An earlier version of this tracker instead cross-referenced the "Content Compendium" ContentsNote sheet
/// by duty name. That was a dead end: ContentsNote isn't a per-duty completion list at all - it's a much
/// smaller (~100 row) set of unrelated meta-challenges (e.g. "Dungeon Master", "Feeling Lucky"), so a name
/// match against it never succeeds for an actual dungeon/trial/raid name. UIState.IsInstanceContentCompleted
/// is the correct signal.
///
/// A handful of duties don't use ContentLinkType == 1 (e.g. some Gold Saucer or party-content-linked
/// entries) and have no InstanceContent row to resolve; those report as not completed since no clear
/// completion signal is available for them. This is a known, narrow limitation of the data the game exposes,
/// not a bug - it should never be excluded from the total.
/// </summary>
public sealed unsafe class DutyContentTracker : IContentTracker
{
    private static readonly HashSet<string> SpotCheckNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sastasha",
        "the Tam-Tara Deepcroft",
    };

    private readonly Func<ContentFinderCondition, bool> matchesCategory;

    public string CategoryId { get; }

    public string DisplayName { get; }

    public bool DefaultEnabled { get; }

    public DutyContentTracker(string categoryId, string displayName, bool defaultEnabled, Func<ContentFinderCondition, bool> matchesCategory)
    {
        CategoryId = categoryId;
        DisplayName = displayName;
        DefaultEnabled = defaultEnabled;
        this.matchesCategory = matchesCategory;
    }

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            var cfcSheet = dataManager.GetExcelSheet<ContentFinderCondition>();
            if (cfcSheet == null)
            {
                log.Warning("[{Category}] ContentFinderCondition sheet was unavailable from Lumina", CategoryId);
                return items;
            }

            var cfcRowCount = 0;
            var categoryMatchCount = 0;
            var resolvedCount = 0;

            foreach (var cfc in cfcSheet)
            {
                cfcRowCount++;

                try
                {
                    var name = cfc.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (!matchesCategory(cfc))
                        continue;

                    categoryMatchCount++;

                    var expansion = cfc.RequiredExVersion.IsValid
                        ? cfc.RequiredExVersion.Value.Name.ExtractText()
                        : string.Empty;
                    var level = (int)cfc.ClassJobLevelRequired;

                    // ContentLinkType == 1 means Content resolves to an InstanceContent row, whose id is
                    // what UIState.IsInstanceContentCompleted expects. Other link types (party content,
                    // Gold Saucer content, ...) have no equivalent native completion check exposed.
                    var hasSignal = cfc.ContentLinkType == 1;
                    var completed = false;
                    if (hasSignal)
                    {
                        var instanceContentId = cfc.Content.RowId;
                        completed = NativeUIState.IsInstanceContentCompleted(instanceContentId);
                        resolvedCount++;

                        if (SpotCheckNames.Contains(name))
                        {
                            log.Information(
                                "[{Category}] Spot check '{Name}': ContentFinderCondition.RowId={CfcRowId}, InstanceContent.RowId={InstanceContentId}, Completed={Completed}",
                                CategoryId, name, cfc.RowId, instanceContentId, completed);
                        }
                    }
                    else if (SpotCheckNames.Contains(name))
                    {
                        log.Information(
                            "[{Category}] Spot check '{Name}': ContentFinderCondition.RowId={CfcRowId}, ContentLinkType={ContentLinkType} has no InstanceContent link - no completion signal available",
                            CategoryId, name, cfc.RowId, cfc.ContentLinkType);
                    }

                    items.Add(new TrackedItem(cfc.RowId, name, CategoryId, completed, expansion, level));
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a ContentFinderCondition row due to an error", CategoryId);
                }
            }

            log.Information(
                "[{Category}] ContentFinderCondition rows enumerated: {Total}, matched this category: {Matched}, resolved to an InstanceContent completion signal: {Resolved}",
                CategoryId, cfcRowCount, categoryMatchCount, resolvedCount);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
