namespace Mhodume;

/// <summary>
/// What the player was holding, frame by frame, in a recorded run.
///
/// The recording stores it as a bit field called "inp", written only when it
/// changes. Nothing documents which bit is which, so it was read off the
/// recordings: 44 ghosts, 30,059 frames, every bit correlated against the
/// state stored beside it — velocity in the character's own frame, crouch, and
/// the transitions of movementMode.
///
/// Bits 4 and 5 are set three quarters of the time, go quiet when the run is
/// fast (1578 average speed while set against 3800 while clear), and match
/// nothing else in the recording. They are carried through unnamed rather than
/// guessed at.
/// </summary>
public static class GhostInputs
{
    public record Key(int Bit, string Short, string Name);

    /// <summary>In the order they are shown, which is the order they are used.</summary>
    public static readonly Key[] Keys =
    {
        new(0, "W", "Forward"),
        new(3, "A", "Left"),
        new(1, "S", "Back"),
        new(2, "D", "Right"),
        new(9, "JMP", "Jump"),
        new(6, "CRH", "Crouch"),
    };

    /// <summary>
    /// The six bits above, together. Everything else the recording sets is
    /// carried by the game for its own reasons and cannot be labelled, so it
    /// is dropped rather than shown as an unexplained change: bits 4 and 5
    /// alone toggle several hundred times in an ordinary run.
    /// </summary>
    public static readonly int Named = Keys.Aggregate(0, (m, k) => m | (1 << k.Bit));

    public static bool Held(int mask, Key key) => (mask & (1 << key.Bit)) != 0;

    /// <summary>The keys held, as "W A JMP", or "" for nothing.</summary>
    public static string Describe(int mask) =>
        string.Join(" ", Keys.Where(k => Held(mask, k)).Select(k => k.Short));
}

/// <summary>One moment in a run and what was held at it.</summary>
public record InputMoment(double Seconds, int Mask);
