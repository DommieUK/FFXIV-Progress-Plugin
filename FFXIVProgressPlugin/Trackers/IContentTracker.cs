using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace FFXIVProgressPlugin.Trackers;

/// <summary>
/// A pluggable source of completion data for one category of content (e.g. Dungeons, Achievements).
/// New categories (Deep Dungeon floors, Chocobo Racing, Triple Triad, Hunt marks, relic steps, ...)
/// can be added by implementing this interface and registering an instance in <see cref="ContentTrackerRegistry"/>,
/// without touching the sync/snapshot logic.
/// </summary>
public interface IContentTracker
{
    /// <summary>Stable machine-readable key, used in config and in the JSON payload.</summary>
    string CategoryId { get; }

    /// <summary>Human-readable name shown in the settings UI.</summary>
    string DisplayName { get; }

    /// <summary>Whether this category is enabled by default for new installs.</summary>
    bool DefaultEnabled { get; }

    /// <summary>
    /// Builds the current list of tracked items for this category by reading Lumina sheet data and
    /// live game state. Must only be called on the game's Framework thread, since implementations
    /// may read live game memory (FFXIVClientStructs) that is unsafe to touch from other threads.
    /// Implementations must not throw - all game-memory access must be wrapped in try/catch.
    /// </summary>
    IReadOnlyList<TrackedItem> BuildItems(IDataManager dataManager, IUnlockState unlockState, IPluginLog log);
}
