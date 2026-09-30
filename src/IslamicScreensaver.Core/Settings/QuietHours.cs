namespace IslamicScreensaver.Core.Settings;

/// <summary>What the screensaver does inside the quiet window.</summary>
public enum QuietHoursBehaviour
{
    /// <summary>Black screen, no recitation. The display still blanks, so the room stays dark.</summary>
    DarkAndSilent,

    /// <summary>Exits the moment Windows launches it, leaving whatever was on screen.</summary>
    DoNotStart,

    /// <summary>Everything as usual, with the sound turned off.</summary>
    Silent,
}

/// <summary>
/// A window of the day the screensaver should keep out of — because being woken at three in the
/// morning by recitation and a lit screen is startling.
///
/// Windows owns the idle timer, so we cannot stop it launching us. What we can do is decide what
/// happens once it has: see <see cref="QuietHoursBehaviour"/>.
/// </summary>
public sealed class QuietHours
{
    /// <summary>Off by default: the screensaver plays normally, with sound, at any hour.</summary>
    public bool Enabled { get; set; }

    public TimeOnly From { get; set; } = new(23, 0);

    public TimeOnly To { get; set; } = new(7, 0);

    public QuietHoursBehaviour Behaviour { get; set; } = QuietHoursBehaviour.DarkAndSilent;

    /// <summary>
    /// Whether <paramref name="now"/> falls inside the window. A window that ends before it starts
    /// wraps over midnight, which is the usual way to write a night: 23:00 to 07:00.
    /// </summary>
    public bool Covers(TimeOnly now)
    {
        if (!Enabled || From == To)
            return false;   // equal ends describe no window at all, never a silent forever

        return From < To
            ? now >= From && now < To
            : now >= From || now < To;
    }

    public bool CoversNow() => Covers(TimeOnly.FromDateTime(DateTime.Now));

    /// <summary>
    /// How long until the window opens, or <c>null</c> when it is open already or switched off.
    /// Used to go quiet part-way through a session that began before bedtime.
    /// </summary>
    public TimeSpan? TimeUntilStart(TimeOnly now)
    {
        if (!Enabled || From == To || Covers(now))
            return null;

        var until = (From.ToTimeSpan() - now.ToTimeSpan()).Ticks >= 0
            ? From.ToTimeSpan() - now.ToTimeSpan()
            : From.ToTimeSpan() - now.ToTimeSpan() + TimeSpan.FromDays(1);
        return until;
    }

    public TimeSpan? TimeUntilStart() => TimeUntilStart(TimeOnly.FromDateTime(DateTime.Now));

    /// <summary>"23:00 – 07:00", for the settings page and the log.</summary>
    public string Describe() => $"{From:HH\\:mm} – {To:HH\\:mm}";
}
