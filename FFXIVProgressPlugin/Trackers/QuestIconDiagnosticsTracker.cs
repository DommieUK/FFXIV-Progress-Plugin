using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// TEMPORARY diagnostic tracker - not a real content category. Buckets every non-MSQ quest by its
/// Quest.EventIconType row id (the sheet that drives which map/NPC icon a quest shows), with a handful
/// of sample quest names per bucket, so a real player can eyeball which bucket corresponds to the
/// "blue ! feature quest" icon without needing to hardcode a guess. Remove once that's answered.
/// </summary>
public sealed class QuestIconDiagnosticsTracker : IContentTracker
{
    public string CategoryId => "QuestIconDiagnostics";

    public string DisplayName => "Quest Icon Diagnostics (temporary)";

    public bool DefaultEnabled => true;

    public IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log)
    {
        var items = new List<TrackedItem>();

        try
        {
            var sheet = dataManager.GetExcelSheet<Quest>();
            if (sheet == null)
                return items;

            var counts = new Dictionary<uint, int>();
            var samples = new Dictionary<uint, List<string>>();

            foreach (var quest in sheet)
            {
                try
                {
                    var name = quest.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    if (QuestCategorization.IsMainScenarioQuest(quest))
                        continue;

                    var iconId = quest.EventIconType.RowId;

                    counts[iconId] = counts.GetValueOrDefault(iconId) + 1;

                    if (!samples.TryGetValue(iconId, out var sampleList))
                    {
                        sampleList = new List<string>();
                        samples[iconId] = sampleList;
                    }

                    if (sampleList.Count < 6)
                        sampleList.Add(name);
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[{Category}] Skipped a Quest row due to an error", CategoryId);
                }
            }

            foreach (var iconId in counts.Keys.OrderByDescending(id => counts[id]))
            {
                var label = $"Icon {iconId}: {counts[iconId]} quests - e.g. {string.Join(" | ", samples[iconId])}";
                items.Add(new TrackedItem(iconId, label, CategoryId, false, string.Empty, 0));
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "[{Category}] Failed to build tracked items", CategoryId);
        }

        return items;
    }
}
