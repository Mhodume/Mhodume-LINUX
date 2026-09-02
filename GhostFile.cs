using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Mhodume;

/// <summary>A single recorded run found on disk.</summary>
public record GhostInfo(
    string Path,
    string Map,
    string PlayerName,
    double DurationSeconds,
    int TimeMs,
    bool Completed,
    string Category,
    DateTime Recorded)
{
    public string TimeText => TimeSpan.FromMilliseconds(TimeMs).ToString(@"m\:ss\.fff");
    public string Display => $"{PlayerName} — {TimeText}{(Completed ? "" : " (unfinished)")}";

    /// <summary>When the run was recorded, short and relative-feeling.</summary>
    public string DateText => Recorded == default ? "" : Recorded.ToString("dd MMM");
}

/// <summary>One continuous run of points; a teleport starts a new segment.</summary>
public class TrajectorySegment
{
    /// <summary>Each point is x, y, z, speed in game units, then the input mask.</summary>
    public List<double[]> Points { get; } = new();
}

public class Trajectory
{
    public string Map { get; init; } = "";
    public string Player { get; init; } = "";
    public int TimeMs { get; init; }
    public double MaxSpeed { get; init; }
    public List<TrajectorySegment> Segments { get; } = new();

    /// <summary>Every change of input in the run, for the timeline view.</summary>
    public List<InputMoment> Inputs { get; } = new();

    /// <summary>
    /// Where the runner was and where they were looking, at the recording's own
    /// rate. Kept apart from the drawn line: that one is simplified down to the
    /// points that describe its shape, which is the wrong sampling for a camera
    /// - a straight corridor collapses to two points while the view swings all
    /// the way through it.
    /// </summary>
    public List<CameraSample> Camera { get; } = new();

    /// <summary>Checkpoint times the game recorded, in milliseconds.</summary>
    public List<int> CheckpointMs { get; } = new();

    public int PointCount => Segments.Sum(s => s.Points.Count);
}

/// <summary>
/// Reads VHOLUME ghost recordings.
///
/// On-disk format, despite the .json.gz name: a 4-byte little-endian
/// uncompressed length, then a standard gzip stream, then JSON.
///
/// Frames are delta-encoded. A frame carrying "loc"/"rot"/"vel" is an absolute
/// keyframe; every other frame carries "dloc"/"drot"/"dvel" relative to the
/// PREVIOUS frame. That interpretation was verified against the keyframes:
/// accumulating from the previous frame lands within about one frame-step of
/// each keyframe, whereas treating deltas as keyframe-relative is off by two
/// orders of magnitude.
/// </summary>
/// <summary>Where the runner was and looking, one frame of the recording.</summary>
public record CameraSample(double Seconds, double X, double Y, double Z,
                           double Yaw, double Pitch, int Inputs);

/// <summary>
/// One frame of a run, once the deltas have been added back up: where the
/// runner was, how fast, and what they were holding.
/// </summary>
internal readonly record struct Sample(
    double X, double Y, double Z, double Speed, int Inputs, double Time);

/// <summary>
/// A frame as read, before the accumulated values have been pulled back onto
/// the keyframes they miss. A class rather than a record struct because the
/// correction is applied in place, over a list, twice.
/// </summary>
internal class Frame
{
    public double T, X, Y, Z, Yaw, Pitch, Speed;
    public int Mask;

    /// <summary>How far this keyframe was from where the deltas had got to,
    /// or null on a frame that carried no keyframe.</summary>
    public (double X, double Y, double Z)? Miss;
    public (double Yaw, double Pitch)? ViewMiss;
}

public static class GhostFile
{
    /// <summary>
    /// Floor for the teleport cut, for moments too slow for the speed to say
    /// much (standing still, then respawned). Fast movement is judged against the
    /// speed instead — see <see cref="TeleportSpeedMargin"/> — so a high-speed
    /// stretch is never torn apart, which a fixed cut at this value used to do.
    /// </summary>
    private const double TeleportThresholdCm = 600;

    /// <summary>How far past what the speed allows in one frame still counts as
    /// movement, not a teleport. A respawn jumps far beyond this; fast running
    /// does not.</summary>
    private const double TeleportSpeedMargin = 2.5;

    /// <summary>Douglas-Peucker tolerance. Below this, points add nothing visible.</summary>
    private const double SimplifyToleranceCm = 20;

    public static string GhostsRoot => Path.Combine(
        MhodumePaths.LocalAppDataBase,
        "VHOLUME", "Saved", "Ghosts");

    // ------------------------------------------------------------- discovery
    /// <summary>Lists every ghost on disk, newest-best first per map.</summary>
    public static List<GhostInfo> Discover()
    {
        var result = new List<GhostInfo>();
        if (!Directory.Exists(GhostsRoot)) return result;

        foreach (var file in Directory.EnumerateFiles(GhostsRoot, "*.json.gz", SearchOption.AllDirectories))
        {
            try
            {
                var info = ReadHeader(file);
                if (info is not null) result.Add(info);
            }
            catch
            {
                // a corrupt or half-written ghost should not break the listing
            }
        }
        return result;
    }

    /// <summary>Reads just the metadata block, without rebuilding the path.</summary>
    private static GhostInfo? ReadHeader(string path)
    {
        var json = Decompress(path);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("metadata", out var meta)) return null;

        var map = meta.TryGetProperty("map", out var m) ? m.GetString() ?? "?" : "?";
        var player = meta.TryGetProperty("playerName", out var p) ? p.GetString() ?? "?" : "?";
        var timeMs = meta.TryGetProperty("realTimeMs", out var t) ? t.GetInt32() : 0;
        var duration = meta.TryGetProperty("duration", out var d) ? d.GetDouble() : 0;
        var completed = meta.TryGetProperty("completed", out var c) && c.GetBoolean();

        // Ghosts/<Map>/<Category>/file, or Ghosts/Provisional/<Map>/...
        var category = Path.GetFileName(Path.GetDirectoryName(path)) ?? "";
        if (path.Contains(Path.Combine("Ghosts", "Provisional"), StringComparison.OrdinalIgnoreCase))
            category = "Provisional";
        if (path.Contains(Path.Combine("Ghosts", "Downloaded"), StringComparison.OrdinalIgnoreCase))
            category = "Downloaded";

        // The file's own write time is when the run was saved — a real date to
        // sort by, with no timestamp needed in the ghost itself.
        DateTime recorded;
        try { recorded = File.GetLastWriteTime(path); } catch { recorded = default; }

        return new GhostInfo(path, map, player, duration, timeMs, completed, category, recorded);
    }

    private static string Decompress(string path)
    {
        var raw = File.ReadAllBytes(path);
        if (raw.Length < 6) throw new InvalidDataException("ghost file too small");

        // first four bytes: uncompressed length; the gzip stream follows
        using var input = new MemoryStream(raw, 4, raw.Length - 4);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        return reader.ReadToEnd();
    }

    // ----------------------------------------------------------- full load
    public static Trajectory Load(string path)
    {
        var json = Decompress(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var meta = root.GetProperty("metadata");

        var traj = new Trajectory
        {
            Map = meta.TryGetProperty("map", out var m) ? m.GetString() ?? "" : "",
            Player = meta.TryGetProperty("playerName", out var p) ? p.GetString() ?? "" : "",
            TimeMs = meta.TryGetProperty("realTimeMs", out var t) ? t.GetInt32() : 0,
        };

        if (meta.TryGetProperty("cpTimes", out var cps) &&
            cps.ValueKind == JsonValueKind.Array)
        {
            foreach (var cp in cps.EnumerateArray())
                if (cp.TryGetProperty("t", out var cpt))
                    traj.CheckpointMs.Add(cpt.GetInt32());
        }

        // ---- rebuild absolute positions and the view, frame by frame
        //
        // Both are stored as an absolute keyframe every few seconds with deltas
        // in between, and the deltas do not add up to the next keyframe. Each
        // keyframe records how far out the accumulation had got, and Straighten
        // spreads that back over the span afterwards.
        var frames = new List<Frame>();
        double px = 0, py = 0, pz = 0, speed = 0;
        double yaw = 0, pitch = 0;
        int mask = 0;
        bool started = false;

        foreach (var frame in root.GetProperty("frames").EnumerateArray())
        {
            (double, double, double)? miss = null;
            if (frame.TryGetProperty("loc", out var loc))
            {
                var tx = loc.GetProperty("x").GetDouble();
                var ty = loc.GetProperty("y").GetDouble();
                var tz = loc.GetProperty("z").GetDouble();
                // A keyframe frame carries no delta, so the accumulation stands
                // where the previous frame left it - that is what it missed by.
                if (started) miss = (tx - px, ty - py, tz - pz);
                px = tx; py = ty; pz = tz;
                started = true;
            }
            else if (frame.TryGetProperty("dloc", out var dloc))
            {
                if (!started) continue;             // deltas before any keyframe
                var a = dloc.EnumerateArray().ToArray();
                if (a.Length < 3) continue;
                px += a[0].GetDouble();
                py += a[1].GetDouble();
                pz += a[2].GetDouble();
            }
            else continue;

            // The view rides the same scheme, keyframes and all.
            (double, double)? viewMiss = null;
            if (frame.TryGetProperty("rot", out var rot))
            {
                var tp = rot.GetProperty("pitch").GetDouble();
                var ty2 = rot.GetProperty("yaw").GetDouble();
                if (frames.Count > 0) viewMiss = (Wrap(ty2 - yaw), Wrap(tp - pitch));
                pitch = tp; yaw = ty2;
            }
            else if (frame.TryGetProperty("drot", out var drot))
            {
                var a = drot.EnumerateArray().ToArray();
                if (a.Length >= 2)
                {
                    pitch += a[0].GetDouble();
                    yaw += a[1].GetDouble();
                }
            }

            if (frame.TryGetProperty("anim", out var anim) &&
                anim.TryGetProperty("speed", out var s))
                speed = s.GetDouble();

            var at = frame.TryGetProperty("t", out var ft) ? ft.GetDouble() : 0;

            // "inp" is written only when it changes, so it carries forward.
            if (frame.TryGetProperty("inp", out var inp))
            {
                var now = inp.GetInt32() & GhostInputs.Named;
                if (now != mask || traj.Inputs.Count == 0)
                    traj.Inputs.Add(new InputMoment(at, now));
                mask = now;
            }

            frames.Add(new Frame
            {
                T = at, X = px, Y = py, Z = pz, Yaw = yaw, Pitch = pitch,
                Speed = speed, Mask = mask, Miss = miss, ViewMiss = viewMiss,
            });
        }

        if (frames.Count == 0) return traj;

        Straighten(frames);

        foreach (var fr in frames)
            traj.Camera.Add(new CameraSample(fr.T, fr.X, fr.Y, fr.Z,
                                             fr.Yaw, fr.Pitch, fr.Mask));

        var raw = frames
            .Select(fr => new Sample(fr.X, fr.Y, fr.Z, fr.Speed, fr.Mask, fr.T))
            .ToList();

        var maxSpeed = Math.Max(1, raw.Max(r => r.Speed));

        // ---- split on teleports, simplify each piece
        var medianSeconds = EstimateFrameSeconds(raw);   // fallback when a pair has no usable time
        var piece = new List<Sample> { raw[0] };
        var pieces = new List<List<Sample>> { piece };

        for (int i = 1; i < raw.Count; i++)
        {
            var step = Distance(raw[i - 1], raw[i]);
            // A real teleport is a jump the recorded speed cannot cover in the
            // time between these two frames. The time is taken per pair from the
            // frame stamps, so however fast the run - and however the recording
            // is paced - a stretch reads as movement rather than being chopped
            // into pieces the drawing then drops. Only a respawn, far past what
            // the speed allows over that interval, is cut. A fixed cut tore fast
            // sections apart, which is what left them full of holes.
            var dt = raw[i].Time - raw[i - 1].Time;
            if (dt <= 0) dt = medianSeconds;
            var pairSpeed = Math.Max(raw[i].Speed, raw[i - 1].Speed);
            var allowed = Math.Max(TeleportThresholdCm,
                                   pairSpeed * dt * TeleportSpeedMargin);
            if (step > allowed)
            {
                piece = new List<Sample>();
                pieces.Add(piece);
            }
            piece.Add(raw[i]);
        }

        var result = new Trajectory
        {
            Map = traj.Map, Player = traj.Player, TimeMs = traj.TimeMs, MaxSpeed = maxSpeed,
        };
        result.Inputs.AddRange(traj.Inputs);
        result.CheckpointMs.AddRange(traj.CheckpointMs);
        result.Camera.AddRange(traj.Camera);

        foreach (var pc in pieces)
        {
            if (pc.Count < 2) continue;
            var simplified = Simplify(pc, SimplifyToleranceCm);
            var seg = new TrajectorySegment();
            // Speed stays in game units: the mod scales it against a setting, so
            // changing where "top speed" sits does not mean reloading the run.
            foreach (var pt in simplified)
                seg.Points.Add(new[] { Round(pt.X), Round(pt.Y), Round(pt.Z),
                                       Math.Round(pt.Speed, 1), pt.Inputs });
            if (seg.Points.Count >= 2) result.Segments.Add(seg);
        }

        return result;
    }

    /// <summary>Degrees onto -180..180, so a wrap past north is not a spin.</summary>
    private static double Wrap(double degrees)
    {
        var d = (degrees + 180) % 360;
        if (d < 0) d += 360;
        return d - 180;
    }

    /// <summary>
    /// Pulls the accumulated values back onto the keyframes they miss.
    ///
    /// Each keyframe is what was recorded about that moment; everything between
    /// two of them was reached by adding deltas that do not quite agree. The
    /// miss at the closing keyframe is spread linearly back across the span, so
    /// the run meets every keyframe exactly and moves smoothly in between.
    ///
    /// A jump the run really made - a respawn, a teleport - is not drift, and
    /// the split into segments downstream depends on it surviving. A miss past
    /// the teleport threshold is therefore left exactly where it is.
    ///
    /// The tail after the last keyframe has nothing to be corrected against and
    /// is left as it accumulated.
    /// </summary>
    private static void Straighten(List<Frame> frames)
    {
        int from = 0;
        for (int i = 1; i < frames.Count; i++)
        {
            if (frames[i].Miss is not (double mx, double my, double mz)) continue;

            var jumped = Math.Sqrt(mx * mx + my * my + mz * mz) > TeleportThresholdCm;
            if (!jumped)
            {
                // Up to the keyframe, not including it: that frame already
                // holds the recorded value, so correcting it would move it off.
                var span = i - from;
                for (int j = from + 1; j < i; j++)
                {
                    var w = (double)(j - from) / span;
                    frames[j].X += mx * w;
                    frames[j].Y += my * w;
                    frames[j].Z += mz * w;
                }
            }
            from = i;
        }

        from = 0;
        for (int i = 1; i < frames.Count; i++)
        {
            if (frames[i].ViewMiss is not (double dy, double dp)) continue;

            var span = i - from;
            for (int j = from + 1; j < i; j++)
            {
                var w = (double)(j - from) / span;
                frames[j].Yaw += dy * w;
                frames[j].Pitch += dp * w;
            }
            from = i;
        }
    }

    private static double Round(double v) => Math.Round(v, 1);

    /// <summary>
    /// The run's frame time in seconds, taken as the median of step/speed across
    /// the samples. That ratio is the time a frame took wherever the runner was
    /// actually moving; a teleport is a huge outlier the median ignores. Used to
    /// work out how far the speed could carry the runner in one frame.
    /// </summary>
    private static double EstimateFrameSeconds(List<Sample> raw)
    {
        var dts = new List<double>();
        for (int i = 1; i < raw.Count; i++)
        {
            var speed = raw[i].Speed;
            if (speed < 1) continue;                 // too slow to time a frame by
            dts.Add(Distance(raw[i - 1], raw[i]) / speed);
        }
        if (dts.Count == 0) return 1.0 / 60;         // nothing to go on; assume 60 Hz
        dts.Sort();
        return dts[dts.Count / 2];
    }

    private static double Distance(
        Sample a,
        Sample b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // ------------------------------------------------------------- simplify
    /// <summary>
    /// Douglas-Peucker in 3D, iterative to avoid deep recursion on long runs.
    /// Keeps the shape of the path while dropping points that sit on a line.
    /// </summary>
    private static List<Sample> Simplify(
        List<Sample> pts, double tolerance)
    {
        if (pts.Count < 3) return pts;

        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;

        var stack = new Stack<(int First, int Last)>();
        stack.Push((0, pts.Count - 1));

        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            if (last <= first + 1) continue;

            double worst = 0;
            int index = -1;
            for (int i = first + 1; i < last; i++)
            {
                var d = PerpendicularDistance(pts[i], pts[first], pts[last]);
                if (d > worst) { worst = d; index = i; }
            }

            if (worst > tolerance && index > 0)
            {
                keep[index] = true;
                stack.Push((first, index));
                stack.Push((index, last));
            }
        }

        var result = new List<Sample>();
        for (int i = 0; i < pts.Count; i++)
            if (keep[i]) result.Add(pts[i]);
        return result;
    }

    private static double PerpendicularDistance(
        Sample p,
        Sample a,
        Sample b)
    {
        double abx = b.X - a.X, aby = b.Y - a.Y, abz = b.Z - a.Z;
        double apx = p.X - a.X, apy = p.Y - a.Y, apz = p.Z - a.Z;

        double abLenSq = abx * abx + aby * aby + abz * abz;
        if (abLenSq < 1e-9) return Math.Sqrt(apx * apx + apy * apy + apz * apz);

        double t = Math.Clamp((apx * abx + apy * aby + apz * abz) / abLenSq, 0, 1);
        double cx = a.X + abx * t - p.X;
        double cy = a.Y + aby * t - p.Y;
        double cz = a.Z + abz * t - p.Z;
        return Math.Sqrt(cx * cx + cy * cy + cz * cz);
    }
}
