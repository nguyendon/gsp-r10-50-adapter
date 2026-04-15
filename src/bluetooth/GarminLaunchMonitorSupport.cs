using Microsoft.Extensions.Configuration;

namespace gspro_r10.bluetooth
{
  public enum GarminLaunchMonitorModel
  {
    R10,
    R50
  }

  public static class GarminLaunchMonitorSupport
  {
    public static GarminLaunchMonitorModel ResolveModel(IConfigurationSection configuration)
    {
      return (configuration["deviceType"] ?? string.Empty).Trim().ToLowerInvariant() switch
      {
        "r50" => GarminLaunchMonitorModel.R50,
        _ => GarminLaunchMonitorModel.R10
      };
    }

    public static string GetDefaultBluetoothDeviceName(GarminLaunchMonitorModel model)
    {
      return model switch
      {
        GarminLaunchMonitorModel.R50 => "Approach R50",
        _ => "Approach R10"
      };
    }

    public static string GetDisplayName(GarminLaunchMonitorModel model)
    {
      return model switch
      {
        GarminLaunchMonitorModel.R50 => "Approach R50",
        _ => "Approach R10"
      };
    }

    public static string GetOpenConnectDeviceId(GarminLaunchMonitorModel model)
    {
      return model switch
      {
        GarminLaunchMonitorModel.R50 => "GSPRO-R50",
        _ => "GSPRO-R10"
      };
    }

    public static string GetBluetoothLogComponent(GarminLaunchMonitorModel model)
    {
      return model switch
      {
        GarminLaunchMonitorModel.R50 => "R50-BT",
        _ => "R10-BT"
      };
    }
  }
}
