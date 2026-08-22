using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks completion of Main Scenario Quests, resolved via the Quest sheet's journal section
/// (Quest -&gt; JournalGenre -&gt; JournalCategory -&gt; JournalSection, matched against the "Main Scenario"
/// section name pulled live from Lumina) and Dalamud's IUnlockState.IsQuestCompleted.
/// </summary>
public sealed class MainScenarioQuestTracker : IContentTracker
{
    public string CategoryId => "MainScenarioQuests";

    public string DisplayName => "Main Scenario Quests";

    public bool DefaultEnabled => true;

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            var sheet = dataManager.GetExcelSheet<Quest>();
            if (sheet == null)
                return items;

            foreach (var quest in sheet)
            {
                try
                {
                    var name = quest.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (!quest.JournalGenre.IsValid)
                        continue;
                    var journalGenre = quest.JournalGenre.Value;

                    if (!journalGenre.JournalCategory.IsValid)
                        continue;
                    var journalCategory = journalGenre.JournalCategory.Value;

                    if (!journalCategory.JournalSection.IsValid)
                        continue;
                    var sectionName = journalCategory.JournalSection.Value.Name.ExtractText();

                    if (!sectionName.Contains("Main Scenario", StringComparison.OrdinalIgnoreCase))
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
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
