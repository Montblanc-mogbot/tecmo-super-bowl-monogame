using System;
using TecmoSBGame.Components.Menu;
using TecmoSBGame.State;

namespace TecmoSBGame.Flow;

/// <summary>
/// Simple high-level game/screen flow controller.
///
/// - Non-ECS: usable by MonoGame runtime and headless runner.
/// - Deterministic: all random decisions come from the provided seed.
/// </summary>
public sealed class GameFlowController
{
    private readonly Random _rng;

    public GameFlowController(int seed = 0xC0FFEE)
    {
        _rng = new Random(seed);
    }

    public GameFlowState State { get; private set; } = GameFlowState.Title;

    // Main menu choice.
    public MenuItemType SelectedMainMenuItem { get; private set; } = MenuItemType.Preseason;

    // Team selection inputs/outputs.
    public int TeamCount { get; set; } = 2;
    public int AwayTeamIndex { get; private set; } = 0;
    public int HomeTeamIndex { get; private set; } = 1;

    /// <summary>0 = Away column, 1 = Home column.</summary>
    public int ActiveTeamSelectColumn { get; private set; } = 0;

    // Coin toss state.
    public bool CoinTossResolved { get; private set; }

    /// <summary>
    /// Before resolve: represents the call (true=heads, false=tails).
    /// After resolve: represents the winner's choice (true=receive, false=kick).
    /// </summary>
    public bool WinnerChoosesReceive { get; private set; } = true;

    /// <summary>
    /// Slot index (0=away, 1=home). -1 while unresolved.
    /// </summary>
    public int TossWinnerTeamIndex { get; private set; } = -1;

    public int ReceivingTeamIndex { get; private set; } = 0;
    public int KickingTeamIndex { get; private set; } = 1;

    /// <summary>
    /// Visual-only wind direction. -1 = left, +1 = right.
    /// </summary>
    public int WindDirection { get; private set; } = +1;

    // Post-play bookkeeping.
    public int LastEndedPlayId { get; private set; } = -1;

    public event Action<GameFlowState, GameFlowState>? StateChanged;

    public void Reset()
    {
        SelectedMainMenuItem = MenuItemType.Preseason;

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

        Transition(GameFlowState.Title);
    }

    public void SelectMainMenuItem(MenuItemType item)
    {
        if (State != GameFlowState.MainMenu)
            return;

        SelectedMainMenuItem = item;

        // For now, only Preseason is wired to gameplay.
        if (item == MenuItemType.Preseason)
            Transition(GameFlowState.TeamSelect);
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
                // Selection handled by MenuNavigationSystem + SelectMainMenuItem(...)
                break;

            case GameFlowState.TeamSelect:
                UpdateTeamSelect(leftPressed, rightPressed, upPressed, downPressed);
                if (startPressed)
                    Transition(GameFlowState.CoinToss);
                break;

            case GameFlowState.CoinToss:
                UpdateCoinToss(leftPressed, rightPressed);
                if (startPressed)
                {
                    if (!CoinTossResolved)
                    {
                        ResolveCoinToss();
                    }
                    else
                    {
                        Transition(GameFlowState.Kickoff);
                    }
                }
                break;

            case GameFlowState.Kickoff:
                if (startPressed)
                    Transition(GameFlowState.OnField);
                break;

            case GameFlowState.PostPlay:
                if (startPressed)
                    Transition(GameFlowState.OnField);
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

        // Prevent same team on both sides (soft rule: if they match, bump the non-active side).
        if (TeamCount > 1 && AwayTeamIndex == HomeTeamIndex)
        {
            if (ActiveTeamSelectColumn == 0)
                HomeTeamIndex = Wrap(HomeTeamIndex + 1, TeamCount);
            else
                AwayTeamIndex = Wrap(AwayTeamIndex + 1, TeamCount);
        }
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

    public void ConfirmTeamSelection(MatchState match)
    {
        if (match is null) throw new ArgumentNullException(nameof(match));

        // Store which actual team ids are mapped into slot0/slot1.
        match.AwayTeamId = AwayTeamIndex;
        match.HomeTeamId = HomeTeamIndex;

        // Reset coin toss selection state.
        CoinTossResolved = false;
        TossWinnerTeamIndex = -1;
        WinnerChoosesReceive = true; // default call: heads

        // Deterministic, visual-only wind direction each matchup.
        WindDirection = _rng.Next(0, 2) == 0 ? -1 : +1;
    }

    public void ApplyCoinTossToMatch(MatchState match)
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
        // Flip coin.
        bool flipHeads = _rng.Next(0, 2) == 0;
        bool callHeads = WinnerChoosesReceive;

        // Away makes the call; if correct, away wins.
        TossWinnerTeamIndex = (callHeads == flipHeads) ? 0 : 1;

        // Now WinnerChoosesReceive becomes the actual choice toggle.
        WinnerChoosesReceive = true;
        CoinTossResolved = true;

        RecomputeKickoffTeamsFromChoice();
    }

    /// <summary>
    /// Call after the coin-toss winner updates receive/kick.
    /// </summary>
    public void RecomputeKickoffTeamsFromChoice()
    {
        if (!CoinTossResolved || TossWinnerTeamIndex < 0)
            return;

        ReceivingTeamIndex = WinnerChoosesReceive
            ? TossWinnerTeamIndex
            : (TossWinnerTeamIndex == 0 ? 1 : 0);

        KickingTeamIndex = ReceivingTeamIndex == 0 ? 1 : 0;
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
        value %= count;
        if (value < 0) value += count;
        return value;
    }
}
