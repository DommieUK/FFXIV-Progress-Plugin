using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks non-Main-Scenario quests that the game's own data marks as belonging to a "feature" category
/// (job/class quests, beast tribes, Chronicles of a New Era, Grand Company, Records of Unusual Endeavors,
/// Special Quests) or that unlock a job or a duty. See <see cref="QuestCategorization"/> for exactly what
/// this covers and why - it deliberately excludes generic system unlocks (Aether Currents, aetherytes,
/// Market Board, Chocobo, ...) that have no real sheet flag distinguishing them from ordinary side quests.
/// </summary>
public sealed class FeatureQuestTracker : IContentTracker
{
    private static readonly HashSet<string> SpotCheckNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hallo Halatali",
        "The Way of the Samurai",
        "The Company You Keep",
    };

    public string CategoryId => "FeatureQuests";

    public string DisplayName => "Feature Quests";

    public bool DefaultEnabled => true;

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            var sheet = dataManager.GetExcelSheet<Quest>();
            if (sheet == null)
                return items;

            var dutyUnlockQuestIds = QuestCategorization.BuildDutyUnlockQuestIds(dataManager, log, CategoryId);

            foreach (var quest in sheet)
            {
                try
                {
                    var name = quest.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (QuestCategorization.IsMainScenarioQuest(quest))
                    {
                        // "The Company You Keep" (Grand Company enrollment) is a spot check the project
                        // expected to land here, but the game's own data files it as genuine Main
                        // Scenario (Id prefix "Man...", Section "Main Scenario...") - logged here so that
                        // discrepancy is visible in every sync, not just the one-off investigation.
                        if (SpotCheckNames.Contains(name))
                        {
                            log.Information(
                                "[{Category}] Spot check '{Name}': RowId={RowId} is Main Scenario per the game's own data, not a Feature Quest - excluded here",
                                CategoryId, name, quest.RowId);
                        }

                        continue;
                    }

                    if (!QuestCategorization.IsFeatureQuest(quest, dutyUnlockQuestIds))
                        continue;

                    var expansion = quest.Expansion.IsValid ? quest.Expansion.Value.Name.ExtractText() : string.Empty;
                    var level = quest.ClassJobLevel.Count > 0 ? quest.ClassJobLevel[0] : 0;

                    var completed = unlockState.IsQuestCompleted(quest);

                    if (SpotCheckNames.Contains(name))
                    {
                        log.Information(
                            "[{Category}] Spot check '{Name}': RowId={RowId}, Completed={Completed}",
                            CategoryId, name, quest.RowId, completed);
                    }

                    items.Add(new TrackedItem(quest.RowId, name, CategoryId, completed, expansion, level));
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a Quest row due to an error", CategoryId);
                }
            }

            log.Information("[{Category}] Quest rows matched: {Total}", CategoryId, items.Count);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
