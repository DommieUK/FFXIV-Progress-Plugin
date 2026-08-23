using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Shared quest-categorization logic used by <see cref="MainScenarioQuestTracker"/>,
/// <see cref="FeatureQuestTracker"/> and <see cref="OtherQuestTracker"/>, so all three agree on exactly
/// one definition of "Main Scenario" and "Feature Quest" instead of drifting apart.
///
/// There is no single sheet flag literally named "Feature Quest" ("Blue Quest") - confirmed by loading
/// the Quest/JournalGenre/JournalCategory/JournalSection sheets directly (via Lumina against the live
/// game data, not decompilation guesswork): no JournalSection or JournalCategory is named "Feature
/// Quests", and no Addon (UI text) entry contains that string in English either. The in-game blue "!"
/// marker is a map/NPC icon color (EventIconType), a rendering detail with no queryable category name.
///
/// So "Feature Quest" here is built as a union of the real, verifiable signals that cover most of what
/// the community means by the term:
/// - JournalSection "Class & Job Quests" and "Chronicles of a New Era" (raid/trial questlines), and any
///   JournalSection starting with "Allied Society Quests" (beast tribes) - confirmed via the live
///   JournalSection sheet, which currently has two dated variants of each of those.
/// - JournalCategory "Grand Company Quests", "Records of Unusual Endeavors" (deep dungeons, Ishgardian
///   Restoration, ...) and "Special Quests" (Great Hunt and other one-off feature unlocks).
/// - Quest.ClassJobUnlock (job unlock) and the duty-unlock chain (Quest.InstanceContentUnlock plus the
///   reverse ContentFinderCondition.UnlockCriteria/UnlockCriteria2 lookup) - the same RowRef signals
///   QuestCategorization used before, restored here since they're real and non-guessed.
///
/// What this deliberately does NOT cover: individual Aether Current quests, aetheryte/location unlock
/// quests, and misc system unlocks (Market Board, Chocobo, Glamour, Triple Triad, ...). These sit inside
/// ordinary regional "Sidequests" categories with no distinguishing field - same bucket as any generic
/// fetch quest - so there is no real flag to key on without guessing. They fall into OtherQuests.
///
/// Spot-checked against "The Company You Keep" (Grand Company enrollment): the game's own data files
/// this under Section "Main Scenario (A Realm Reborn through Endwalker)" with the "Man" quest-code
/// prefix, i.e. it's already a genuine Main Scenario Quest per the game's own categorization, not a
/// separate Feature Quest - so it's correctly excluded here and left to MainScenarioQuestTracker.
/// </summary>
public static class QuestCategorization
{
    private static readonly HashSet<string> FeatureJournalSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class & Job Quests",
        "Chronicles of a New Era",
    };

    private static readonly HashSet<string> FeatureJournalCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Grand Company Quests",
        "Records of Unusual Endeavors",
        "Special Quests",
    };

    public static bool IsMainScenarioQuest(Quest quest)
    {
        var sectionName = GetJournalSectionName(quest);
        return sectionName.Contains("Main Scenario", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFeatureQuest(Quest quest, HashSet<uint> dutyUnlockQuestIds)
    {
        var sectionName = GetJournalSectionName(quest);
        if (FeatureJournalSections.Contains(sectionName))
            return true;
        if (sectionName.StartsWith("Allied Society Quests", StringComparison.OrdinalIgnoreCase))
            return true;

        if (FeatureJournalCategories.Contains(GetJournalCategoryName(quest)))
            return true;

        return UnlocksJob(quest) || UnlocksDuty(quest, dutyUnlockQuestIds);
    }

    private static string GetJournalSectionName(Quest quest)
    {
        if (!quest.JournalGenre.IsValid)
            return string.Empty;
        var journalGenre = quest.JournalGenre.Value;

        if (!journalGenre.JournalCategory.IsValid)
            return string.Empty;
        var journalCategory = journalGenre.JournalCategory.Value;

        if (!journalCategory.JournalSection.IsValid)
            return string.Empty;

        return journalCategory.JournalSection.Value.Name.ExtractText();
    }

    private static string GetJournalCategoryName(Quest quest)
    {
        if (!quest.JournalGenre.IsValid)
            return string.Empty;
        var journalGenre = quest.JournalGenre.Value;

        if (!journalGenre.JournalCategory.IsValid)
            return string.Empty;

        return journalGenre.JournalCategory.Value.Name.ExtractText();
    }

    // RowRef<T>.IsValid only checks that the target sheet contains RowId - it does NOT treat 0 as
    // "unset". ClassJob row 0 ("Adventurer") genuinely exists, so an unset ClassJobUnlock (raw value 0)
    // reads back as "valid" unless RowId is checked explicitly. Same defensive check applied below.
    private static bool UnlocksJob(Quest quest) => quest.ClassJobUnlock.RowId != 0 && quest.ClassJobUnlock.IsValid;

    private static bool UnlocksDuty(Quest quest, HashSet<uint> dutyUnlockQuestIds)
    {
        if (dutyUnlockQuestIds.Contains(quest.RowId))
            return true;

        if (quest.InstanceContentUnlock.RowId == 0 || !quest.InstanceContentUnlock.IsValid)
            return false;

        var instanceContentUnlock = quest.InstanceContentUnlock.Value;
        return instanceContentUnlock.ContentFinderCondition.RowId != 0 && instanceContentUnlock.ContentFinderCondition.IsValid;
    }

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
