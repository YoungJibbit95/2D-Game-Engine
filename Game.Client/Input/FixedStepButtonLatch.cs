namespace Game.Client.Input;

/// <summary>
/// Preserves a variable-update button press until one authoritative fixed step consumes it.
/// The held state remains live so release-sensitive Core controls are not delayed.
/// </summary>
public sealed class FixedStepButtonLatch
{
    private bool _isHeld;
    private bool _pressedSinceConsume;

    public bool IsActiveForFixedStep => _isHeld || _pressedSinceConsume;

    public void Observe(bool isHeld, bool wasPressed)
    {
        _isHeld = isHeld;
        _pressedSinceConsume |= wasPressed;
    }

    public void ConsumePress()
    {
        _pressedSinceConsume = false;
    }

    public void Reset()
    {
        _isHeld = false;
        _pressedSinceConsume = false;
    }
}
