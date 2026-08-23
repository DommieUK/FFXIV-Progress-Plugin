using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks non-Main-Scenario quests that unlock a job or a duty, per <see cref="QuestCategorization"/>.
/// This does NOT cover quests that unlock other kinds of features (Market Board, Chocobo, Materia
/// Melding, Duty Roulette, ...) - no reliable, non-hardcoded signal for those exists in the game's data
/// (see QuestCategorization's doc comment for what was checked).
/// </summary>
public sealed class UnlockQuestTracker : IContentTracker
{
    public string CategoryId => "UnlockQuests";

    public string DisplayName => "Unlock Quests";

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

            var jobUnlockCount = 0;
            var dutyUnlockCount = 0;
            var jobUnlockSpotChecks = new List<string>();

            foreach (var quest in sheet)
            {
                try
                {
                    var name = quest.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (QuestCategorization.IsMainScenarioQuest(quest))
                        continue;

                    var unlocksJob = QuestCategorization.UnlocksJob(quest);
                    var unlocksDuty = QuestCategorization.UnlocksDuty(quest, dutyUnlockQuestIds);
                    if (!unlocksJob && !unlocksDuty)
                        continue;

                    if (unlocksJob)
                    {
                        jobUnlockCount++;
                        if (jobUnlockSpotChecks.Count < 10)
                            jobUnlockSpotChecks.Add($"{name} -> {quest.ClassJobUnlock.Value.Name.ExtractText()}");
                    }

                    if (unlocksDuty)
                        dutyUnlockCount++;

                    var expansion = quest.Expansion.IsValid ? quest.Expansion.Value.Name.ExtractText() : string.Empty;
                    var level = quest.ClassJobLevel.Count > 0 ? quest.ClassJobLevel[0] : 0;

                    var completed = unlockState.IsQuestCompleted(quest);

                    items.Add(new TrackedItem(quest.RowId, name, CategoryId, completed, expansion, level));
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a Quest row due to an error", CategoryId);
                }
            }

            log.Information(
                "[{Category}] Total: {Total}, job-unlock quests: {JobUnlocks}, duty-unlock quests: {DutyUnlocks}. Job-unlock spot check (name -> job): {SpotChecks}",
                CategoryId, items.Count, jobUnlockCount, dutyUnlockCount, string.Join(" | ", jobUnlockSpotChecks));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
