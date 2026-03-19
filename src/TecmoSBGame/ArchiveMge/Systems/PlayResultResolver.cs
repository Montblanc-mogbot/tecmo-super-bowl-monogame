using Microsoft.Xna.Framework;
using MonoGame.Extended.Entities;
using TecmoSBGame.Components;
using TecmoSBGame.State;

namespace TecmoSBGame.Systems;

/// <summary>
/// Resolves the result of plays including yards gained, penalties, and special outcomes.
/// </summary>
public class PlayResultResolver
{
    private readonly World _world;
    private readonly GameStateManager _gameState;
    
    // Events
    public event Action<PlayResult>? OnPlayResolved;
    public event Action<int, int>? OnBigPlay; // yards, playerId
    public event Action? OnSack;
    public event Action? OnInterception;
    public event Action? OnFumble;

    public PlayResultResolver(World world, GameStateManager gameState)
    {
        _world = world;
        _gameState = gameState;
    }

    /// <summary>
    /// Resolves a completed play and returns the result.
    /// </summary>
    public PlayResult ResolvePlay(PlayContext context)
    {
        var result = new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            BallCarrierId = context.BallCarrierId,
            IsComplete = false
        };

        // Determine end position based on play outcome
        switch (context.Outcome)
        {
            case PlayOutcome.Tackle:
                result = ResolveTackle(context);
                break;

            case PlayOutcome.OutOfBounds:
                result = ResolveOutOfBounds(context);
                break;

            case PlayOutcome.Touchdown:
                result = ResolveTouchdown(context);
                break;

            case PlayOutcome.Safety:
                result = ResolveSafety(context);
                break;

            case PlayOutcome.Sack:
                result = ResolveSack(context);
                break;

            case PlayOutcome.IncompletePass:
                result = ResolveIncompletePass(context);
                break;

            case PlayOutcome.Interception:
                result = ResolveInterception(context);
                break;

            case PlayOutcome.Fumble:
                result = ResolveFumble(context);
                break;

            case PlayOutcome.Penalty:
                result = ResolvePenalty(context);
                break;
        }

        // Calculate yards gained/lost
        result.YardsGained = CalculateYards(context.StartPosition, result.EndPosition, context.Direction);
        
        // Check for first down
        result.FirstDown = result.YardsGained >= context.YardsNeeded;
        
        // Check for big play (20+ yards)
        if (result.YardsGained >= 20 && result.IsComplete)
        {
            OnBigPlay?.Invoke(result.YardsGained, context.BallCarrierId);
        }

        // Update game state
        UpdateGameState(result);

        result.IsComplete = true;
        OnPlayResolved?.Invoke(result);
        
        return result;
    }

    private PlayResult ResolveTackle(PlayContext context)
    {
        var ballCarrier = _world.GetEntity(context.BallCarrierId);
        var position = ballCarrier.Get<PositionComponent>();
        
        return new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            EndPosition = position.Position,
            Outcome = PlayOutcome.Tackle,
            TacklerId = context.DefenderId,
            BallCarrierId = context.BallCarrierId,
            IsComplete = true
        };
    }

    private PlayResult ResolveOutOfBounds(PlayContext context)
    {
        var ballCarrier = _world.GetEntity(context.BallCarrierId);
        var position = ballCarrier.Get<PositionComponent>();
        
        return new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            EndPosition = position.Position,
            Outcome = PlayOutcome.OutOfBounds,
            BallCarrierId = context.BallCarrierId,
            IsComplete = true
        };
    }

    private PlayResult ResolveTouchdown(PlayContext context)
    {
        return new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            EndPosition = context.Direction ? new Vector2(100, 0) : new Vector2(0, 0),
            Outcome = PlayOutcome.Touchdown,
            BallCarrierId = context.BallCarrierId,
            YardsGained = 100 - (int)context.StartPosition.Y,
            FirstDown = true,
            IsComplete = true
        };
    }

    private PlayResult ResolveSafety(PlayContext context)
    {
        return new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            EndPosition = context.Direction ? new Vector2(0, 0) : new Vector2(100, 0),
            Outcome = PlayOutcome.Safety,
            BallCarrierId = context.BallCarrierId,
            YardsGained = -(int)context.StartPosition.Y,
            IsComplete = true
        };
    }

    private PlayResult ResolveSack(PlayContext context)
    {
        OnSack?.Invoke();
        
        var qb = _world.GetEntity(context.BallCarrierId);
        var position = qb.Get<PositionComponent>();
        
        return new PlayResult
        {
            PlayType = PlayType.Pass,
            StartPosition = context.StartPosition,
            EndPosition = position.Position,
            Outcome = PlayOutcome.Sack,
            TacklerId = context.DefenderId,
            BallCarrierId = context.BallCarrierId,
            IsComplete = true
        };
    }

    private PlayResult ResolveIncompletePass(PlayContext context)
    {
        return new PlayResult
        {
            PlayType = PlayType.Pass,
            StartPosition = context.StartPosition,
            EndPosition = context.StartPosition,
            Outcome = PlayOutcome.IncompletePass,
            YardsGained = 0,
            FirstDown = false,
            IsComplete = true
        };
    }

    private PlayResult ResolveInterception(PlayContext context)
    {
        OnInterception?.Invoke();
        
        var defender = _world.GetEntity(context.DefenderId);
        var position = defender.Get<PositionComponent>();
        
        return new PlayResult
        {
            PlayType = PlayType.Pass,
            StartPosition = context.StartPosition,
            EndPosition = position.Position,
            Outcome = PlayOutcome.Interception,
            DefenderId = context.DefenderId,
            IsTurnover = true,
            IsComplete = true
        };
    }

    private PlayResult ResolveFumble(PlayContext context)
    {
        OnFumble?.Invoke();
        
        var ballCarrier = _world.GetEntity(context.BallCarrierId);
        var position = ballCarrier.Get<PositionComponent>();
        
        return new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            EndPosition = position.Position,
            Outcome = PlayOutcome.Fumble,
            BallCarrierId = context.BallCarrierId,
            IsTurnover = context.FumbleRecoveredByDefense,
            IsComplete = true
        };
    }

    private PlayResult ResolvePenalty(PlayContext context)
    {
        return new PlayResult
        {
            PlayType = context.PlayType,
            StartPosition = context.StartPosition,
            EndPosition = context.StartPosition,
            Outcome = PlayOutcome.Penalty,
            YardsGained = 0,
            FirstDown = false,
            PenaltyYards = context.PenaltyYards,
            PenaltyAccepted = context.PenaltyAccepted,
            IsComplete = true
        };
    }

    private int CalculateYards(Vector2 start, Vector2 end, bool facingRight)
    {
        float deltaX = end.X - start.X;
        return facingRight ? (int)deltaX : -(int)deltaX;
    }

    private void UpdateGameState(PlayResult result)
    {
        if (!result.IsComplete)
            return;

        // Handle scoring
        switch (result.Outcome)
        {
            case PlayOutcome.Touchdown:
                _gameState.Touchdown();
                break;

            case PlayOutcome.Safety:
                _gameState.Safety();
                break;

            case PlayOutcome.Interception:
            case PlayOutcome.Fumble when result.IsTurnover:
                _gameState.EndPlay(0, false, false, true);
                break;

            default:
                _gameState.EndPlay(result.YardsGained, result.FirstDown);
                break;
        }
    }
}

/// <summary>
/// Context for resolving a play.
/// </summary>
public class PlayContext
{
    public PlayType PlayType { get; set; }
    public Vector2 StartPosition { get; set; }
    public int BallCarrierId { get; set; }
    public int DefenderId { get; set; } = -1;
    public PlayOutcome Outcome { get; set; }
    public bool Direction { get; set; } // true = facing right, false = facing left
    public int YardsNeeded { get; set; } = 10;
    
    // Fumble specific
    public bool FumbleRecoveredByDefense { get; set; }
    
    // Penalty specific
    public int PenaltyYards { get; set; }
    public bool PenaltyAccepted { get; set; }
}

/// <summary>
/// The result of a resolved play.
/// </summary>
public class PlayResult
{
    public PlayType PlayType { get; set; }
    public PlayOutcome Outcome { get; set; }
    public Vector2 StartPosition { get; set; }
    public Vector2 EndPosition { get; set; }
    public int YardsGained { get; set; }
    public bool FirstDown { get; set; }
    public bool IsTurnover { get; set; }
    public bool IsComplete { get; set; }
    
    // Entity references
    public int BallCarrierId { get; set; } = -1;
    public int TacklerId { get; set; } = -1;
    public int DefenderId { get; set; } = -1;
    
    // Penalty info
    public int PenaltyYards { get; set; }
    public bool PenaltyAccepted { get; set; }
}

/// <summary>
/// Possible outcomes of a play.
/// </summary>
public enum PlayOutcome
{
    Tackle,
    OutOfBounds,
    Touchdown,
    Safety,
    Sack,
    IncompletePass,
    Interception,
    Fumble,
    Penalty,
    NoGain
}
