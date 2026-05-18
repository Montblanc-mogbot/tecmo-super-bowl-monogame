using System;
using Arch.Core;
using Arch.Core.Extensions;
using TecmoSB;
using TecmoSBGame.SimArch.Components.PlayCall;
using TecmoSBGame.SimArch.Events;

namespace TecmoSBGame.SimArch;

/// <summary>
/// Arch-based simulation entrypoint.
///
/// Owns the Arch World and provides a small API surface for game/UI code.
///
/// NOTE: Keep simulation deterministic: update via fixed timestep.
/// UI should call ApplyPlaySelection which queues intent to be applied on the next Update tick.
/// </summary>
public sealed class Sim : IDisposable
{
    public World World { get; private set; }

    public SimSnapshot Snapshot { get; } = new();

    private PendingPlaySelection? _pendingSelection;


    private readonly Systems.MovementSystem _movement = new();
    private readonly Systems.SpeedModifierSystem _speedModifiers = new();
    private readonly Systems.PlayerControlSystem _playerControl = new();
    private readonly Systems.CollisionContactSystem _contacts = new();
    private readonly Systems.FumbleDebugSystem _fumbleDebug = new();
    private readonly Systems.FumbleResolutionSystem _fumbleResolution = new();
    private readonly Systems.LooseBallPickupSystem _loosePickup = new();
    private readonly Systems.EngagementSystem _engagement = new();
    private readonly Systems.TackleResolutionSystem _tackleResolution = new();
    private readonly Systems.BehaviorStackSystem _behaviorStack = new();
    private readonly PlayScripts.PlayScriptRegistry _scriptRegistry = new();
    private readonly Routes.RouteRegistry _routeRegistry = new();
    private readonly Systems.PlayScriptSystem _playScripts = new();
    private readonly Systems.RouteFollowSystem _routes = new();
    private readonly Systems.BlockerAiSystem _blockerAi = new();
    private readonly Systems.DefensiveRushSystem _rush = new();
    private readonly Systems.CoverageSystem _coverage = new();
    private readonly Systems.QbAiSystem _qbAi = new();

    // Match/play rules
    private readonly SimArch.State.MatchState _match = new();
    private readonly SimArch.State.PlayState _play = new();

    public SimArch.State.MatchState MatchState => _match;
    public SimArch.State.PlayState PlayState => _play;
    private readonly Systems.GameClockSystem _clock = new();
    private readonly Systems.DownDistanceSystem _downDistance = new();
    private readonly Systems.PlayResultResolver _playResult;
    private readonly Systems.KickoffAfterScoreSystem _kickoffAfterScore;
    private readonly Systems.KickoffPlaySystem _kickoffPlay = new();
    private readonly Systems.PuntPlaySystem _puntPlay = new();
    private readonly Systems.FieldGoalPlaySystem _fieldGoalPlay = new();
    private readonly Systems.PlayLifecycleSystem _lifecycle;
    private readonly Systems.SnapAndContinueInputSystem _snapAndContinue;
    private readonly Systems.PlayCall.PlayCallSystem _playCall;
    private readonly Systems.PlayCall.PlayCallPublishSelectionSystem _playCallPublish = new();
    private readonly Systems.PreSnapSystems _preSnap = new();
    private readonly Systems.FormationScriptSystem _formationScripts;
    private readonly Systems.BallSystem _ball = new();
    private readonly Systems.PassFlightCompleteSystem _passComplete = new();
    private readonly Systems.KickoffFlightCompleteSystem _kickoffComplete = new();
    private readonly Systems.PlayEndSystem _playEnd = new();
    private readonly Systems.NextPlayResetSystem _nextPlayReset = new();
    private readonly Systems.TackleAndPlayEndSystems _tackleAndEnd = new(); // legacy fallback; no longer detects tackles
    private int _lastAppliedPlayEndPlayId;

    private readonly System.Collections.Generic.List<int> _offense = new(11);
    private readonly System.Collections.Generic.List<int> _defense = new(11);
    private int _ballEntityId = -1;

    private Components.Control _control;
    private Components.Input _input;
    private Components.UiButtons _ui;
    private bool _paused;
    private string _lastPlaySummary = string.Empty;

    private TecmoSB.FormationDataConfig? _formationData;
    private TecmoSB.DefensiveFormationDataConfig? _defensiveFormationData;
    private TecmoSB.PlayDataConfig? _playData;

    private TecmoSB.PlayListConfig? _playList;
    private TecmoSB.DefensePlayConfig? _defensePlays;

    public Sim(
        TecmoSB.FormationDataConfig? formationData = null,
        TecmoSB.DefensiveFormationDataConfig? defensiveFormationData = null,
        TecmoSB.PlayListConfig? playList = null,
        TecmoSB.PlayDataConfig? playData = null,
        TecmoSB.DefensePlayConfig? defensePlays = null)
    {
        _formationData = formationData;
        _defensiveFormationData = defensiveFormationData;
        _playList = playList;
        _playData = playData;
        _defensePlays = defensePlays;

        _playResult = new Systems.PlayResultResolver(_match, _play);
        _kickoffAfterScore = new Systems.KickoffAfterScoreSystem(_match, _play);
        _lifecycle = new Systems.PlayLifecycleSystem(_match, _play, _kickoffAfterScore);
        _snapAndContinue = new Systems.SnapAndContinueInputSystem(_match, _play);

        // If these aren't provided, we'll lazy-load in BootstrapWorld.
        _playCall = new Systems.PlayCall.PlayCallSystem(
            formations: _formationData ?? TecmoSB.FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml")),
            playList: _playList ?? TecmoSB.PlayListYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playcall", "playlist.yaml")),
            defensePlays: _defensePlays ?? TecmoSB.DefensePlayYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "defenseplays", "bank4_defense_special_pointers.yaml")));
        _formationScripts = new Systems.FormationScriptSystem(_play);

        World = World.Create();
        BootstrapWorld();
    }

    public void Dispose()
    {
        World.Dispose();
    }

    public void Reset()
    {
        // TODO: if Arch exposes a clear/reset pattern, prefer that.
        // For now: recreate world (simple and deterministic for early refactor stage).
        World.Dispose();
        World = World.Create();
        Snapshot.Tick = 0;
        _pendingSelection = null;

        _scriptRegistry.Clear();
        _routeRegistry.Clear();

        _offense.Clear();
        _defense.Clear();
        _ballEntityId = -1;

        _match.Quarter = 1;
        _match.GameClockSeconds = 5 * 60;
        _match.PossessionTeam = 0;
        _match.OffenseDirection = SimArch.State.OffenseDirection.LeftToRight;
        _match.Down = 1;
        _match.YardsToGo = 10;
        _match.GoalToGo = false;
        _match.BallSpot = SimArch.State.BallSpot.Own(25);
        _match.Team0Score = 0;
        _match.Team1Score = 0;
        _match.PlayNumber = 0;
        _match.DriveId = 0;
        _match.MatchOver = false;
        _match.Phase = SimArch.State.MatchPhase.FirstQuarter;
        _match.ClockRunning = false;
        _match.KickingTeamIndex = 1;
        _match.ReceivingTeamIndex = 0;
        _match.DeferredKickKickingTeam = 1;
        _match.DeferredKickReceivingTeam = 0;
        _match.KickoffPending = false;
        _match.KickoffPlayActive = false;
        _match.KickoffLandingAbsoluteYardOverride = null;
        _match.PuntPending = false;
        _match.PuntPlayActive = false;
        _match.PuntLandingAbsoluteYardOverride = null;
        _match.ForcePuntMuff = false;
        _match.FieldGoalPending = false;
        _match.FieldGoalPlayActive = false;
        _match.ExtraPointPending = false;
        _match.FieldGoalTargetAbsoluteYardOverride = null;
        _match.ForceFieldGoalBlock = false;
        _match.ForceFieldGoalMiss = false;
        _lastAppliedPlayEndPlayId = 0;
        _paused = false;
        _lastPlaySummary = string.Empty;

        _play.ResetForNewPlay(playId: 0, startAbsoluteYard: SimArch.State.PlayState.ToAbsoluteYard(_match.BallSpot, _match.OffenseDirection));

        BootstrapWorld();
    }

    public void ApplyPlaySelection(in PendingPlaySelection sel)
    {
        // Transitional API: publish the selection event.
        // PlayLifecycleSystem owns phase transitions.
        _pendingSelection = sel;

        var e = new PlaySelectedEvent(
            OffensiveFormationId: sel.FormationId,
            OffensivePlayName: sel.OffensivePlayName,
            OffensivePlaySlot: sel.OffensivePlaySlot,
            OffensivePlayNumber: sel.PlayNumber,
            DefensiveCallId: string.Empty);
        SimEventBus.Send(ref e);
    }

    public void SetUiButtons(in Components.UiButtons ui)
    {
        _ui = ui;
    }

    public void SetInput(Microsoft.Xna.Framework.Vector2 direction)
    {
        _input.Direction = direction;
    }

    public bool Paused => _paused;

    public void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused)
            _input.Direction = Microsoft.Xna.Framework.Vector2.Zero;
    }

    public void Update(float dtSeconds)
    {
        if (_paused)
        {
            UpdateSnapshot();
            return;
        }
        var kickoffActive = _match.KickoffPending;
        var puntActive = _match.PuntPending;
        var fieldGoalActive = _match.FieldGoalPending;

        // UI-driven playcall + lifecycle inputs.
        if (!kickoffActive && !puntActive && !fieldGoalActive)
        {
            _playCall.Update(World, _ui);
            _playCallPublish.Update(World, _ui);
        }
        _snapAndContinue.Update(World, _ui);

        // Apply queued selection at the start of a tick.
        if (!kickoffActive && !puntActive && !fieldGoalActive && _pendingSelection is not null)
        {
            var sel = _pendingSelection.Value;
            _pendingSelection = null;

            // Apply play selection.
            if (_playData is null)
                throw new InvalidOperationException("SimArch PlayData not loaded");

            _scriptRegistry.Clear();
            _routeRegistry.Clear();
            Spawning.PlaySpawner.ApplyPlay(World, _playData, _offense, _defense, _ballEntityId, sel.PlayNumber, _scriptRegistry, _routeRegistry);
        }

        // Run systems (minimal set for now).
        if (kickoffActive)
        {
            _kickoffPlay.UpdatePreMovement(World, _match, _play, _ballEntityId, _offense, _defense, ref _control);
        }
        else if (puntActive)
        {
            _puntPlay.UpdatePreMovement(World, _match, _play, _ballEntityId, _offense, _defense, ref _control);
        }
        else if (fieldGoalActive)
        {
            _fieldGoalPlay.UpdatePreMovement(World, _match, _play, _ballEntityId, _offense, _defense, ref _control);
        }
        else
        {
            // Pre-snap placement runs opportunistically when the ball is Dead.
            _preSnap.Update(World, _offense, _defense, _ballEntityId, offenseDirSign: 1f);
            _formationScripts.Update(World, dtSeconds);

            _playScripts.Update(World, dtSeconds, _ballEntityId, _offense, _defense, _scriptRegistry, ref _control);
            _routes.Update(World, dtSeconds, _routeRegistry);
            _blockerAi.Update(World, dtSeconds, _offense, _defense, _ballEntityId);
            _rush.Update(World, dtSeconds, _defense);
            _coverage.Update(World, dtSeconds, _ballEntityId, _defense);
            _qbAi.Update(World, dtSeconds, _ballEntityId);
        }
        _playerControl.Update(World, dtSeconds, _ballEntityId, ref _control);
        _speedModifiers.Update(World, dtSeconds);
        _movement.Update(World, dtSeconds, _control.ControlledEntityId, _input.Direction);
        _contacts.Update(World, _offense, _defense);
        _engagement.Update(World, dtSeconds);
        _tackleResolution.Update(World, dtSeconds, _ballEntityId, ref _control, _play);
        _behaviorStack.Update(World, dtSeconds);

        // Debug fumble trigger + loose-ball recovery.
        _fumbleDebug.Update(World, _ballEntityId, _ui);
        _fumbleResolution.Update(World, _ballEntityId);

        _ball.Update(World, dtSeconds);
        if (kickoffActive)
            _kickoffPlay.UpdatePostBall(World, _match, _play, _ballEntityId, ref _control);
        else if (puntActive)
            _puntPlay.UpdatePostBall(World, _match, _play, _ballEntityId, ref _control);
        else if (fieldGoalActive)
            _fieldGoalPlay.UpdatePostBall(World, _match, _play, _ballEntityId, ref _control);
        else
            _passComplete.Update(World);
        _loosePickup.Update(World, _ballEntityId, ref _control, _match, _play);

        if (_loosePickup.RecoveredThisTick)
        {
            _playResult.ResolveOnTackle(World, _ballEntityId);

            var reason = _loosePickup.TurnoverThisTick
                ? TecmoSBGame.SimArch.State.WhistleReason.Turnover
                : TecmoSBGame.SimArch.State.WhistleReason.Tackle;
            var ended = new TecmoSBGame.SimArch.Events.PlayEndedEvent(
                PlayId: _play.PlayId,
                Reason: (int)reason,
                EndAbsoluteYard: _play.EndAbsoluteYard,
                YardsGained: _play.Result.YardsGained,
                Turnover: _play.Result.Turnover,
                Touchdown: _play.Result.Touchdown,
                Safety: _play.Result.Safety);
            SimEventBus.Send(ref ended);
        }

        // Convert sim whistle into lifecycle events.
        if (_tackleResolution.WhistledThisTick)
        {
            // Compute end spot + yards gained from world coordinates.
            _playResult.ResolveOnTackle(World, _ballEntityId);

            var w = new TecmoSBGame.SimArch.Events.WhistleEvent("tackle");
            SimEventBus.Send(ref w);

            var ended = new TecmoSBGame.SimArch.Events.PlayEndedEvent(
                PlayId: _play.PlayId,
                Reason: (int)TecmoSBGame.SimArch.State.WhistleReason.Tackle,
                EndAbsoluteYard: _play.EndAbsoluteYard,
                YardsGained: _play.Result.YardsGained,
                Turnover: _play.Result.Turnover,
                Touchdown: _play.Result.Touchdown,
                Safety: _play.Result.Safety);
            SimEventBus.Send(ref ended);
        }

        foreach (var resolved in SimEventBus.Drain<PassResolvedEvent>())
        {
            if (resolved.Outcome != PassOutcome.Incomplete)
                continue;

            var ended = new TecmoSBGame.SimArch.Events.PlayEndedEvent(
                PlayId: _play.PlayId,
                Reason: (int)TecmoSBGame.SimArch.State.WhistleReason.Incomplete,
                EndAbsoluteYard: _play.StartAbsoluteYard,
                YardsGained: 0,
                Turnover: false,
                Touchdown: false,
                Safety: false);
            SimEventBus.Send(ref ended);
        }

        // Lifecycle transitions (event-driven; no auto-snap/auto-advance shortcuts).
        _lifecycle.Update(World);

        foreach (var _ in SimEventBus.Drain<HalftimeEvent>())
        {
            // wait for explicit continue after halftime
        }

        foreach (var _ in SimEventBus.Drain<GameEndedEvent>())
        {
            _match.MatchOver = true;
        }

        if (_play.IsOver && _play.PlayId != _lastAppliedPlayEndPlayId)
        {
            _downDistance.ApplyPlayEnd(_match, _play);
            _lastAppliedPlayEndPlayId = _play.PlayId;
            _lastPlaySummary = BuildLastPlaySummary(_match, _play);
        }

        // Play time/clock tick (after lifecycle transitions).
        if (_play.Phase == TecmoSBGame.SimArch.State.PlayPhase.InPlay)
        {
            _play.PlayElapsedSeconds += dtSeconds;
            _clock.Update(_match, _play);
        }

        _playEnd.Update(World, _ballEntityId, _match, _play);
        _nextPlayReset.Update(World, _ballEntityId, _match, _play);

        if (_match.Phase == SimArch.State.MatchPhase.Halftime && _play.Phase == TecmoSBGame.SimArch.State.PlayPhase.PreSnap)
            _clock.AdvanceFromHalftime(_match);
        // NOTE: tackle detection is now handled by CollisionContactSystem + TackleResolutionSystem.
        // Keep this system wired only for any remaining play-end/reset scaffolding.
        // _tackleAndEnd.Update(World, _ballEntityId, ref _control);

        // Update snapshot.
        Snapshot.Tick++;
        UpdateSnapshot();
    }

    private void BootstrapWorld()
    {
        // Arch can allocate entity ids starting at 0; our gameplay code uses 0 as a "null" sentinel.
        // Ensure roster ids start at 1+.
        _ = World.Create();

        // If the host didn't provide YAML configs (e.g. quick headless), try to load from repo content.
        if (_formationData is null)
        {
            try
            {
                _formationData = TecmoSB.FormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "formation_data.yaml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sim-arch] WARN: could not load formation_data.yaml; using demo roster. err={ex.Message}");
            }
        }

        if (_defensiveFormationData is null)
        {
            try
            {
                _defensiveFormationData = TecmoSB.DefensiveFormationDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "formations", "defensive_formation_data.yaml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sim-arch] WARN: could not load defensive_formation_data.yaml; using placeholder defense. err={ex.Message}");
            }
        }

        if (_playData is null)
        {
            try
            {
                _playData = TecmoSB.PlayDataYamlLoader.LoadFromFile(System.IO.Path.Combine("content", "playdata", "bank5_6_play_data.yaml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sim-arch] WARN: could not load playdata YAML; play selection will be limited. err={ex.Message}");
                _playData = null;
            }
        }

        var (off, def, ball) = _formationData is null || _defensiveFormationData is null
            ? Spawning.FormationSpawner.SpawnDemoScrimmage(World)
            : Spawning.FormationSpawner.SpawnScrimmage(World, _formationData, _defensiveFormationData);
        _offense.AddRange(off);
        _defense.AddRange(def);
        _ballEntityId = ball;

        _control = new Components.Control
        {
            ControlledEntityId = off.Count > 0 ? off[0] : -1,
            PendingForcedEntityId = -1,
            PreviousControlledEntityId = -1,
        };
    }

    private void UpdateSnapshot()
    {
        // Collect players
        var players = new SimSnapshot.PlayerSnapshot[_offense.Count + _defense.Count];
        var idx = 0;

        // Lookups
        var posById = new System.Collections.Generic.Dictionary<int, Microsoft.Xna.Framework.Vector2>();
        var teamById = new System.Collections.Generic.Dictionary<int, Components.Team>();
        var roleById = new System.Collections.Generic.Dictionary<int, Components.Role>();
        var playerRoleById = new System.Collections.Generic.Dictionary<int, Components.PlayerRole>();
        var behaviorById = new System.Collections.Generic.Dictionary<int, Components.Behavior>();

        var qPlayers = new QueryDescription().WithAll<Components.Position, Components.Team>();
        World.Query(in qPlayers, (Entity e, ref Components.Position p, ref Components.Team t) =>
        {
            posById[e.Id] = p.Value;
            teamById[e.Id] = t;
        });

        var qRole = new QueryDescription().WithAll<Components.Role>();
        World.Query(in qRole, (Entity e, ref Components.Role r) => roleById[e.Id] = r);

        var qPlayerRole = new QueryDescription().WithAll<Components.PlayerRole>();
        World.Query(in qPlayerRole, (Entity e, ref Components.PlayerRole pr) => playerRoleById[e.Id] = pr);

        var qBeh = new QueryDescription().WithAll<Components.Behavior>();
        World.Query(in qBeh, (Entity e, ref Components.Behavior b) => behaviorById[e.Id] = b);

        void Fill(int entityId)
        {
            if (!posById.TryGetValue(entityId, out var pos))
                return;
            if (!teamById.TryGetValue(entityId, out var team))
                return;

            roleById.TryGetValue(entityId, out var role);
            playerRoleById.TryGetValue(entityId, out var playerRole);
            behaviorById.TryGetValue(entityId, out var beh);

            players[idx] = new SimSnapshot.PlayerSnapshot
            {
                EntityId = entityId,
                Position = pos,
                TeamIndex = team.TeamIndex,
                IsOffense = team.IsOffense,
                HasBall = false,
                IsPlayerControlled = team.IsPlayerControlled,
                SpriteId = team.IsOffense ? "qb" : "def",
                Role = role.Id.ToString(),
                Slot = playerRole.Slot ?? string.Empty,
                Behavior = beh.State.ToString(),
            };
            idx++;
        }

        foreach (var id in _offense) Fill(id);
        foreach (var id in _defense) Fill(id);

        if (idx != players.Length)
            Array.Resize(ref players, idx);

        Snapshot.Players = players;

        // HUD snapshot
        Snapshot.Hud = new SimSnapshot.HudSnapshot
        {
            Quarter = _match.Quarter,
            GameClockSeconds = _match.GameClockSeconds,
            Team0Score = _match.Team0Score,
            Team1Score = _match.Team1Score,
            PossessionTeam = _match.PossessionTeam,
            AwayTeamId = _match.AwayTeamId,
            HomeTeamId = _match.HomeTeamId,
            Down = _match.Down,
            YardsToGo = _match.YardsToGo,
            GoalToGo = _match.GoalToGo,
            BallOnOwnSide = _match.BallSpot.OnOwnSide,
            BallYards = _match.BallSpot.Yards,
            ClockRunning = _match.ClockRunning,
            Paused = _paused,
            MatchOver = _match.MatchOver,
            PlayNumber = _match.PlayNumber,
            PossessionLabel = BuildPossessionLabel(_match),
            SituationLabel = BuildSituationLabel(_match),
            StatusLine = BuildStatusLine(_match, _play, _paused),
            LastPlaySummary = _lastPlaySummary,
        };

        // Engagement lines (from Engagement component partner pairs)
        {
            var lines = new System.Collections.Generic.List<SimSnapshot.EngagementLine>();
            var qEng = new QueryDescription().WithAll<Components.Engagement>();
            World.Query(in qEng, (Entity e, ref Components.Engagement eng) =>
            {
                if (eng.PartnerEntityId < 0)
                    return;

                // Only draw one line per pair.
                if (e.Id > eng.PartnerEntityId)
                    return;

                if (!posById.TryGetValue(e.Id, out var aPos))
                    return;
                if (!posById.TryGetValue(eng.PartnerEntityId, out var bPos))
                    return;

                lines.Add(new SimSnapshot.EngagementLine(e.Id, eng.PartnerEntityId, aPos, bPos));
            });

            Snapshot.EngagementLines = lines.ToArray();
        }

        // Route debug
        {
            var routes = new System.Collections.Generic.List<SimSnapshot.RouteDebug>();
            var qRoutes = new QueryDescription().WithAll<Components.RouteFollow, Components.Behavior>();
            World.Query(in qRoutes, (Entity e, ref Components.RouteFollow rf, ref Components.Behavior b) =>
            {
                routes.Add(new SimSnapshot.RouteDebug(
                    EntityId: e.Id,
                    TargetPosition: b.TargetPosition,
                    NodeIndex: rf.NodeIndex,
                    FramesRemaining: rf.FramesRemainingInNode,
                    Completed: rf.Completed));
            });

            Snapshot.Routes = routes.ToArray();
        }

        // Coverage debug
        {
            var cov = new System.Collections.Generic.List<SimSnapshot.CoverageDebug>();
            var qCov = new QueryDescription().WithAll<Components.Coverage>();
            World.Query(in qCov, (Entity e, ref Components.Coverage c) =>
            {
                cov.Add(new SimSnapshot.CoverageDebug(
                    DefenderId: e.Id,
                    Type: (SnapshotCoverageType)c.Type,
                    AssignmentTargetId: c.AssignmentTargetId,
                    PursuitTargetId: c.PursuitTargetId,
                    InPursuit: c.InPursuit,
                    Landmark: c.LandmarkPosition));
            });

            Snapshot.Coverage = cov.ToArray();
        }

        // Playcall overlay
        {
            var playCallSnapshot = new SimSnapshot.PlayCallOverlaySnapshot
            {
                Visible = _play.Phase == TecmoSBGame.SimArch.State.PlayPhase.PreSnap,
                Focus = PlayCallFocus.Formation,
                SelectedFormationId = string.Empty,
                SelectedPlayName = string.Empty,
                FormationWindow = Array.Empty<string>(),
                PlayWindow = Array.Empty<string>(),
            };

            var qPlayCall = new QueryDescription().WithAll<PlayCallState>();
            World.Query(in qPlayCall, (Entity _, ref PlayCallState pcs) =>
            {
                playCallSnapshot.Visible = _play.Phase == TecmoSBGame.SimArch.State.PlayPhase.PreSnap;
                playCallSnapshot.Focus = pcs.Focus;
                playCallSnapshot.SelectedFormationId = pcs.SelectedFormationId;
                playCallSnapshot.SelectedPlayName = pcs.SelectedPlay?.Name ?? string.Empty;
                playCallSnapshot.FormationWindow = BuildWindow(pcs.FormationIds, pcs.FormationIndex);
                playCallSnapshot.PlayWindow = BuildWindow(pcs.PlaysForFormation, pcs.PlayIndex, static p => p.Name ?? string.Empty);
            });

            Snapshot.PlayCall = playCallSnapshot;
        }

        // Ball
        {
            // Ball
            var qBall = new QueryDescription().WithAll<Components.Ball, Components.Position>();
            var didSetBall = false;

            World.Query(in qBall, (Entity e, ref Components.Ball b, ref Components.Position p) =>
            {
                if (e.Id != _ballEntityId)
                    return;

                Snapshot.Ball = new SimSnapshot.BallSnapshot
                {
                    Position = p.Value,
                    IsHeld = b.State == TecmoSBGame.SimArch.Components.BallState.Held,
                    OwnerEntityId = b.OwnerEntityId,
                    SpriteId = "ball",
                };
                didSetBall = true;
            });

            if (didSetBall)
            {
                var b = Snapshot.Ball;

                // Mark owner in players snapshot
                if (b.OwnerEntityId >= 0)
                {
                    for (var i = 0; i < Snapshot.Players.Length; i++)
                    {
                        if (Snapshot.Players[i].EntityId == b.OwnerEntityId)
                        {
                            Snapshot.Players[i].HasBall = true;
                            break;
                        }
                    }
                }

                return;
            }
        }

        Snapshot.Ball = default;
    }

    private static string[] BuildWindow(System.Collections.Generic.IReadOnlyList<string> items, int selectedIndex)
    {
        if (items.Count == 0)
            return Array.Empty<string>();

        var start = Math.Max(0, selectedIndex - 1);
        var end = Math.Min(items.Count - 1, selectedIndex + 1);
        var window = new System.Collections.Generic.List<string>(3);
        for (var i = start; i <= end; i++)
        {
            var prefix = i == selectedIndex ? $"[{items[i]}]" : items[i];
            window.Add(prefix);
        }

        return window.ToArray();
    }

    private static string[] BuildWindow(System.Collections.Generic.IReadOnlyList<TecmoSB.PlayEntry> items, int selectedIndex, Func<TecmoSB.PlayEntry, string> label)
    {
        if (items.Count == 0)
            return Array.Empty<string>();

        var start = Math.Max(0, selectedIndex - 1);
        var end = Math.Min(items.Count - 1, selectedIndex + 1);
        var window = new System.Collections.Generic.List<string>(3);
        for (var i = start; i <= end; i++)
        {
            var text = label(items[i]);
            window.Add(i == selectedIndex ? $"[{text}]" : text);
        }

        return window.ToArray();
    }

    private static string BuildPossessionLabel(State.MatchState match)
    {
        return match.PossessionTeam == 0 ? $"AWAY #{match.AwayTeamId}" : $"HOME #{match.HomeTeamId}";
    }

    private static string BuildSituationLabel(State.MatchState match)
    {
        var side = match.BallSpot.OnOwnSide ? "OWN" : "OPP";
        var distance = match.GoalToGo ? "GOAL" : match.YardsToGo.ToString();
        return $"{FormatDown(match.Down)} & {distance} AT {side} {match.BallSpot.Yards}";
    }

    private static string BuildStatusLine(State.MatchState match, State.PlayState play, bool paused)
    {
        if (paused)
            return "PAUSED · PRESS P TO RESUME";
        if (match.MatchOver)
            return "FINAL";
        if (match.Phase == State.MatchPhase.Halftime)
            return "HALFTIME · PRESS ENTER TO CONTINUE";
        if (play.Phase == State.PlayPhase.PostPlay)
            return (play.Result.Turnover || play.Result.Touchdown || play.Result.Safety) ? "POST-PLAY · AUTO ADVANCING" : "POST-PLAY · PRESS ENTER TO CONTINUE";
        if (play.Phase == State.PlayPhase.PreSnap)
            return match.ClockRunning ? "PRE-SNAP · SELECT PLAY / SNAP" : "PRE-SNAP · SELECT PLAY";
        return "LIVE PLAY";
    }

    private static string BuildLastPlaySummary(State.MatchState match, State.PlayState play)
    {
        if (play.Result.Touchdown)
            return $"TOUCHDOWN · {DescribeYards(play.Result.YardsGained)}";
        if (play.Result.Safety)
            return "SAFETY";
        if (play.Result.Turnover)
            return $"TURNOVER · {DescribeWhistle(play.WhistleReason)}";
        if (play.WhistleReason == State.WhistleReason.Incomplete)
            return "INCOMPLETE PASS";
        if (match.Down == 1 && play.Result.YardsGained > 0)
            return $"FIRST DOWN · {DescribeYards(play.Result.YardsGained)}";
        return DescribeYards(play.Result.YardsGained);
    }

    private static string DescribeYards(int yards)
    {
        if (yards > 0)
            return $"GAIN OF {yards}";
        if (yards < 0)
            return $"LOSS OF {Math.Abs(yards)}";
        return "NO GAIN";
    }

    private static string DescribeWhistle(State.WhistleReason whistleReason)
        => whistleReason switch
        {
            State.WhistleReason.Touchback => "TOUCHBACK",
            State.WhistleReason.OutOfBounds => "OUT OF BOUNDS",
            State.WhistleReason.Incomplete => "INCOMPLETE",
            _ => "CHANGE OF POSSESSION",
        };

    private static string FormatDown(int down)
        => down switch
        {
            1 => "1ST",
            2 => "2ND",
            3 => "3RD",
            4 => "4TH",
            _ => $"{down}TH",
        };

    public readonly record struct PendingPlaySelection(
        int PlayNumber,
        string FormationId,
        string OffensivePlayName,
        string OffensivePlaySlot);
}
