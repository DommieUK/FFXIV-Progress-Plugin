using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks every quest that isn't a Main Scenario Quest or a Feature Quest (see
/// <see cref="QuestCategorization"/> for exactly what those exclude). This is the largest quest category
/// by far - expect several thousand items, similar in scale to Achievements.
/// </summary>
public sealed class OtherQuestTracker : IContentTracker
{
    public string CategoryId => "OtherQuests";

    public string DisplayName => "Other Quests";

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
                        continue;

                    if (QuestCategorization.IsFeatureQuest(quest, dutyUnlockQuestIds))
                        continue;

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

            log.Information("[{Category}] Quest rows matched: {Total}", CategoryId, items.Count);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
