using System;
using TecmoSBGame.Persistence;

namespace TecmoSBGame.SimArch.Flow;

/// <summary>
/// Simple high-level game/screen flow controller.
///
/// Ported from: ArchiveMge/Flow/GameFlowController.cs
/// </summary>
public sealed class GameFlowController
{
    private readonly Random _rng;

    public GameFlowController(int seed = 0xC0FFEE)
    {
        _rng = new Random(seed);
    }

    public GameFlowState State { get; private set; } = GameFlowState.Title;

    public SimArch.Components.MenuItemType SelectedMainMenuItem { get; private set; } = SimArch.Components.MenuItemType.Preseason;

    public int TeamCount { get; set; } = 2;
    public int AwayTeamIndex { get; private set; } = 0;
    public int HomeTeamIndex { get; private set; } = 1;

    public int ActiveTeamSelectColumn { get; private set; } = 0;

    public bool CoinTossResolved { get; private set; }

    public bool WinnerChoosesReceive { get; private set; } = true;

    public int TossWinnerTeamIndex { get; private set; } = -1;

    public int ReceivingTeamIndex { get; private set; } = 0;
    public int KickingTeamIndex { get; private set; } = 1;

    public int WindDirection { get; private set; } = +1;

    public int LastEndedPlayId { get; private set; } = -1;
    public SeasonMetaPage ActiveSeasonMetaPage { get; private set; } = SeasonMetaPage.Hub;
    public SeasonState? ActiveSeason { get; private set; }

    public event Action<GameFlowState, GameFlowState>? StateChanged;

    public void Reset()
    {
        SelectedMainMenuItem = SimArch.Components.MenuItemType.Preseason;

        AwayTeamIndex = 0;
        HomeTeamIndex = Math.Min(1, TeamCount - 1);
        ActiveTeamSelectColumn = 0;

        CoinTossResolved = false;
        WinnerChoosesReceive = true;
        TossWinnerTeamIndex = -1;

        ReceivingTeamIndex = 0;
        KickingTeamIndex = 1;

        WindDirection = +1;
        LastEndedPlayId = -1;
        ActiveSeasonMetaPage = SeasonMetaPage.Hub;
        ActiveSeason = null;

        Transition(GameFlowState.Title);
    }

    public void SelectMainMenuItem(SimArch.Components.MenuItemType item)
    {
        if (State != GameFlowState.MainMenu)
            return;

        SelectedMainMenuItem = item;

        if (item == SimArch.Components.MenuItemType.Preseason)
            Transition(GameFlowState.TeamSelect);
        else if (item == SimArch.Components.MenuItemType.Season)
            Transition(GameFlowState.SeasonMeta);
    }

    public void UpdateUiInput(bool startPressed, bool leftPressed, bool rightPressed, bool upPressed, bool downPressed)
    {
        switch (State)
        {
            case GameFlowState.Title:
                if (startPressed)
                    Transition(GameFlowState.MainMenu);
                break;

            case GameFlowState.MainMenu:
                break;

            case GameFlowState.TeamSelect:
                UpdateTeamSelect(leftPressed, rightPressed, upPressed, downPressed);
                if (startPressed)
                    Transition(GameFlowState.CoinToss);
                break;

            case GameFlowState.SeasonMeta:
                UpdateSeasonMeta(leftPressed, rightPressed);
                if (startPressed)
                    CycleSeasonMetaPage();
                if (downPressed)
                    Transition(GameFlowState.MainMenu);
                break;

            case GameFlowState.CoinToss:
                UpdateCoinToss(leftPressed, rightPressed);
                if (startPressed)
                {
                    if (!CoinTossResolved) ResolveCoinToss();
                    else Transition(GameFlowState.Kickoff);
                }
                break;

            case GameFlowState.Kickoff:
                if (startPressed) Transition(GameFlowState.OnField);
                break;

            case GameFlowState.PostPlay:
                if (startPressed) Transition(GameFlowState.OnField);
                break;

            case GameFlowState.OnField:
            default:
                break;
        }
    }

    private void UpdateTeamSelect(bool leftPressed, bool rightPressed, bool upPressed, bool downPressed)
    {
        if (leftPressed) ActiveTeamSelectColumn = 0;
        if (rightPressed) ActiveTeamSelectColumn = 1;

        int delta = 0;
        if (upPressed) delta--;
        if (downPressed) delta++;

        if (delta != 0)
        {
            if (ActiveTeamSelectColumn == 0)
                AwayTeamIndex = Wrap(AwayTeamIndex + delta, TeamCount);
            else
                HomeTeamIndex = Wrap(HomeTeamIndex + delta, TeamCount);
        }

        if (TeamCount > 1 && AwayTeamIndex == HomeTeamIndex)
        {
            if (ActiveTeamSelectColumn == 0)
                HomeTeamIndex = Wrap(HomeTeamIndex + 1, TeamCount);
            else
                AwayTeamIndex = Wrap(AwayTeamIndex + 1, TeamCount);
        }
    }

    private void UpdateSeasonMeta(bool leftPressed, bool rightPressed)
    {
        if (leftPressed)
            ActiveSeasonMetaPage = (SeasonMetaPage)(((int)ActiveSeasonMetaPage + 5) % 6);
        if (rightPressed)
            ActiveSeasonMetaPage = (SeasonMetaPage)(((int)ActiveSeasonMetaPage + 1) % 6);
    }

    private void CycleSeasonMetaPage()
    {
        ActiveSeasonMetaPage = (SeasonMetaPage)(((int)ActiveSeasonMetaPage + 1) % 6);
    }

    private void UpdateCoinToss(bool leftPressed, bool rightPressed)
    {
        if (leftPressed || rightPressed)
        {
            WinnerChoosesReceive = !WinnerChoosesReceive;
            if (CoinTossResolved)
                RecomputeKickoffTeamsFromChoice();
        }
    }

    public void ConfirmTeamSelection(SimArch.State.MatchState match)
    {
        if (match is null) throw new ArgumentNullException(nameof(match));

        match.AwayTeamId = AwayTeamIndex;
        match.HomeTeamId = HomeTeamIndex;

        CoinTossResolved = false;
        TossWinnerTeamIndex = -1;
        WinnerChoosesReceive = true;

        WindDirection = _rng.Next(0, 2) == 0 ? -1 : +1;
    }

    public void ApplyCoinTossToMatch(SimArch.State.MatchState match)
    {
        if (match is null) throw new ArgumentNullException(nameof(match));
        match.ResetForKickoff(kickingTeam: KickingTeamIndex, receivingTeam: ReceivingTeamIndex);
    }

    public void NotifyPlayEnded(int playId)
    {
        LastEndedPlayId = playId;
        if (State == GameFlowState.OnField)
            Transition(GameFlowState.PostPlay);
    }

    public void NotifyNextPlayReady()
    {
        if (State == GameFlowState.PostPlay)
            Transition(GameFlowState.OnField);
    }

    private void ResolveCoinToss()
    {
        bool flipHeads = _rng.Next(0, 2) == 0;
        bool callHeads = WinnerChoosesReceive;

        TossWinnerTeamIndex = (callHeads == flipHeads) ? 0 : 1;

        WinnerChoosesReceive = true;
        CoinTossResolved = true;

        RecomputeKickoffTeamsFromChoice();
    }

    public void RecomputeKickoffTeamsFromChoice()
    {
        if (!CoinTossResolved || TossWinnerTeamIndex < 0)
            return;

        ReceivingTeamIndex = WinnerChoosesReceive
            ? TossWinnerTeamIndex
            : (TossWinnerTeamIndex == 0 ? 1 : 0);

        KickingTeamIndex = ReceivingTeamIndex == 0 ? 1 : 0;
    }

    public void SetActiveSeason(SeasonState? season)
    {
        ActiveSeason = season;
    }

    private void Transition(GameFlowState next)
    {
        if (State == next)
            return;

        var prev = State;
        State = next;
        StateChanged?.Invoke(prev, next);
    }

    private static int Wrap(int value, int count)
    {
        if (count <= 0) return 0;
        var v = value % count;
        if (v < 0) v += count;
        return v;
    }
}
