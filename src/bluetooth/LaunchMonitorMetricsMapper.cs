using gspro_r10.OpenConnect;
using LaunchMonitor.Proto;

namespace gspro_r10.bluetooth
{
  public static class LaunchMonitorMetricsMapper
  {
    private static readonly double METERS_PER_S_TO_MILES_PER_HOUR = 2.2369;

    public static BallData? BallDataFromMetrics(BallMetrics? ballMetrics)
    {
      if (ballMetrics == null) return null;

      double? spinAxis = ballMetrics.HasSpinAxis ? ballMetrics.SpinAxis * -1 : null;
      double? totalSpin = ballMetrics.HasTotalSpin ? ballMetrics.TotalSpin : null;

      return new BallData()
      {
        HLA = ballMetrics.HasLaunchDirection ? ballMetrics.LaunchDirection : null,
        VLA = ballMetrics.HasLaunchAngle ? ballMetrics.LaunchAngle : null,
        Speed = ballMetrics.HasBallSpeed ? ballMetrics.BallSpeed * METERS_PER_S_TO_MILES_PER_HOUR : null,
        SpinAxis = spinAxis,
        TotalSpin = totalSpin,
        SideSpin = (totalSpin != null && spinAxis != null)
          ? totalSpin * Math.Sin(spinAxis.Value * Math.PI / 180)
          : null,
        BackSpin = (totalSpin != null && spinAxis != null)
          ? totalSpin * Math.Cos(spinAxis.Value * Math.PI / 180)
          : null
      };
    }

    public static ClubData? ClubDataFromMetrics(ClubMetrics? clubMetrics)
    {
      if (clubMetrics == null) return null;

      double? clubSpeed = clubMetrics.HasClubHeadSpeed
        ? clubMetrics.ClubHeadSpeed * METERS_PER_S_TO_MILES_PER_HOUR
        : null;

      return new ClubData()
      {
        Speed = clubSpeed,
        SpeedAtImpact = clubSpeed,
        AngleOfAttack = clubMetrics.HasAttackAngle ? clubMetrics.AttackAngle : null,
        FaceToTarget = clubMetrics.HasClubAngleFace ? clubMetrics.ClubAngleFace : null,
        Path = clubMetrics.HasClubAnglePath ? clubMetrics.ClubAnglePath : null
      };
    }
  }
}
