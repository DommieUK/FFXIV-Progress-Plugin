using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using FFXIVProgressPlugin.Trackers;

namespace FFXIVProgressPlugin.Sync;

/// <summary>
/// Periodically builds a completion snapshot and POSTs it to the configured Worker URL.
/// Runs entirely off the game thread: the timer loop and HTTP call live on background Tasks, and only
/// the brief snapshot-building step is marshalled onto the Framework thread (via IFramework.RunOnFrameworkThread)
/// since that step touches live game memory. Failures are retried with exponential backoff and logged to
/// Dalamud's plugin log only - this never writes to the in-game chat window.
/// </summary>
public sealed class SyncService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Configuration config;
    private readonly ContentTrackerRegistry registry;
    private readonly IDataManager dataManager;
    private readonly IUnlockState unlockState;
    private readonly IPlayerState playerState;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly HttpClient httpClient;
    private readonly CancellationTokenSource cts = new();
    private readonly SemaphoreSlim syncGate = new(1, 1);

    private Task? loopTask;

    public DateTime? LastSuccessUtc { get; private set; }

    public string? LastError { get; private set; }

    public bool IsSyncing { get; private set; }

    public SyncService(
        Configuration config,
        ContentTrackerRegistry registry,
        IDataManager dataManager,
        IUnlockState unlockState,
        IPlayerState playerState,
        IFramework framework,
        IPluginLog log)
    {
        this.config = config;
        this.registry = registry;
        this.dataManager = dataManager;
        this.unlockState = unlockState;
        this.playerState = playerState;
        this.framework = framework;
        this.log = log;

        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public void Start()
    {
        loopTask = Task.Run(() => RunLoopAsync(cts.Token));
    }

    /// <summary>Fires an immediate sync outside the normal interval, e.g. for a "Sync now" button.</summary>
    public void TriggerSyncNow()
    {
        _ = Task.Run(() => SyncOnceWithRetryAsync(cts.Token));
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        await SyncOnceWithRetryAsync(token).ConfigureAwait(false);

        while (!token.IsCancellationRequested)
        {
            var interval = TimeSpan.FromSeconds(Math.Max(10, config.SyncIntervalSeconds));
            try
            {
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SyncOnceWithRetryAsync(token).ConfigureAwait(false);
        }
    }

    private async Task SyncOnceWithRetryAsync(CancellationToken token)
    {
        if (!await syncGate.WaitAsync(0, token).ConfigureAwait(false))
            return; // a sync is already running (e.g. "Sync now" while the interval fired)

        try
        {
            IsSyncing = true;

            const int maxAttempts = 4;
            var delay = TimeSpan.FromSeconds(2);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await SyncOnceAsync(token).ConfigureAwait(false);
                    LastError = null;
                    LastSuccessUtc = DateTime.UtcNow;
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    log.Warning(ex, "Sync attempt {Attempt}/{Max} failed", attempt, maxAttempts);

                    if (attempt == maxAttempts)
                        break;

                    await Task.Delay(delay, token).ConfigureAwait(false);
                    delay *= 2;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        finally
        {
            IsSyncing = false;
            syncGate.Release();
        }
    }

    private async Task SyncOnceAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(config.WorkerUrl))
            throw new InvalidOperationException("Worker URL isn't configured.");

        if (!Uri.TryCreate(config.WorkerUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Worker URL must be a valid HTTPS URL.");

        var snapshot = await framework
            .RunOnFrameworkThread(() => SnapshotBuilder.Build(config, registry, dataManager, unlockState, playerState, log))
            .ConfigureAwait(false);

        var json = JsonSerializer.Serialize(snapshot, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrEmpty(config.SecretToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.SecretToken);

        using var response = await httpClient.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        cts.Cancel();

        try
        {
            loopTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // expected on cancellation
        }

        httpClient.Dispose();
        cts.Dispose();
        syncGate.Dispose();
    }
}
