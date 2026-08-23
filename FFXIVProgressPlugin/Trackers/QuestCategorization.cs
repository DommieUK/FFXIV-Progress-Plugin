using System;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Shared quest-categorization logic used by <see cref="MainScenarioQuestTracker"/> and
/// <see cref="OtherQuestTracker"/>, so both agree on exactly one definition of "Main Scenario" instead of
/// drifting apart.
/// </summary>
public static class QuestCategorization
{
    public static bool IsMainScenarioQuest(Quest quest)
    {
        if (!quest.JournalGenre.IsValid)
            return false;
        var journalGenre = quest.JournalGenre.Value;

        if (!journalGenre.JournalCategory.IsValid)
            return false;
        var journalCategory = journalGenre.JournalCategory.Value;

        if (!journalCategory.JournalSection.IsValid)
            return false;
        var sectionName = journalCategory.JournalSection.Value.Name.ExtractText();

        return sectionName.Contains("Main Scenario", StringComparison.OrdinalIgnoreCase);
    }
}
