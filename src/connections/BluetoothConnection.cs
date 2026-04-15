using gspro_r10.OpenConnect;
using InTheHand.Bluetooth;
using LaunchMonitor.Proto;
using gspro_r10.bluetooth;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace gspro_r10
{
  public class BluetoothConnection : IDisposable
  {
    private static readonly double METERS_PER_S_TO_MILES_PER_HOUR = 2.2369;
    private static readonly float FEET_TO_METERS = 1 / 3.281f;
    private bool disposedValue;

    public ConnectionManager ConnectionManager { get; }
    public IConfigurationSection Configuration { get; }
    public GarminLaunchMonitorModel DeviceModel { get; }
    public int ReconnectInterval { get; }
    public LaunchMonitorDevice? LaunchMonitor { get; private set; }
    public BluetoothDevice? Device { get; private set; }

    public BluetoothConnection(ConnectionManager connectionManager, IConfigurationSection configuration)
    {
      ConnectionManager = connectionManager;
      Configuration = configuration;
      DeviceModel = GarminLaunchMonitorSupport.ResolveModel(configuration);
      BluetoothLogger.SetDeviceModel(DeviceModel);
      ReconnectInterval = int.Parse(configuration["reconnectInterval"] ?? "5");
      Task.Run(ConnectToDevice);

    }

    private void ConnectToDevice()
    {
      string deviceName = Configuration["bluetoothDeviceName"] ?? string.Empty;
      if (string.IsNullOrWhiteSpace(deviceName))
        deviceName = GarminLaunchMonitorSupport.GetDefaultBluetoothDeviceName(DeviceModel);
      Device = FindDevice(deviceName);
      if (Device == null)
      {
        BluetoothLogger.Error($"Could not find '{deviceName}' in list of paired devices.");
        BluetoothLogger.Error("Device must be paired through computer bluetooth settings before running");
        BluetoothLogger.Error("If the device is paired, make sure bluetooth.deviceType and bluetooth.bluetoothDeviceName match settings.json");
        return;
      }

      do
      {
        BluetoothLogger.Info($"Connecting to {Device.Name}: {Device.Id}");
        Device.Gatt.ConnectAsync().Wait();

        if (!Device.Gatt.IsConnected)
        {
          BluetoothLogger.Info($"Could not connect to bluetooth device. Waiting {ReconnectInterval} seconds before trying again");
          Thread.Sleep(TimeSpan.FromSeconds(ReconnectInterval));
        }
      }
      while (!Device.Gatt.IsConnected);

      Device.Gatt.AutoConnect = true;

      BluetoothLogger.Info($"Connected to {GarminLaunchMonitorSupport.GetDisplayName(DeviceModel)}");
      LaunchMonitor = SetupLaunchMonitor(Device);
      Device.GattServerDisconnected += OnDeviceDisconnected;
    }

    private void OnDeviceDisconnected(object? sender, EventArgs args)
    {
      BluetoothLogger.Error("Lost bluetooth connection");
      if (Device != null)
        Device.GattServerDisconnected -= OnDeviceDisconnected;
      LaunchMonitor?.Dispose();

      Task.Run(ConnectToDevice);
    }

    private LaunchMonitorDevice? SetupLaunchMonitor(BluetoothDevice device)
    {
      LaunchMonitorDevice lm = new LaunchMonitorDevice(device);
      lm.AutoWake = bool.Parse(Configuration["autoWake"] ?? "false");
      lm.CalibrateTiltOnConnect = bool.Parse(Configuration["calibrateTiltOnConnect"] ?? "false");

      lm.DebugLogging = bool.Parse(Configuration["debugLogging"] ?? "false");

      lm.MessageRecieved += (o, e) => BluetoothLogger.Incoming(e.Message?.ToString() ?? string.Empty);
      lm.MessageSent += (o, e) => BluetoothLogger.Outgoing(e.Message?.ToString() ?? string.Empty);
      lm.BatteryLifeUpdated += (o, e) => BluetoothLogger.Info($"Battery Life Updated: {e.Battery}%");
      lm.Error += (o, e) => BluetoothLogger.Error($"{e.Severity}: {e.Message}");

      if (bool.Parse(Configuration["sendStatusChangesToGSP"] ?? "false"))
      {
        lm.ReadinessChanged += (o, e) =>
        {
          ConnectionManager.SendLaunchMonitorReadyUpdate(e.Ready);
        };
      }

      lm.ShotMetrics += (o, e) =>
      {
        LogMetrics(e.Metrics);
        ConnectionManager.SendShot(
          LaunchMonitorMetricsMapper.BallDataFromMetrics(e.Metrics?.BallMetrics),
          LaunchMonitorMetricsMapper.ClubDataFromMetrics(e.Metrics?.ClubMetrics)
        );
      };

      if (!lm.Setup())
      {
        BluetoothLogger.Error("Failed Device Setup");
        return null;
      }

      float temperature = float.Parse(Configuration["temperature"] ?? "60");
      float humidity = float.Parse(Configuration["humidity"] ?? "1");
      float altitude = float.Parse(Configuration["altitude"] ?? "0");
      float airDensity = float.Parse(Configuration["airDensity"] ?? "1");
      float teeDistanceInFeet = float.Parse(Configuration["teeDistanceInFeet"] ?? "7");
      float teeRange = teeDistanceInFeet * FEET_TO_METERS;

      lm.ShotConfig(temperature, humidity, altitude, airDensity, teeRange);

      BluetoothLogger.Info($"Device Setup Complete: ");
      BluetoothLogger.Info($"   Model: {lm.Model}");
      BluetoothLogger.Info($"   Firmware: {lm.Firmware}");
      BluetoothLogger.Info($"   Bluetooth ID: {lm.Device.Id}");
      BluetoothLogger.Info($"   Battery: {lm.Battery}%");
      BluetoothLogger.Info($"   Current State: {lm.CurrentState}");
      BluetoothLogger.Info($"   Tilt: {lm.DeviceTilt}");

      return lm;
    }

    private BluetoothDevice? FindDevice(string deviceName)
    {
      foreach (BluetoothDevice pairedDev in Bluetooth.GetPairedDevicesAsync().Result)
        if (pairedDev.Name == deviceName)
          return pairedDev;
      return null;
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          if (Device != null)
            Device.GattServerDisconnected -= OnDeviceDisconnected;
          LaunchMonitor?.Dispose();
        }

        disposedValue = true;
      }
    }

    public void LogMetrics(Metrics? metrics)
    {
      if (metrics == null)
      {
        return;
      }
      try
      {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"===== Shot {metrics.ShotId} =====");
        sb.AppendLine($"{"Ball Metrics",-40}│ {"Club Metrics",-40}│ {"Swing Metrics",-40}");
        sb.AppendLine($"{new string('─', 40)}┼─{new string('─', 40)}┼─{new string('─', 40)}");
        sb.Append($" {"BallSpeed:",-15} {metrics.BallMetrics?.BallSpeed * METERS_PER_S_TO_MILES_PER_HOUR,-22} │");
        sb.Append($" {"Club Speed:",-20} {metrics.ClubMetrics?.ClubHeadSpeed * METERS_PER_S_TO_MILES_PER_HOUR,-18} │");
        sb.AppendLine($" {"Backswing Start:",-20} {metrics.SwingMetrics?.BackSwingStartTime,-17}");

        sb.Append($" {"VLA:",-15} {metrics.BallMetrics?.LaunchAngle,-22} │");
        sb.Append($" {"Club Path:",-20} {metrics.ClubMetrics?.ClubAnglePath,-18} │");
        sb.AppendLine($" {"Downswing Start:",-20} {metrics.SwingMetrics?.DownSwingStartTime,-17}");

        sb.Append($" {"HLA:",-15} {metrics.BallMetrics?.LaunchDirection,-22} │");
        sb.Append($" {"Club Face:",-20} {metrics.ClubMetrics?.ClubAngleFace,-18} │");
        sb.AppendLine($" {"Impact time:",-20} {metrics.SwingMetrics?.ImpactTime,-17}");

        uint? backswingDuration = metrics.SwingMetrics?.DownSwingStartTime - metrics.SwingMetrics?.BackSwingStartTime;
        sb.Append($" {"Spin Axis:",-15} {metrics.BallMetrics?.SpinAxis * -1,-22} │");
        sb.Append($" {"Attack Angle:",-20} {metrics.ClubMetrics?.AttackAngle,-18} │");
        sb.AppendLine($" {"Backswing duration:",-20} {backswingDuration,-17}");

        uint? downswingDuration = metrics.SwingMetrics?.ImpactTime - metrics.SwingMetrics?.DownSwingStartTime;
        sb.Append($" {"Total Spin:",-15} {metrics.BallMetrics?.TotalSpin,-22} │");
        sb.Append($" {"",-20} {"",-18} │");
        sb.AppendLine($" {"Downswing duration:",-20} {downswingDuration,-17}");

        sb.Append($" {"Ball Type:",-15} {metrics.BallMetrics?.GolfBallType,-22} │");
        sb.Append($" {"",-20} {"",-18} │");
        sb.AppendLine($" {"Tempo:",-20} {(float)(backswingDuration ?? 0) / downswingDuration,-17}");

        sb.Append($" {"Spin Calc:",-15} {metrics.BallMetrics?.SpinCalculationType,-22} │");
        sb.Append($" {"",-20} {"",-18} │");
        sb.AppendLine($" {"Normal/Practice:",-20} {metrics.ShotType,-17}");
        BluetoothLogger.Info(sb.ToString());

      }
      catch (Exception e)
      {
        Console.WriteLine(e);
      }

    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }

  public static class BluetoothLogger
  {
    private static string ComponentName = "R10-BT";

    public static void SetDeviceModel(GarminLaunchMonitorModel deviceModel)
    {
      ComponentName = GarminLaunchMonitorSupport.GetBluetoothLogComponent(deviceModel);
    }

    public static void Info(string message) => LogBluetoothMessage(message, LogMessageType.Informational);
    public static void Error(string message) => LogBluetoothMessage(message, LogMessageType.Error);
    public static void Outgoing(string message) => LogBluetoothMessage(message, LogMessageType.Outgoing);
    public static void Incoming(string message) => LogBluetoothMessage(message, LogMessageType.Incoming);
    public static void LogBluetoothMessage(string message, LogMessageType type) => BaseLogger.LogMessage(message, ComponentName, type, ConsoleColor.Magenta);
  }
}
