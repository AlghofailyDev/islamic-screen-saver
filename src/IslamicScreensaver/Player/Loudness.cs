using IslamicScreensaver.Core;
using NAudio.Wave;

namespace IslamicScreensaver.Player;

/// <summary>
/// Measures how loud a recording actually sounds, so audio from different sources can be matched.
/// The isti'ādhah comes from a different master than Quran.com's recitations, and without this it
/// would jump in volume against them.
/// </summary>
internal static class Loudness
{
    /// <summary>Loud parts are what the ear judges by, so quiet passages are excluded from the average.</summary>
    private const float SilenceFloor = 0.02f;

    /// <summary>Root-mean-square level of the audible part of a recording, or null if it cannot be read.</summary>
    public static double? MeasureRms(string path, TimeSpan start, TimeSpan? limit)
    {
        try
        {
            using var reader = new MediaFoundationReader(path);
            if (start > TimeSpan.Zero && start < reader.TotalTime)
                reader.CurrentTime = start;

            var samples = reader.ToSampleProvider();
            var maxSamples = limit is { } span
                ? (long)(span.TotalSeconds * samples.WaveFormat.SampleRate * samples.WaveFormat.Channels)
                : long.MaxValue;

            var buffer = new float[1 << 14];
            double sum = 0;
            long counted = 0;
            long read;
            long total = 0;

            while (total < maxSamples && (read = samples.Read(buffer.AsSpan())) > 0)
            {
                for (var i = 0; i < read; i++)
                {
                    var value = buffer[i];
                    if (Math.Abs(value) < SilenceFloor)
                        continue;
                    sum += (double)value * value;
                    counted++;
                }
                total += read;
            }

            return counted > 0 ? Math.Sqrt(sum / counted) : null;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not measure the loudness of {path}.", ex);
            return null;
        }
    }

    /// <summary>
    /// How much to scale <paramref name="path"/> so it sits at the same level as the reference recording.
    /// Clamped so a bad measurement can never make the prelude deafening or inaudible.
    /// </summary>
    public static float MatchGain(string path, string referencePath, TimeSpan referenceStart)
    {
        var sample = MeasureRms(path, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        var reference = MeasureRms(referencePath, referenceStart, TimeSpan.FromSeconds(30));

        if (sample is not > 0 || reference is not > 0)
            return 1f;

        var gain = (float)Math.Clamp(reference.Value / sample.Value, 0.15, 6.0);
        Log.Info($"{System.IO.Path.GetFileName(path)} level {sample:F4} vs recitation {reference:F4}: gain {gain:F2}x.");
        return gain;
    }
}
