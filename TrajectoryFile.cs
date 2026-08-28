using System.Globalization;
using System.Text;

namespace Mhodume;

/// <summary>
/// The format the app and the mod meet in.
///
/// Kept apart from ConfigStore so it can be exercised without a dispatcher, a
/// window or a disk: this text is the seam between the two halves of the
/// project, and a mismatch here shows up as a feature that silently does
/// nothing in game.
///
/// Versions, oldest first:
///   V1  points are "x y z speed", speed pre-normalised to the run's own
///       maximum - which made the colours mean something different on every
///       run, so it did not last.
///   V2  the same, speed in game units, scaled by a setting instead.
///   V3  a fifth number on each point: the keys held there.
///   V4  a "C" section after the segments - the camera track, one row per
///       recorded frame, "t x y z yaw pitch". A section of its own because a
///       camera wants the recording's own sampling while the drawn line wants
///       a simplified one, and because a camera row's first four fields would
///       otherwise read as a point.
///   V5  a seventh number on each camera row: the keys held at that frame.
///       The points carry them too, but those are found by nearest position,
///       which is the right question while you are running and the wrong one
///       while a recording is played back - a path that crosses itself makes
///       it lag. During playback the clock is known exactly.
/// </summary>
public static class TrajectoryFile
{
    public const string Version = "V5";

    public static string Render(Trajectory traj)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.Append(Version).Append('\n');
        sb.Append("map ").Append(traj.Map).Append('\n');
        sb.Append("player ").Append(traj.Player).Append('\n');
        sb.Append("time ").Append(traj.TimeMs).Append('\n');

        foreach (var seg in traj.Segments)
        {
            sb.Append("S\n");
            foreach (var p in seg.Points)
            {
                sb.Append(p[0].ToString(inv)).Append(' ')
                  .Append(p[1].ToString(inv)).Append(' ')
                  .Append(p[2].ToString(inv)).Append(' ')
                  .Append(p[3].ToString(inv));
                if (p.Length > 4)
                    sb.Append(' ').Append(((long)p[4]).ToString(inv));
                sb.Append('\n');
            }
        }

        if (traj.Camera.Count > 0)
        {
            sb.Append("C\n");
            foreach (var c in traj.Camera)
            {
                sb.Append(Math.Round(c.Seconds, 2).ToString(inv)).Append(' ')
                  .Append(Math.Round(c.X, 1).ToString(inv)).Append(' ')
                  .Append(Math.Round(c.Y, 1).ToString(inv)).Append(' ')
                  .Append(Math.Round(c.Z, 1).ToString(inv)).Append(' ')
                  .Append(Math.Round(c.Yaw, 1).ToString(inv)).Append(' ')
                  .Append(Math.Round(c.Pitch, 1).ToString(inv)).Append(' ')
                  .Append(c.Inputs.ToString(inv)).Append('\n');
            }
        }

        return sb.ToString();
    }
}
