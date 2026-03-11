using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.Entities;
using MonoGame.Extended.Entities.Systems;
using TecmoSB;
using TecmoSBGame.Components.PlayCall;
using TecmoSBGame.Events;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems.PlayCall;

/// <summary>
/// Pre-snap playcall selection state machine.
///
/// Input (keyboard/gamepad):
/// - D-pad / arrows: navigate
/// - A / Enter / Space: confirm/select
/// - B / Escape / Back: cancel/back
/// - Tab / Y: toggle focus (formation/play) on offense
/// - Select: toggle offense/defense pane
/// - Start: finalize and emit <see cref="PlaySelectedEvent"/>
/// </summary>
public sealed class PlayCallSystem : EntityUpdateSystem
{
    private readonly LoopState _loop;
    private readonly PlayState _play;
    private readonly GameEvents _events;

    private readonly FormationDataConfig _formations;
    private readonly PlayListConfig _playlist;
    private readonly DefensePlayConfig _defense;

    private ComponentMapper<PlayCallComponent> _pc = null!;

    public PlayCallSystem(
        LoopState loopState,
        PlayState playState,
        GameEvents events,
        FormationDataConfig formations,
        PlayListConfig playlist,
        DefensePlayConfig defense)
        : base(Aspect.All(typeof(PlayCallComponent)))
    {
        _loop = loopState ?? throw new ArgumentNullException(nameof(loopState));
        _play = playState ?? throw new ArgumentNullException(nameof(playState));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _formations = formations ?? throw new ArgumentNullException(nameof(formations));
        _playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
        _defense = defense ?? throw new ArgumentNullException(nameof(defense));
    }

    public override void Initialize(IComponentMapperService mapperService)
    {
        _pc = mapperService.GetMapper<PlayCallComponent>();
    }

    public override void Update(GameTime gameTime)
    {
        // Only show playcall during on-field pre-snap, and only when the ball is dead (scrimmage pre-snap).
        // Kickoff pre-snap uses BallState.Held and does not show playcall.
        var shouldShow = _loop.IsOnField("pre_snap")
            && _play.Phase == PlayPhase.PreSnap
            && _play.BallState == BallState.Dead
            && !_play.PlayCallLockedIn;

        foreach (var entityId in ActiveEntities)
        {
            var pc = _pc.Get(entityId);
            pc.Visible = shouldShow;

            if (!shouldShow)
            {
                pc.WasVisible = false;
                continue;
            }

            // Rising edge: playcall became visible for a new pre-snap slice.
            if (!pc.WasVisible)
            {
                pc.Step = PlayCallStep.Offense;
                pc.Focus = PlayCallFocus.Formation;
                pc.LastAutoPlaycallPlayId = -1;

                // Force a rebuild of plays list.
                pc.SelectedFormationId = "";
                pc.SelectedPlay = null;
                pc.SelectedDefenseId = "";

                // Default to first formation that actually has plays.
                if (pc.FormationIds.Count > 0 && _playlist.PlayList is not null)
                {
                    for (var i = 0; i < pc.FormationIds.Count; i++)
                    {
                        var fid = pc.FormationIds[i];
                        if (_playlist.PlayList.Any(p => string.Equals(p.Formation, fid, StringComparison.OrdinalIgnoreCase)))
                        {
                            pc.FormationIndex = i;
                            pc.PlayIndex = 0;
                            break;
                        }
                    }
                }
            }

            EnsureLists(pc);
            EnsureSelectionValid(pc);

            // Dev shortcut: auto-select a deterministic play every down until a real playcall system exists.
            // NOTE: manual playcall input will never work if AutoPlaycallEnabled is true.
            if (_play.AutoPlaycallEnabled)
            {
                HandleAutoPlaycall(pc);
            }
            else
            {
                HandleInput(pc);
            }

            // Keep convenience fields fresh.
            SyncSelected(pc);

            // Mark visible for edge detection.
            pc.WasVisible = true;
        }
    }

    private void EnsureLists(PlayCallComponent pc)
    {
        if (pc.FormationIds.Count == 0)
        {
            foreach (var f in _formations.OffensiveFormations)
                pc.FormationIds.Add(f.Id);

            // Default to the first formation that actually has plays (skip kickoff/test formations like "00").
            for (var i = 0; i < pc.FormationIds.Count; i++)
            {
                var fid = pc.FormationIds[i];
                if (_playlist.PlayList is null)
                    break;

                if (_playlist.PlayList.Any(p => string.Equals(p.Formation, fid, StringComparison.OrdinalIgnoreCase)))
                {
                    pc.FormationIndex = i;
                    break;
                }
            }
        }

        if (pc.DefensiveCalls.Count == 0)
        {
            if (_defense.DefensiveExecutions is not null)
                pc.DefensiveCalls.AddRange(_defense.DefensiveExecutions);
        }

        // Build plays list for current formation.
        var formationId = pc.FormationIds.Count > 0
            ? pc.FormationIds[Math.Clamp(pc.FormationIndex, 0, pc.FormationIds.Count - 1)]
            : "";

        if (!string.Equals(pc.SelectedFormationId, formationId, StringComparison.OrdinalIgnoreCase))
        {
            pc.PlaysForFormation.Clear();

            if (!string.IsNullOrWhiteSpace(formationId) && _playlist.PlayList is not null)
            {
                foreach (var p in _playlist.PlayList.Where(p => string.Equals(p.Formation, formationId, StringComparison.OrdinalIgnoreCase)))
                    pc.PlaysForFormation.Add(p);
            }

            pc.SelectedFormationId = formationId;
            pc.PlayIndex = 0;
        }
    }

    private static void EnsureSelectionValid(PlayCallComponent pc)
    {
        if (pc.FormationIds.Count <= 0)
        {
            pc.FormationIndex = 0;
            pc.PlayIndex = 0;
            pc.DefenseIndex = 0;
            return;
        }

        pc.FormationIndex = Math.Clamp(pc.FormationIndex, 0, pc.FormationIds.Count - 1);
        pc.PlayIndex = pc.PlaysForFormation.Count > 0
            ? Math.Clamp(pc.PlayIndex, 0, pc.PlaysForFormation.Count - 1)
            : 0;

        pc.DefenseIndex = pc.DefensiveCalls.Count > 0
            ? Math.Clamp(pc.DefenseIndex, 0, pc.DefensiveCalls.Count - 1)
            : 0;

        if (pc.Step == PlayCallStep.Defense)
            pc.Focus = PlayCallFocus.Defense;
        else if (pc.Focus == PlayCallFocus.Defense)
            pc.Focus = PlayCallFocus.Formation;
    }

    private void HandleAutoPlaycall(PlayCallComponent pc)
    {
        // Guard: only emit once per play.
        if (pc.LastAutoPlaycallPlayId == _play.PlayId)
            return;

        EnsureLists(pc);

        // Demo goal: pick a deterministic real Tecmo play (play_number=10 "T FAKE SWEEP R") when available.
        // If not found, fall back to the first formation with any plays.
        const int DemoPlayNumber = 10;

        var formationIndex = -1;
        var playIndex = 0;

        for (var fi = 0; fi < pc.FormationIds.Count; fi++)
        {
            pc.FormationIndex = fi;
            EnsureLists(pc);

            for (var pi = 0; pi < pc.PlaysForFormation.Count; pi++)
            {
                var p = pc.PlaysForFormation[pi];
                if (p.PlayNumbers is null)
                    continue;

                foreach (var n in p.PlayNumbers)
                {
                    if (n == DemoPlayNumber)
                    {
                        formationIndex = fi;
                        playIndex = pi;
                        break;
                    }
                }

                if (formationIndex >= 0)
                    break;
            }

            if (formationIndex >= 0)
                break;
        }

        // Fallback: Prefer formation 01 if it has any plays; otherwise first formation with any plays.
        if (formationIndex < 0)
        {
            // Try 01 first.
            var idx01 = pc.FormationIds.FindIndex(id => string.Equals(id, "01", StringComparison.OrdinalIgnoreCase));
            if (idx01 >= 0)
            {
                pc.FormationIndex = idx01;
                EnsureLists(pc);
                if (pc.PlaysForFormation.Count > 0)
                    formationIndex = idx01;
            }
        }

        if (formationIndex < 0)
        {
            for (var i = 0; i < pc.FormationIds.Count; i++)
            {
                pc.FormationIndex = i;
                EnsureLists(pc);
                if (pc.PlaysForFormation.Count > 0)
                {
                    formationIndex = i;
                    break;
                }
            }
        }

        if (formationIndex < 0)
        {
            // No data: nothing we can do.
            return;
        }

        pc.FormationIndex = formationIndex;
        pc.PlayIndex = Math.Clamp(playIndex, 0, Math.Max(0, pc.PlaysForFormation.Count - 1));
        pc.DefenseIndex = 0;

        pc.Step = PlayCallStep.Defense;
        pc.Focus = PlayCallFocus.Defense;

        EmitSelected(pc);
        pc.LastAutoPlaycallPlayId = _play.PlayId;
    }

    private void HandleInput(PlayCallComponent pc)
    {
        var kb = Keyboard.GetState();
        var gp = GamePad.GetState(PlayerIndex.One);

        var up = kb.IsKeyDown(Keys.Up) || kb.IsKeyDown(Keys.W) || gp.DPad.Up == ButtonState.Pressed;
        var down = kb.IsKeyDown(Keys.Down) || kb.IsKeyDown(Keys.S) || gp.DPad.Down == ButtonState.Pressed;
        var left = kb.IsKeyDown(Keys.Left) || kb.IsKeyDown(Keys.A) || gp.DPad.Left == ButtonState.Pressed;
        var right = kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D) || gp.DPad.Right == ButtonState.Pressed;

        var a = kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space) || gp.Buttons.A == ButtonState.Pressed;
        var b = kb.IsKeyDown(Keys.Escape) || gp.Buttons.B == ButtonState.Pressed;
        var start = kb.IsKeyDown(Keys.Tab) || gp.Buttons.Start == ButtonState.Pressed; // also doubles as focus-toggle via Tab
        var select = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift) || gp.Buttons.Back == ButtonState.Pressed;

        // Edge detection.
        var upP = up && !pc.PrevUp;
        var downP = down && !pc.PrevDown;
        var leftP = left && !pc.PrevLeft;
        var rightP = right && !pc.PrevRight;
        var aP = a && !pc.PrevA;
        var bP = b && !pc.PrevB;
        var startP = start && !pc.PrevStart;
        var selectP = select && !pc.PrevSelect;

        pc.PrevUp = up;
        pc.PrevDown = down;
        pc.PrevLeft = left;
        pc.PrevRight = right;
        pc.PrevA = a;
        pc.PrevB = b;
        pc.PrevStart = start;
        pc.PrevSelect = select;

        // Toggle offense/defense pane.
        if (selectP)
        {
            pc.Step = pc.Step == PlayCallStep.Offense ? PlayCallStep.Defense : PlayCallStep.Offense;
            pc.Focus = pc.Step == PlayCallStep.Defense ? PlayCallFocus.Defense : PlayCallFocus.Formation;
            return;
        }

        // Back.
        if (bP)
        {
            if (pc.Step == PlayCallStep.Defense)
            {
                pc.Step = PlayCallStep.Offense;
                pc.Focus = PlayCallFocus.Formation;
            }
            else
            {
                // Offense: move focus back to formation.
                pc.Focus = PlayCallFocus.Formation;
            }
            return;
        }

        // Confirm/finalize.
        // Start acts as finalize only after a play and defense are chosen.
        if (startP)
        {
            if (pc.Step == PlayCallStep.Offense)
            {
                // On offense, Tab/Start toggles focus between formation and play.
                pc.Focus = pc.Focus == PlayCallFocus.Formation ? PlayCallFocus.Play : PlayCallFocus.Formation;
                return;
            }

            // In defense pane: Start finalizes.
            EmitSelected(pc);
            return;
        }

        // A: advance.
        if (aP)
        {
            if (pc.Step == PlayCallStep.Offense)
            {
                if (pc.Focus == PlayCallFocus.Formation)
                {
                    // If this formation has no plays, don't advance focus; user must pick a valid formation.
                    if (pc.PlaysForFormation.Count == 0)
                    {
                        Console.WriteLine($"[playcall] offense confirm ignored: formation={pc.SelectedFormationId} has 0 plays");
                        return;
                    }

                    pc.Focus = PlayCallFocus.Play;
                    return;
                }

                // Tecmo intent: player selects only their offensive play.
                // Defense selection is AI-driven.
                EmitSelected(pc);
                return;
            }

            // Defense: A emits and stays in defense.
            EmitSelected(pc);
            return;
        }

        // Navigation.
        if (pc.Step == PlayCallStep.Offense)
        {
            if (pc.Focus == PlayCallFocus.Formation)
            {
                const int cols = 4;

                if (leftP) pc.FormationIndex = Math.Max(0, pc.FormationIndex - 1);
                if (rightP) pc.FormationIndex = Math.Min(pc.FormationIds.Count - 1, pc.FormationIndex + 1);
                if (upP) pc.FormationIndex = Math.Max(0, pc.FormationIndex - cols);
                if (downP) pc.FormationIndex = Math.Min(pc.FormationIds.Count - 1, pc.FormationIndex + cols);
            }
            else
            {
                if (upP) pc.PlayIndex = Math.Max(0, pc.PlayIndex - 1);
                if (downP) pc.PlayIndex = Math.Min(Math.Max(0, pc.PlaysForFormation.Count - 1), pc.PlayIndex + 1);
            }

            return;
        }

        // Defense grid nav.
        if (pc.Step == PlayCallStep.Defense)
        {
            const int cols = 4;
            if (leftP) pc.DefenseIndex = Math.Max(0, pc.DefenseIndex - 1);
            if (rightP) pc.DefenseIndex = Math.Min(Math.Max(0, pc.DefensiveCalls.Count - 1), pc.DefenseIndex + 1);
            if (upP) pc.DefenseIndex = Math.Max(0, pc.DefenseIndex - cols);
            if (downP) pc.DefenseIndex = Math.Min(Math.Max(0, pc.DefensiveCalls.Count - 1), pc.DefenseIndex + cols);
        }
    }

    private void SyncSelected(PlayCallComponent pc)
    {
        pc.SelectedFormationId = pc.FormationIds.Count > 0
            ? pc.FormationIds[Math.Clamp(pc.FormationIndex, 0, pc.FormationIds.Count - 1)]
            : "";

        pc.SelectedPlay = pc.PlaysForFormation.Count > 0
            ? pc.PlaysForFormation[Math.Clamp(pc.PlayIndex, 0, pc.PlaysForFormation.Count - 1)]
            : null;

        pc.SelectedDefenseId = pc.DefensiveCalls.Count > 0
            ? pc.DefensiveCalls[Math.Clamp(pc.DefenseIndex, 0, pc.DefensiveCalls.Count - 1)].Id
            : "";
    }

    private void EmitSelected(PlayCallComponent pc)
    {
        SyncSelected(pc);

        var off = pc.SelectedPlay;
        string def = ""; // AI-selected (do not require player to pick defense)

        // Graceful no-data behavior: only emit if we have at least an offensive play.
        if (off is null)
        {
            Console.WriteLine("[playcall] emit skipped: no offensive play selected");
            return;
        }

        var playNo = off.PlayNumbers is not null && off.PlayNumbers.Count > 0 ? off.PlayNumbers[0] : 0;

        Console.WriteLine($"[playcall] emit selected step={pc.Step} focus={pc.Focus} formation={off.Formation ?? pc.SelectedFormationId} slot={off.Slot} play=\"{off.Name}\" play_number={playNo} def={def}");

        _events.Publish(new PlaySelectedEvent(
            OffensiveFormationId: off.Formation ?? pc.SelectedFormationId,
            OffensivePlayName: off.Name ?? "",
            OffensivePlaySlot: off.Slot ?? "",
            OffensivePlayNumber: playNo,
            DefensiveCallId: def ?? ""));
    }
}
