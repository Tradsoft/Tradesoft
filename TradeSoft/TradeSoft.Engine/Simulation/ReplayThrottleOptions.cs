namespace TradeSoft.Engine.Simulation;

public sealed class ReplayThrottleOptions
{
    /// <summary>Vitesse de replay. 1.0 = temps réel, 10.0 = 10x plus vite.</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>
    /// Si retard (CPU / latence), on ne "rattrape" pas en dormant.
    /// On continue sans attendre.
    /// </summary>
    public bool CatchUpWhenBehind { get; init; } = true;

    /// <summary>
    /// Pour éviter de faire un Delay énorme (debug). Ex: 200 ms.
    /// </summary>
    public int MaxDelayMs { get; init; } = 250;

    /// <summary>Si true, on ignore le throttle (replay à fond) tout en gardant les hooks debug.</summary>
    public bool Disabled { get; init; } = false;

    public void Validate()
    {
        if (Speed <= 0.0) throw new ArgumentOutOfRangeException(nameof(Speed), "Speed must be > 0.");
        if (MaxDelayMs < 0) throw new ArgumentOutOfRangeException(nameof(MaxDelayMs));
    }
}
