using System.Text.Json;
using System.Text.Json.Serialization;
using gspro_r10.bluetooth;
using gspro_r10.OpenConnect;
using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  public class ConnectionManager: IDisposable
  {
    private R10ConnectionServer? R10Server;
    private OpenConnectClient OpenConnectClient;
    private BluetoothConnection? BluetoothConnection { get; }
    private R50NetworkProxy? GarminR50NetworkProxy { get; }
    internal HttpPuttingServer? PuttingConnection { get; }
    public event ClubChangedEventHandler? ClubChanged;
    public delegate void ClubChangedEventHandler(object sender, ClubChangedEventArgs e);
    public class ClubChangedEventArgs: EventArgs
    {
      public Club Club { get; set; }
    }

    private JsonSerializerOptions serializerSettings = new JsonSerializerOptions()
    {
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string OpenConnectDeviceId;
    private int shotNumber = 0;
    private bool disposedValue;

    public ConnectionManager(IConfigurationRoot configuration)
    {
      LaunchMonitorConfiguration launchMonitorConfiguration = LaunchMonitorConfiguration.Resolve(configuration);
      IConfigurationSection bluetoothConfiguration = configuration.GetSection("bluetooth");
      string configuredDeviceId = configuration.GetSection("openConnect")["deviceId"] ?? string.Empty;
      OpenConnectDeviceId = string.IsNullOrWhiteSpace(configuredDeviceId)
        ? GarminLaunchMonitorSupport.GetOpenConnectDeviceId(launchMonitorConfiguration.Model)
        : configuredDeviceId;
      OpenConnectClient = new OpenConnectClient(this, configuration.GetSection("openConnect"), OpenConnectDeviceId);
      OpenConnectClient.ConnectAsync();

      if (launchMonitorConfiguration.Transport == LaunchMonitorTransport.R10E6Server)
      {
        R10Server = new R10ConnectionServer(this, configuration.GetSection("r10E6Server"));
        R10Server.Start();
      }

      if (launchMonitorConfiguration.Transport == LaunchMonitorTransport.Bluetooth)
      {
        bluetoothConfiguration["deviceType"] = launchMonitorConfiguration.Model == GarminLaunchMonitorModel.R50 ? "r50" : "r10";
        BluetoothConnection = new BluetoothConnection(this, bluetoothConfiguration);
      }

      if (launchMonitorConfiguration.Transport == LaunchMonitorTransport.R50NetworkProxy)
      {
        GarminR50NetworkProxy = new R50NetworkProxy(configuration.GetSection("r50NetworkProxy"));
        GarminR50NetworkProxy.Start();
      }

      if (bool.Parse(configuration.GetSection("putting")["enabled"] ?? "false"))
      {
        PuttingConnection = new HttpPuttingServer(this, configuration.GetSection("putting"));
        PuttingConnection.Start();
      }
    }

    internal void SendShot(OpenConnect.BallData? ballData, OpenConnect.ClubData? clubData)
    {
      string openConnectMessage = JsonSerializer.Serialize(OpenConnectApiMessage.CreateShotData(
        OpenConnectDeviceId,
        shotNumber++,
        ballData,
        clubData
      ), serializerSettings);

      OpenConnectClient.SendAsync(openConnectMessage);
    }

    public void ClubUpdate(Club club)
    {
      Task.Run(() => {
        ClubChanged?.Invoke(this, new ClubChangedEventArgs()
        {
          Club = club
        });
      });

    }

    internal void SendLaunchMonitorReadyUpdate(bool deviceReady)
    {
      OpenConnectClient.SetDeviceReady(deviceReady);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          R10Server?.Dispose();
          PuttingConnection?.Dispose();
          BluetoothConnection?.Dispose();
          GarminR50NetworkProxy?.Dispose();
          OpenConnectClient?.DisconnectAndStop();
          OpenConnectClient?.Dispose();
        }
        disposedValue = true;
      }
    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}
