using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using NativeUIState = FFXIVClientStructs.FFXIV.Client.Game.UI.UIState;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// Owns the list of available <see cref="IContentTracker"/> instances. Add new categories here
/// (Deep Dungeon floors, Chocobo Racing, Triple Triad, Hunt marks, relic steps, ...) without touching
/// the sync/snapshot logic - anything implementing IContentTracker just needs to be added to this list.
/// </summary>
public sealed class ContentTrackerRegistry
{
    public IReadOnlyList<IContentTracker> All { get; }

    public ContentTrackerRegistry()
    {
        All = new List<IContentTracker>
        {
            new DutyContentTracker(
                "Dungeons",
                "Dungeons",
                defaultEnabled: true,
                matchesCategory: cfc => cfc.IsInDutyFinder && IsContentType(cfc, "Dungeons")),

            new DutyContentTracker(
                "Trials",
                "Trials",
                defaultEnabled: true,
                matchesCategory: cfc => cfc.IsInDutyFinder && IsContentType(cfc, "Trials")),

            // "Raids" as a ContentType covers both 8-player raids and 24-player alliance raids - the
            // only thing that tells them apart is party size (ContentMemberType.PartyCount > 1 means
            // multiple 8-player parties, i.e. an alliance).
            new DutyContentTracker(
                "Raids",
                "Raids",
                defaultEnabled: true,
                matchesCategory: cfc => cfc.IsInDutyFinder && IsContentType(cfc, "Raids") && !IsAllianceSized(cfc)),

            // Alliance raids and the newer "Chaotic" alliance raid aren't queued through the classic
            // Duty Finder (IsInDutyFinder is false for both), so unlike Dungeons/Trials/normal Raids this
            // can't gate on IsInDutyFinder at all.
            new DutyContentTracker(
                "AllianceRaids",
                "Alliance Raids",
                defaultEnabled: true,
                matchesCategory: cfc => (IsContentType(cfc, "Raids") && IsAllianceSized(cfc))
                                         || IsContentType(cfc, "Chaotic Alliance Raid")),

            // Variant & Criterion dungeons are queued through their own separate finder UI, not the
            // classic Duty Finder, so IsInDutyFinder is false for all of them too.
            new DutyContentTracker(
                "VariantCriterion",
                "Variant & Criterion Dungeons",
                defaultEnabled: true,
                matchesCategory: cfc => IsContentType(cfc, "V&C Dungeon Finder")),

            new MainScenarioQuestTracker(),

            new UnlockQuestTracker(),

            new SideQuestTracker(),

            new AchievementTracker(),

            // Mount sheet has no expansion/patch field (checked MountTransient too - description text
            // only), so Expansion is left empty. "Company Chocobo" is unlocked by essentially every
            // character via the early "My Little Chocobo" Grand Company quest, used as a spot check.
            new CollectibleTracker<Mount>(
                "Mounts",
                "Mounts",
                defaultEnabled: true,
                getName: m => m.Singular.ExtractText(),
                isUnlocked: (u, m) => u.IsMountUnlocked(m),
                spotCheckNames: ["Company Chocobo"]),

            // Companion (Minion) sheet has no expansion/patch field (checked CompanionTransient too).
            new CollectibleTracker<Companion>(
                "Minions",
                "Minions",
                defaultEnabled: true,
                getName: m => m.Singular.ExtractText(),
                isUnlocked: (u, m) => u.IsCompanionUnlocked(m)),

            // Title sheet has no expansion/patch field. Requires the title list to have been received
            // this session (IsTitleListLoaded, backed by UIState.TitleList.DataReceived) - unlike the
            // Content Compendium bug fixed earlier, FFXIVClientStructs exposes a direct
            // TitleList.RequestTitleList() call rather than needing an agent Show/Hide trick, so trigger
            // that once when not yet requested and let the next sync pick up the result.
            new CollectibleTracker<Title>(
                "Titles",
                "Titles",
                defaultEnabled: true,
                getName: t => t.Masculine.ExtractText(),
                isUnlocked: (u, t) => u.IsTitleUnlocked(t),
                isReady: u => u.IsTitleListLoaded,
                onNotReady: RequestTitleListLoad),

            // Emote sheet has a "Patch" field, but it's only populated from patch 5.50 onward - every
            // emote added before Shadowbringers' back half reports Patch=1 or 0 with no way to tell ARR
            // from Heavensward from Stormblood apart, so it isn't a clean enough signal to derive
            // Expansion from (confirmed against known ARR emotes like Wave/Doze/Rally, all Patch=1).
            // Left empty rather than half-populated. "Wave" and "Doze" are taught for free in every
            // starting city's earliest quests, used as spot checks.
            new CollectibleTracker<Emote>(
                "Emotes",
                "Emotes",
                defaultEnabled: true,
                getName: e => e.Name.ExtractText(),
                isUnlocked: (u, e) => u.IsEmoteUnlocked(e),
                spotCheckNames: ["Wave", "Doze"]),

            // Orchestrion sheet has no expansion/patch field (Name/Description only) and no rolls are
            // unlocked by default, so there's no safe universal spot check here.
            new CollectibleTracker<Orchestrion>(
                "OrchestrionRolls",
                "Orchestrion Rolls",
                defaultEnabled: true,
                getName: o => o.Name.ExtractText(),
                isUnlocked: (u, o) => u.IsOrchestrionUnlocked(o)),

            // Ornament = Facewear/Fashion Accessories in Dalamud's own IUnlockState doc comment. No
            // expansion/patch field (checked OrnamentTransient too - single description string).
            new CollectibleTracker<Ornament>(
                "Facewear",
                "Facewear",
                defaultEnabled: true,
                getName: o => o.Singular.ExtractText(),
                isUnlocked: (u, o) => u.IsOrnamentUnlocked(o)),

            // TripleTriadCard sheet has no expansion/patch field (checked TripleTriadCardResident too -
            // acquisition source and rarity/type, not a version).
            new CollectibleTracker<TripleTriadCard>(
                "TripleTriadCards",
                "Triple Triad Cards",
                defaultEnabled: true,
                getName: c => c.Name.ExtractText(),
                isUnlocked: (u, c) => u.IsTripleTriadCardUnlocked(c)),

            // "Bardings" maps to the BuddyEquip sheet - Dalamud's IsBuddyEquipUnlocked doc comment calls
            // it "Equipment of the players Chocobo Companion", and its row names ("Lominsan Barding",
            // "Leather Saddle", ...) confirm it's the chocobo saddle/barding set, not a separate
            // barding-only sheet. No expansion/patch field. Uses the capitalized .Name field rather than
            // .Singular, which holds lowercase grammatical text ("set of Lominsan barding").
            new CollectibleTracker<BuddyEquip>(
                "Bardings",
                "Bardings",
                defaultEnabled: true,
                getName: b => b.Name.ExtractText(),
                isUnlocked: (u, b) => u.IsBuddyEquipUnlocked(b)),
        };
    }

    public bool IsEnabled(Configuration config, IContentTracker tracker)
        => config.EnabledCategories.TryGetValue(tracker.CategoryId, out var enabled) ? enabled : tracker.DefaultEnabled;

    private static bool IsContentType(ContentFinderCondition cfc, string name)
        => cfc.ContentType.IsValid && cfc.ContentType.Value.Name.ExtractText().Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool IsAllianceSized(ContentFinderCondition cfc)
        => cfc.ContentMemberType.IsValid && cfc.ContentMemberType.Value.PartyCount > 1;

    private static unsafe void RequestTitleListLoad(IPluginLog log)
    {
        var uiState = NativeUIState.Instance();
        if (uiState == null)
            return;

        if (!uiState->TitleList.DataRequested)
            uiState->TitleList.RequestTitleList();
    }
}
