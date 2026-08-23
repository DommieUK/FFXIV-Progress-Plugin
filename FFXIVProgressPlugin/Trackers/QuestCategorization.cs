using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Shared quest-categorization logic used by <see cref="MainScenarioQuestTracker"/>,
/// <see cref="UnlockQuestTracker"/> and <see cref="SideQuestTracker"/>, so the three trackers agree on
/// exactly one definition of "Main Scenario" and "unlocks something" instead of drifting apart.
///
/// There is no single flag on the Quest sheet meaning "this quest unlocks something" - it was checked
/// via decompiling Lumina.Excel.dll before writing this. Two signals are directly and reliably derivable:
///
/// - Job unlocks: Quest.ClassJobUnlock is a typed RowRef&lt;ClassJob&gt;, cross-confirmed by the reverse
///   field ClassJob.UnlockQuest (RowRef&lt;Quest&gt;). No guessing involved.
/// - Duty unlocks: ContentFinderCondition.UnlockCriteria/UnlockCriteria2 are RowRefs that resolve to a
///   Quest when their discriminant byte (UnlockType/UnlockType2) equals 1 - the exact same shape as the
///   Content/ContentLinkType pattern DutyContentTracker already relies on. This is supplemented by the
///   forward chain Quest.InstanceContentUnlock -> InstanceContent.ContentFinderCondition, which catches
///   any quest the reverse scan might miss for InstanceContent-backed duties specifically.
///
/// What is deliberately NOT covered: generic feature unlocks (Market Board, Chocobo, Materia Melding,
/// Duty Roulette, etc). No field, enum, or sheet link identifying "this quest unlocks feature X" exists
/// anywhere in the Quest sheet or its related sheets - the only remotely relevant field is
/// Quest.SystemReward (an undocumented, untyped ushort[2]), which cannot be interpreted without external
/// reverse-engineering knowledge rather than sheet structure. Hardcoding a guessed quest list for these
/// was rejected on purpose: it would silently go stale as new content ships. So "unlocks something" here
/// means "unlocks a job or a duty" specifically, not every possible kind of unlock.
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

    // RowRef<T>.IsValid only checks that the target sheet contains RowId - it does NOT treat 0 as "unset".
    // ClassJob row 0 ("Adventurer") genuinely exists, so an unset ClassJobUnlock (raw value 0) reads back
    // as "valid" unless RowId is checked explicitly. Same defensive check applied to every RowRef below.
    public static bool UnlocksJob(Quest quest) => quest.ClassJobUnlock.RowId != 0 && quest.ClassJobUnlock.IsValid;

    public static bool UnlocksDuty(Quest quest, HashSet<uint> dutyUnlockQuestIds)
    {
        if (dutyUnlockQuestIds.Contains(quest.RowId))
            return true;

        if (quest.InstanceContentUnlock.RowId == 0 || !quest.InstanceContentUnlock.IsValid)
            return false;

        var instanceContentUnlock = quest.InstanceContentUnlock.Value;
        return instanceContentUnlock.ContentFinderCondition.RowId != 0 && instanceContentUnlock.ContentFinderCondition.IsValid;
    }

    public static bool UnlocksSomething(Quest quest, HashSet<uint> dutyUnlockQuestIds)
        => UnlocksJob(quest) || UnlocksDuty(quest, dutyUnlockQuestIds);

    /// <summary>
    /// Scans ContentFinderCondition once and collects every Quest row id referenced by UnlockCriteria or
    /// UnlockCriteria2 when that slot's discriminant identifies it as a Quest (UnlockType/UnlockType2 == 1).
    /// </summary>
    public static HashSet<uint> BuildDutyUnlockQuestIds(IDataManager dataManager, IPluginLog log, string categoryId)
    {
        var ids = new HashSet<uint>();

        var cfcSheet = dataManager.GetExcelSheet<ContentFinderCondition>();
        if (cfcSheet == null)
            return ids;

        foreach (var cfc in cfcSheet)
        {
            try
            {
                if (cfc.UnlockType == 1 && cfc.UnlockCriteria.RowId != 0)
                    ids.Add(cfc.UnlockCriteria.RowId);

                if (cfc.UnlockType2 == 1 && cfc.UnlockCriteria2.RowId != 0)
                    ids.Add(cfc.UnlockCriteria2.RowId);
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[{Category}] Skipped a ContentFinderCondition row while building the duty-unlock quest index", categoryId);
            }
        }

        return ids;
    }
}
