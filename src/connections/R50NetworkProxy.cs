using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace gspro_r10
{
  public class R50NetworkProxy : IDisposable
  {
    private const int BUFFER_SIZE = 8192;
    private readonly string UpstreamHost;
    private readonly int UpstreamPort;
    private readonly int ListenPort;
    private readonly bool LogPayloads;
    private readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
    private readonly TcpListener Listener;
    private Task? AcceptLoopTask;
    private int SessionCounter;
    private bool DisposedValue;

    public R50NetworkProxy(IConfigurationSection configuration)
    {
      UpstreamHost = configuration["upstreamHost"] ?? "127.0.0.1";
      UpstreamPort = int.Parse(configuration["upstreamPort"] ?? "2483");
      ListenPort = int.Parse(configuration["listenPort"] ?? "2483");
      LogPayloads = bool.Parse(configuration["logPayloads"] ?? "true");
      Listener = new TcpListener(IPAddress.Any, ListenPort);
    }

    public void Start()
    {
      Listener.Start();
      R50NetworkLogger.Info($"Listening on port {ListenPort} and forwarding to {UpstreamHost}:{UpstreamPort}");
      AcceptLoopTask = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
      while (!CancellationTokenSource.IsCancellationRequested)
      {
        TcpClient? downstream = null;
        try
        {
          downstream = await Listener.AcceptTcpClientAsync(CancellationTokenSource.Token);
          int sessionId = Interlocked.Increment(ref SessionCounter);
          _ = Task.Run(() => HandleSession(sessionId, downstream));
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (ObjectDisposedException)
        {
          break;
        }
        catch (Exception e)
        {
          downstream?.Dispose();
          R50NetworkLogger.Error($"Accept loop error: {e.Message}");
        }
      }
    }

    private async Task HandleSession(int sessionId, TcpClient downstream)
    {
      using (downstream)
      using (TcpClient upstream = new TcpClient())
      {
        try
        {
          R50NetworkLogger.Info($"Session {sessionId} downstream connected from {downstream.Client.RemoteEndPoint}");
          await upstream.ConnectAsync(UpstreamHost, UpstreamPort, CancellationTokenSource.Token);
          R50NetworkLogger.Info($"Session {sessionId} upstream connected to {UpstreamHost}:{UpstreamPort}");

          using NetworkStream downstreamStream = downstream.GetStream();
          using NetworkStream upstreamStream = upstream.GetStream();

          Task relayToUpstream = Relay(sessionId, "client->r50", downstreamStream, upstreamStream);
          Task relayToClient = Relay(sessionId, "r50->client", upstreamStream, downstreamStream);

          await Task.WhenAny(relayToUpstream, relayToClient);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
          R50NetworkLogger.Error($"Session {sessionId} error: {e.Message}");
        }
        finally
        {
          R50NetworkLogger.Info($"Session {sessionId} disconnected");
        }
      }
    }

    private async Task Relay(int sessionId, string direction, NetworkStream source, NetworkStream destination)
    {
      byte[] buffer = new byte[BUFFER_SIZE];

      while (!CancellationTokenSource.IsCancellationRequested)
      {
        int bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), CancellationTokenSource.Token);
        if (bytesRead == 0)
          break;

        if (LogPayloads)
          R50NetworkLogger.Payload($"S{sessionId} {direction} {FormatPayload(buffer, bytesRead)}", direction == "client->r50");

        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), CancellationTokenSource.Token);
        await destination.FlushAsync(CancellationTokenSource.Token);
      }
    }

    private static string FormatPayload(byte[] buffer, int bytesRead)
    {
      byte[] payload = buffer.Take(bytesRead).ToArray();
      string hex = payload.ToHexString();
      string utf8 = Encoding.UTF8.GetString(payload);
      string printable = new string(utf8.Where(c => !char.IsControl(c) || c == '\r' || c == '\n' || c == '\t').ToArray()).Trim();

      if (printable.Length > 120)
        printable = printable.Substring(0, 120);

      return string.IsNullOrWhiteSpace(printable)
        ? $"{bytesRead} bytes hex={hex}"
        : $"{bytesRead} bytes hex={hex} text={printable}";
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!DisposedValue)
      {
        if (disposing)
        {
          CancellationTokenSource.Cancel();
          Listener.Stop();
          AcceptLoopTask?.Wait(TimeSpan.FromSeconds(2));
          CancellationTokenSource.Dispose();
        }

        DisposedValue = true;
      }
    }

    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }

  public static class R50NetworkLogger
  {
    public static void Info(string message) => BaseLogger.LogMessage(message, "R50-NET", LogMessageType.Informational, ConsoleColor.Cyan);
    public static void Error(string message) => BaseLogger.LogMessage(message, "R50-NET", LogMessageType.Error, ConsoleColor.Cyan);
    public static void Payload(string message, bool outgoing)
    {
      BaseLogger.LogMessage(message, "R50-NET", outgoing ? LogMessageType.Outgoing : LogMessageType.Incoming, ConsoleColor.Cyan);
    }
  }
}
