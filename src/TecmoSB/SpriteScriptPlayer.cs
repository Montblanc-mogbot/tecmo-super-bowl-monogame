namespace TecmoSB;

/// Minimal interpreter for YAML-authored sprite scripts.
public sealed class SpriteScriptPlayer
{
    private readonly SpriteScript _script;
    private int _frameIndex;
    private int _ticksLeft;

    public int OriginX { get; private set; }
    public int OriginY { get; private set; }

    public SpriteScriptPlayer(SpriteScript script)
    {
        _script = script;
        _frameIndex = 0;
        _ticksLeft = script.Frames.Count > 0 ? script.Frames[0].Duration : 0;
    }

    public SpriteFrame? CurrentFrame =>
        _script.Frames.Count == 0 ? null : _script.Frames[_frameIndex];

    public void Tick()
    {
        if (_script.Frames.Count == 0) return;

        if (_ticksLeft > 0) _ticksLeft--;
        if (_ticksLeft > 0) return;

        // apply op at end of frame (v0)
        var op = _script.Frames[_frameIndex].Op;
        if (op is { Kind: "moveOrigin" })
        {
            OriginX += op.Dx;
            OriginY += op.Dy;
        }

        _frameIndex++;
        if (_frameIndex >= _script.Frames.Count)
        {
            if (_script.Loop is { Kind: "loopTo" } loop)
            {
                _frameIndex = Math.Clamp(loop.Frame, 0, _script.Frames.Count - 1);
            }
            else
            {
                _frameIndex = _script.Frames.Count - 1;
            }
        }

        _ticksLeft = _script.Frames[_frameIndex].Duration;
    }
}
