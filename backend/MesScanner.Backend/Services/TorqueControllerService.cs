using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using MesScanner.Backend.Hubs;
using MesScanner.Backend.Models;

namespace MesScanner.Backend.Services;

public class TorqueControllerService : BackgroundService
{
    private readonly ILogger<TorqueControllerService> _logger;
    private readonly IHubContext<TorqueHub> _hubContext;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly string _ip = "192.168.5.212";
    private readonly int _port = 4545;
    private bool _isConnected = false;
    private DateTime _lastPacketTime = DateTime.MinValue;

    public TorqueControllerService(ILogger<TorqueControllerService> logger, IHubContext<TorqueHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("\n[System] 定扭后端引擎 (带心跳) 已启动...");
        
        // 启动独立的心跳监控任务
        _ = Task.Run(() => HeartbeatLoopAsync(stoppingToken));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_isConnected)
            {
                await ConnectAndHandshakeAsync();
            }
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_isConnected && (DateTime.Now - _lastPacketTime).TotalSeconds > 2)
            {
                await SendPacketAsync("00209999            ");
            }
            await Task.Delay(1000, token);
        }
    }

    private async Task ConnectAndHandshakeAsync()
    {
        try
        {
            Console.WriteLine($"\n[Service] {DateTime.Now:HH:mm:ss} >>> 尝试连接控制器: {_ip}:{_port}");
            _tcpClient = new TcpClient();
            
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                await _tcpClient.ConnectAsync(_ip, _port, cts.Token);
            }

            if (_tcpClient.Connected)
            {
                _stream = _tcpClient.GetStream();
                _isConnected = true;
                Console.WriteLine($"[Service] {DateTime.Now:HH:mm:ss} ✔ 物理成功连上控制器！");
                await LogToFrontend("success", "[System] TCP 链路已连通");

                await Task.Delay(1000); 
                await SendPacketAsync("00200001            "); 
                
                _ = Task.Run(() => ReadLoopAsync());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Service] {DateTime.Now:HH:mm:ss} ✘ 连接异常: {ex.Message}");
            _isConnected = false;
        }
    }

    public async Task SendPacketAsync(string asciiStr)
    {
        try 
        {
            if (!_isConnected || _stream == null) return;
            
            byte[] asciiData = Encoding.ASCII.GetBytes(asciiStr);
            // 动态长度：ASCII 长度 + 1 字节 NULL 终止符
            byte[] fullPacket = new byte[asciiData.Length + 1]; 
            Array.Copy(asciiData, 0, fullPacket, 0, asciiData.Length);
            fullPacket[fullPacket.Length - 1] = 0x00; 

            await _stream.WriteAsync(fullPacket, 0, fullPacket.Length);
            _lastPacketTime = DateTime.Now;

            bool isHeartbeat = asciiStr.Contains("9999");
            if (!isHeartbeat) Console.WriteLine($"[Service] {DateTime.Now:HH:mm:ss} TX >>> {asciiStr}");
            
            await LogToFrontend("info", $"[TX] {asciiStr}", isHeartbeat);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Service] 发送失败: {ex.Message}");
            _isConnected = false;
        }
    }

    private async Task ReadLoopAsync()
    {
        byte[] buffer = new byte[2048];
        try
        {
            while (_isConnected && _tcpClient != null && _tcpClient.Connected)
            {
                int bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string raw = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                
                // 处理马头控制器报文可能连在一起的情况 (Split by NULL)
                var messages = raw.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                foreach (var msg in messages)
                {
                    await ProcessResponse(msg);
                }
            }
        }
        catch { }
        finally { _isConnected = false; }
    }

    private async Task ProcessResponse(string raw)
    {
        string mid = "";
        if (raw.Length >= 8) mid = raw.Substring(4, 4);

        bool isHeartbeat = mid == "9999";
        if (!isHeartbeat) Console.WriteLine($"[Service] {DateTime.Now:HH:mm:ss} RX <<< {raw}");
        
        await LogToFrontend("success", $"[RX] {raw}", isHeartbeat);

        if (mid == "0061")
        {
            try 
            {
                // 标准 Open Protocol 0061 解析 (根据偏移量提取数据)
                // 基于实测 002481 锁定的偏移量与倍率
                string torqueStr = raw.Length >= 147 ? raw.Substring(141, 6) : "000000";
                double torqueVal = double.Parse(torqueStr) / 1000.0;

                string angleStr = raw.Length >= 175 ? raw.Substring(170, 5) : "00000";
                double angleVal = double.Parse(angleStr) / 10.0;

                // 拧紧状态: Offset 105 (1:OK, 0:NOK)
                string statusChar = raw.Length >= 106 ? raw.Substring(105, 1) : "0";
                string statusText = (statusChar == "1") ? "OK" : "NOK";

                await _hubContext.Clients.All.SendAsync("ReceiveData", new TorqueResult {
                    Torque = torqueVal.ToString("F3"), 
                    Angle = angleVal.ToString("F1"),
                    Status = statusText
                });

                Console.WriteLine($"[Service] 实战数据已就绪: {torqueVal:F3}Nm / {angleVal:F1}Deg - {statusText}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Service] 数据解析异常: {ex.Message}");
            }
            finally 
            {
                // 关键点：无论解析是否成功，必须回复 MID 0062 确认收到
                await SendPacketAsync("00200062001         ");
            }
        }
    }

    private async Task LogToFrontend(string level, string msg, bool isHeartbeat = false)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveLog", new LogEntry { 
            Level = level, Msg = msg, IsHeartbeat = isHeartbeat 
        });
    }
}
