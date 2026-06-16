using System;
using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Plugin.Services;

namespace GambaWhere.Partyfinder;

/// <summary>
/// Locates a host's live Party Finder listing and opens its detail view so the user can join.
/// </summary>
public sealed class PartyFinderLocator : IDisposable
{
    private const byte DataCentreArea = 0;
    private const byte PrivateArea = 2;
    private const byte AllCategory = 0;
    private const byte OtherCategory = 16;

    private static readonly (byte Area, byte Category)[] SearchPlan =
    {
        (DataCentreArea, AllCategory),
        (DataCentreArea, OtherCategory),
        (PrivateArea, AllCategory),
        (PrivateArea, OtherCategory),
    };

    private static readonly TimeSpan StageBudget = TimeSpan.FromSeconds(8);

    private static readonly TimeSpan CollectWindow = TimeSpan.FromSeconds(1.5);

    private enum Stage
    {
        Idle,
        Reopening,
        Collecting,
    }

    private readonly IPartyFinderGui _partyFinderGui;
    private readonly IFramework _framework;
    private readonly IChatGui _chat;
    private readonly IPluginLog _log;
    private readonly LookingForGroupInterop _interop;

    private Stage _stage = Stage.Idle;
    private string _targetName = string.Empty;
    private bool _matchFound;
    private ulong _matchListingId;
    private int _phaseIndex;
    private DateTime _deadlineUtc;
    private DateTime _collectUntilUtc;

    private string _statusMessage = string.Empty;
    private DateTime _statusUntil;

    public PartyFinderLocator(
        IPartyFinderGui partyFinderGui,
        IFramework framework,
        IChatGui chat,
        IPluginLog log,
        LookingForGroupInterop interop)
    {
        _partyFinderGui = partyFinderGui;
        _framework = framework;
        _chat = chat;
        _log = log;
        _interop = interop;

        _framework.Update += OnFrameworkUpdate;
        _partyFinderGui.ReceiveListing += OnReceiveListing;
    }

    public bool IsRunning => _stage != Stage.Idle;

    public string StatusMessage => DateTime.UtcNow < _statusUntil ? _statusMessage : string.Empty;

    public void Find(string characterName)
    {
        if (IsRunning)
        {
            SetStatus("Already searching the Party Finder.");
            return;
        }

        if (string.IsNullOrWhiteSpace(characterName))
            return;

        if (!_interop.IsAgentAvailable)
        {
            SetStatus("Could not access the Party Finder. Try again in a moment.");
            return;
        }

        _targetName = characterName.Trim();
        _matchFound = false;
        _matchListingId = 0;
        _phaseIndex = 0;

        if (_interop.IsWindowOpen)
        {
            _interop.CloseWindow();
            SetStage(Stage.Reopening);
            return;
        }

        _interop.OpenWindow();
        BeginPhase(0);
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
        _partyFinderGui.ReceiveListing -= OnReceiveListing;
    }

    private void OnReceiveListing(IPartyFinderListing listing, IPartyFinderListingEventArgs args)
    {
        if (_stage != Stage.Collecting)
            return;

        if (_matchFound || !Matches(listing))
            return;

        _matchFound = true;
        _matchListingId = listing.Id;
    }

    private bool Matches(IPartyFinderListing listing)
    {
        var world = listing.HomeWorld.ValueNullable?.Name.ToString() ?? string.Empty;
        var full = $"{listing.Name.TextValue} {world}".Trim();
        return string.Equals(full, _targetName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_stage == Stage.Idle)
            return;

        if (DateTime.UtcNow > _deadlineUtc)
        {
            Abort();
            return;
        }

        switch (_stage)
        {
            case Stage.Reopening:
                StepReopen();
                break;
            case Stage.Collecting:
                StepCollect();
                break;
        }
    }

    private void StepReopen()
    {
        if (_interop.IsWindowOpen)
            return;

        _interop.OpenWindow();
        BeginPhase(0);
    }

    private void BeginPhase(int index)
    {
        _phaseIndex = index;
        var (area, category) = SearchPlan[index];
        _interop.BrowseListings(area, category);
        _collectUntilUtc = DateTime.UtcNow + CollectWindow;
        SetStage(Stage.Collecting);
    }

    private void StepCollect()
    {
        if (_matchFound)
        {
            OpenMatch();
            return;
        }

        if (DateTime.UtcNow < _collectUntilUtc)
            return;

        var next = _phaseIndex + 1;
        if (next < SearchPlan.Length)
        {
            BeginPhase(next);
            return;
        }

        NotFound();
    }

    private void OpenMatch()
    {
        _interop.OpenListing(_matchListingId);
        SetStatus($"Found {_targetName}'s Party Finder. Opening details.");
        _stage = Stage.Idle;
    }

    private void NotFound()
    {
        _interop.CloseWindow();
        _chat.Print(
            $"Could not find an active Party Finder for {_targetName}. They may not have one open right now.",
            "GambaWhere");
        SetStatus("No matching Party Finder found.");
        _stage = Stage.Idle;
    }

    private void Abort()
    {
        _log.Warning("[PF] Locator timed out at stage {Stage}.", _stage);
        NotFound();
    }

    private void SetStage(Stage stage)
    {
        _stage = stage;
        _deadlineUtc = DateTime.UtcNow + StageBudget;
    }

    private void SetStatus(string message)
    {
        _statusMessage = message;
        _statusUntil = DateTime.UtcNow.AddSeconds(6);
    }
}
