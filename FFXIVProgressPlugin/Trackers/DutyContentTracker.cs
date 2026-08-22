using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using NativeContentsNote = FFXIVClientStructs.FFXIV.Client.Game.UI.ContentsNote;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Tracks completion of instanced duties (Dungeons, Trials, Raids, Alliance Raids, Variant &amp; Criterion
/// Dungeons, ...) that appear in the Duty Finder.
///
/// The game does not expose a simple "has this ContentFinderCondition been cleared" flag through
/// Dalamud or FFXIVClientStructs. What it does expose is the "Content Compendium" completion bitmask
/// (FFXIVClientStructs' ContentsNote, backing the in-game Content Compendium / duty journal), which is
/// keyed by its own ContentsNote sheet rather than by ContentFinderCondition. Both sheets use the same
/// human-readable duty name, so this tracker cross-references them by name at runtime (pulled live from
/// Lumina every sync - never a hardcoded id table) to resolve completion.
///
/// Content without a Content Compendium entry (e.g. some Guildhests) will report as not completed since
/// no clear signal is available; this is a known limitation of the data the game exposes.
/// </summary>
public sealed unsafe class DutyContentTracker : IContentTracker
{
    private readonly Func<string, bool> contentTypeMatches;

    public string CategoryId { get; }

    public string DisplayName { get; }

    public bool DefaultEnabled { get; }

    public DutyContentTracker(string categoryId, string displayName, bool defaultEnabled, Func<string, bool> contentTypeMatches)
    {
        CategoryId = categoryId;
        DisplayName = displayName;
        DefaultEnabled = defaultEnabled;
        this.contentTypeMatches = contentTypeMatches;
    }

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            NativeContentsNote* contentsNote;
            try
            {
                contentsNote = NativeContentsNote.Instance();
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[{Category}] Failed to access ContentsNote instance", CategoryId);
                return items;
            }

            if (contentsNote == null || contentsNote->State != NativeContentsNote.ContentsNoteState.Loaded)
            {
                log.Debug("[{Category}] Content Compendium data isn't loaded yet; skipping this sync", CategoryId);
                return items;
            }

            var noteSheet = dataManager.GetExcelSheet<ContentsNote>();
            if (noteSheet == null)
                return items;

            var noteRowIdByName = new Dictionary<string, uint>();
            foreach (var note in noteSheet)
            {
                try
                {
                    var name = note.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name) || noteRowIdByName.ContainsKey(name))
                        continue;
                    noteRowIdByName[name] = note.RowId;
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a ContentsNote row due to an error", CategoryId);
                }
            }

            var cfcSheet = dataManager.GetExcelSheet<ContentFinderCondition>();
            if (cfcSheet == null)
                return items;

            foreach (var cfc in cfcSheet)
            {
                try
                {
                    if (!cfc.IsInDutyFinder)
                        continue;

                    var name = cfc.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var typeName = cfc.ContentType.IsValid ? cfc.ContentType.Value.Name.ExtractText() : string.Empty;
                    if (!contentTypeMatches(typeName))
                        continue;

                    var expansion = cfc.RequiredExVersion.IsValid
                        ? cfc.RequiredExVersion.Value.Name.ExtractText()
                        : string.Empty;
                    var level = (int)cfc.ClassJobLevelRequired;

                    var completed = noteRowIdByName.TryGetValue(name, out var noteRowId)
                        && contentsNote->IsContentNoteComplete((int)noteRowId);

                    items.Add(new TrackedItem(cfc.RowId, name, CategoryId, completed, expansion, level));
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a ContentFinderCondition row due to an error", CategoryId);
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
