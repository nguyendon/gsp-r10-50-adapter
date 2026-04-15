using gspro_r10.bluetooth;
using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  public enum LaunchMonitorTransport
  {
    Bluetooth,
    R10E6Server,
    R50NetworkProxy
  }

  public class LaunchMonitorConfiguration
  {
    public GarminLaunchMonitorModel Model { get; init; }
    public LaunchMonitorTransport Transport { get; init; }

    public static LaunchMonitorConfiguration Resolve(IConfigurationRoot configuration)
    {
      IConfigurationSection section = configuration.GetSection("launchMonitor");
      string configuredModel = section["model"] ?? string.Empty;
      string configuredTransport = section["transport"] ?? string.Empty;

      GarminLaunchMonitorModel model = GarminLaunchMonitorSupport.ResolveModel(configuredModel);
      LaunchMonitorTransport? transport = ResolveTransport(configuredTransport);

      if (transport != null)
      {
        return new LaunchMonitorConfiguration()
        {
          Model = model,
          Transport = transport.Value
        };
      }

      if (bool.Parse(configuration.GetSection("r50NetworkProxy")["enabled"] ?? "false"))
      {
        return new LaunchMonitorConfiguration()
        {
          Model = GarminLaunchMonitorModel.R50,
          Transport = LaunchMonitorTransport.R50NetworkProxy
        };
      }

      if (bool.Parse(configuration.GetSection("r10E6Server")["enabled"] ?? "false"))
      {
        return new LaunchMonitorConfiguration()
        {
          Model = GarminLaunchMonitorModel.R10,
          Transport = LaunchMonitorTransport.R10E6Server
        };
      }

      if (bool.Parse(configuration.GetSection("bluetooth")["enabled"] ?? "false"))
      {
        return new LaunchMonitorConfiguration()
        {
          Model = GarminLaunchMonitorSupport.ResolveModel(configuration.GetSection("bluetooth")),
          Transport = LaunchMonitorTransport.Bluetooth
        };
      }

      return new LaunchMonitorConfiguration()
      {
        Model = GarminLaunchMonitorModel.R10,
        Transport = LaunchMonitorTransport.Bluetooth
      };
    }

    private static LaunchMonitorTransport? ResolveTransport(string configuredTransport)
    {
      return configuredTransport.Trim().ToLowerInvariant() switch
      {
        "bluetooth" => LaunchMonitorTransport.Bluetooth,
        "r10_e6_server" => LaunchMonitorTransport.R10E6Server,
        "r50_network_proxy" => LaunchMonitorTransport.R50NetworkProxy,
        _ => null
      };
    }
  }
}
