using S7.Net;
using Microsoft.AspNetCore.SignalR;
using MesScanner.Backend.Hubs;
using MesScanner.Backend.Models;
using System.Text.Json;

namespace MesScanner.Backend.Services;

/// <summary>
/// 西门子 PLC 通讯服务 (S7 协议)
/// </summary>
public class PlcService : BackgroundService
{
    private readonly ILogger<PlcService> _logger;
    private readonly IHubContext<TorqueHub> _hubContext;
    private Plc? _plc;
    private bool _isConnected = false;
    private string _ip = "192.168.0.1"; // 默认 PLC IP
    private CpuType _cpu = CpuType.S71200;
    private short _rack = 0;
    private short _slot = 1;
    private string _heartbeatAddress = "";
    private string _aStackFinishAddress = ""; // 新增：A面堆叠完成
    private string _bStackFinishAddress = ""; // 新增：B面堆叠完成
    private string _cellLayerAddress = "";    // 新增：电芯层数点位
    private string _moduleSnAddress = "";     // 新增：堆叠模组序号点位
    private string _col1StartAddr = "";       // 新增：1列起始
    private string _col2StartAddr = "";       // 新增：2列起始
    private string _col3StartAddr = "";       // 新增：3列起始
    private int _aStackDbNum = 1590;          // 新增：A面数据块编号
    private int _bStackDbNum = 1591;          // 新增：B面数据块编号
    private string _plcOkAddr = "DB1590.DBX170.0"; // 新增：OK信号
    private string _plcNgAddr = "DB1590.DBX170.1"; // 新增：NG信号
    private int _currentActiveDbNum = 0;           // 新增：当前活跃的 A/B 面 DB 号

    public PlcService(ILogger<PlcService> logger, IHubContext<TorqueHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public bool IsConnected => _isConnected;

    public void SetConnection(string ip, string cpuType, short rack, short slot, string heartbeatAddress, 
        string aStackAddr = "", string bStackAddr = "", string cellLayerAddr = "", string moduleSnAddr = "",
        string col1 = "", string col2 = "", string col3 = "",
        int aDb = 1590, int bDb = 1591,
        string okAddr = "DB1590.DBX170.0", string ngAddr = "DB1590.DBX170.1")
    {
        _ip = ip;
        _cpu = cpuType == "S71500" ? CpuType.S71500 : (cpuType == "S71200" ? CpuType.S71200 : CpuType.S7300);
        _rack = rack;
        _slot = slot;
        _heartbeatAddress = heartbeatAddress;
        _aStackFinishAddress = aStackAddr;
        _bStackFinishAddress = bStackAddr;
        _cellLayerAddress = cellLayerAddr;
        _moduleSnAddress = moduleSnAddr;
        _col1StartAddr = col1;
        _col2StartAddr = col2;
        _col3StartAddr = col3;
        _aStackDbNum = aDb;
        _bStackDbNum = bDb;
        _plcOkAddr = okAddr;
        _plcNgAddr = ngAddr;
        
        Console.WriteLine($"[PLC Config] A={_aStackFinishAddress}, B={_bStackFinishAddress}, OK={_plcOkAddr}, NG={_plcNgAddr}");
        
        if (Enum.TryParse<CpuType>(cpuType, true, out var cpu))
        {
            _cpu = cpu;
        }
        
        // 重新连接逻辑
        _isConnected = false;
        _plc?.Close();
        _plc = new Plc(_cpu, _ip, _rack, _slot);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动独立的心跳轮询任务
        _ = Task.Run(() => HeartbeatLoopAsync(stoppingToken), stoppingToken);
        // 启动触发信号轮询 (200ms)
        _ = Task.Run(() => TriggerLoopAsync(stoppingToken), stoppingToken);

        LoadConfigFromFile();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_isConnected || _plc == null || !_plc.IsConnected)
            {
                await ConnectAsync();
            }
            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        _logger.LogInformation("[Heartbeat] 心跳轮询任务已启动");
        while (!token.IsCancellationRequested)
        {
            try
            {
                // 只要 PLC 对象存在且地址不为空就尝试读取
                if (_plc != null && !string.IsNullOrEmpty(_heartbeatAddress))
                {
                    // 即使主标志位是 false，只要 PLC 实例底层显示已连接，我们就尝试读取
                    var val = await ReadValueAsync(_heartbeatAddress);
                    bool currentStatus = (val != null);
                    
                    // 同步更新主连接状态
                    _isConnected = currentStatus;

                    // 1. 发送心跳包 (带上真实的位值)
                    await _hubContext.Clients.All.SendAsync("ReceiveHeartbeat", new { 
                        online = currentStatus, 
                        time = DateTime.Now.ToString("HH:mm:ss"),
                        bitValue = val is bool b ? b : (val != null) // 如果是布尔值就取布尔，否则只要不为空就是 true
                    }, token);

                    // 2. 发送到专用监控通道
                    await LogToMonitor("HEARTBEAT", _heartbeatAddress, currentStatus ? "OK" : "TIMEOUT", currentStatus ? "SUCCESS" : "ERROR");
                }
                else
                {
                    await _hubContext.Clients.All.SendAsync("ReceiveHeartbeat", new { online = false }, token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Heartbeat] ✘ 循环异常: {ex.Message}");
            }

            await Task.Delay(2000, token); // 心跳改为 2 秒一次
        }
    }

    private async Task ConnectAsync()
    {
        try
        {
            if (_plc == null) _plc = new Plc(_cpu, _ip, _rack, _slot);
            
            await Task.Run(() => _plc.Open());
            _isConnected = _plc.IsConnected;

            if (_isConnected)
            {
                Console.WriteLine($"\n[PLC Connection] ✔ SUCCESS: Connected to {_ip}");
                _logger.LogInformation("✔ 成功连接到 PLC: {IP}", _ip);
                await LogToFrontend("success", $"[PLC] 成功连接至 {_ip}");
            }
        }
        catch (Exception ex)
        {
            _isConnected = false;
            Console.WriteLine($"\n[PLC Connection] ✘ FAILED: {ex.Message}");
            _logger.LogError("✘ PLC 连接失败: {Msg}", ex.Message);
            await LogToFrontend("error", $"[PLC] 连接失败: {ex.Message}");
        }
    }

    private string GetSideAgnosticAddress(string address, int targetDbNum)
    {
        if (targetDbNum <= 0 || string.IsNullOrEmpty(address)) return address;

        // 1. 如果包含点，如 "DB1.DBB1" 或 "DB1.DBX0.0"，替换第一段的 DB 号
        if (address.Contains('.'))
        {
            var parts = address.Split('.');
            if (parts[0].ToUpper().StartsWith("DB"))
            {
                // 重新拼接后面的所有部分，例如 DB1.DBX0.0 -> DB1590.DBX0.0
                return $"DB{targetDbNum}.{string.Join(".", parts.Skip(1))}";
            }
            return address; // 非 DB 块地址 (如 M0.0) 不处理
        }
        
        // 2. 如果不包含点且以 DB 开头，则认为是偏移量格式，如 "DBB1" -> "DB1590.DBB1"
        if (address.ToUpper().StartsWith("DB"))
        {
            return $"DB{targetDbNum}.{address}";
        }

        return address;
    }

        public async Task<object?> ReadValueAsync(string address, int count = 1)
        {
            if (string.IsNullOrEmpty(address)) return null;
            string cleanAddr = address.Replace(" ", "").Trim().ToUpper();
            
            if (_plc == null || !_isConnected) {
                _logger.LogWarning("[PLC Read] 无法读取，PLC 未连接。地址: {Addr}", cleanAddr);
                return null;
            }

            try
            {
                object? result;
                if (count > 1)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(cleanAddr, @"DB(\d+)\D+(\d+)");
                    if (match.Success)
                    {
                        int dbNum = int.Parse(match.Groups[1].Value);
                        int start = int.Parse(match.Groups[2].Value);
                        result = await Task.Run(() => _plc.ReadBytes(DataType.DataBlock, dbNum, start, count));
                    }
                    else
                    {
                        result = await Task.Run(() => _plc.Read(cleanAddr));
                    }
                }
                else
                {
                    result = await Task.Run(() => _plc.Read(cleanAddr));
                }

                string valStr = result is byte[] bytes ? BitConverter.ToString(bytes) : (result?.ToString() ?? "NULL");
                await LogToMonitor("READ", cleanAddr + (count > 1 ? $"[{count}]" : ""), valStr, "SUCCESS");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PLC Read Error] Addr: {Addr}, Msg: {Msg}", cleanAddr, ex.Message);
                await LogToFrontend("error", $"[PLC读取异常] 地址: {cleanAddr}, 原因: {ex.Message}");
                return null;
            }
        }

    public async Task<List<string>> ReadCellBarcodesAsync(int layers, int barcodeLength, string col1Addr, string col2Addr, string col3Addr, int overrideDbNum = 0)
    {
        var allBarcodes = new List<string>();
        if (_plc == null || layers <= 0) return allBarcodes;

        // 如果指定了 DB 号，则强制替换
        string c1 = GetSideAgnosticAddress(col1Addr, overrideDbNum);
        string c2 = GetSideAgnosticAddress(col2Addr, overrideDbNum);
        string c3 = GetSideAgnosticAddress(col3Addr, overrideDbNum);

        string[] startAddrs = { c1, c2, c3 };

        try
        {
            foreach (var addr in startAddrs)
            {
                if (string.IsNullOrEmpty(addr)) continue;

                // 解析地址，例如 DB1590.DBB228
                var parts = addr.Split('.');
                int dbNum = int.Parse(parts[0].ToUpper().Replace("DB", "").Trim());
                // 支持 DBB, DBW, DBD, DBX 等多种格式，统一切掉非数字前缀
                string offsetStr = System.Text.RegularExpressions.Regex.Replace(parts[1], @"[^\d]", "");
                int start = int.Parse(offsetStr);
                int totalBytesToRead = layers * 40;

                // 一次性读取该列所有层的原始数据
                Console.WriteLine($"[Batch Read] DB: {dbNum}, Start: {start}, Bytes: {totalBytesToRead}, Addr: {addr}");
                byte[] rawData = await Task.Run(() => _plc.ReadBytes(DataType.DataBlock, dbNum, start, totalBytesToRead));

                if (rawData != null)
                {
                    for (int i = 0; i < layers; i++)
                    {
                        // 每 40 字节为一节，跳过前 2 字节的西门子 String 头部 (最大长度和当前长度)
                        byte[] cellBytes = rawData.Skip(i * 40 + 2).Take(barcodeLength).ToArray();
                        // 过滤非打印字符并修剪
                        string code = System.Text.Encoding.ASCII.GetString(cellBytes).Trim('\0', ' ', '\r', '\n');
                        allBarcodes.Add(code);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Batch Read Barcodes] ✘ Error: {ex.Message}");
        }

        return allBarcodes;
    }

    private static readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

    public async Task<(bool success, string error)> WriteValueAsync(string address, object value)
    {
        if (string.IsNullOrEmpty(address)) return (false, "地址为空");
        
        await _writeLock.WaitAsync();
        try
        {
            string cleanAddr = address.Replace(" ", "").ToUpper();
            
            // 【双重保险】自动补全 DB 编号
            // 逻辑：如果以 DB 开头，但不是以 "DB<数字>." 开头 (即缺少数据块编号)，则自动补齐
            bool isMissingDbNum = cleanAddr.StartsWith("DB") && !System.Text.RegularExpressions.Regex.IsMatch(cleanAddr, @"^DB\d+\.");
            
            if (isMissingDbNum && _currentActiveDbNum > 0)
            {
                cleanAddr = $"DB{_currentActiveDbNum}.{cleanAddr}";
                _logger.LogInformation("[Auto-Fix] 地址自动补齐 DB 编号: {Old} -> {New}", address, cleanAddr);
            }
            
            // 1. 处理 JsonElement 的基础解包
            if (value is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<byte>();
                    foreach (var item in je.EnumerateArray()) list.Add((byte)item.GetInt32());
                    value = list.ToArray();
                }
                else
                {
                    value = je.ValueKind switch {
                        JsonValueKind.Number => je.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String => je.GetString(),
                        _ => value
                    };
                }
            }

            // 2. 核心：根据地址类型自动转换 Value 的数据类型
            // 如果是写入位 (DBX)
            if (cleanAddr.Contains("DBX") || cleanAddr.Contains(".X"))
            {
                string sVal = value?.ToString() ?? "0";
                value = (sVal == "1" || sVal.ToLower() == "true");
            }
            // 如果是写入字节 (DBB) 且不是数组
            else if ((cleanAddr.Contains("DBB") || cleanAddr.Contains(".B")) && value is not byte[])
            {
                value = Convert.ToByte(value);
            }
            // 如果是写入整数 (DBW)
            else if (cleanAddr.Contains("DBW") || cleanAddr.Contains(".W"))
            {
                value = Convert.ToUInt16(value);
            }

            // 3. 强制检查连接
            if (_plc == null || !_plc.IsConnected) await ConnectAsync();

            // 4. 执行写入
            if (value is byte[] bytesToWrite)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(cleanAddr, @"\d+");
                int dbNum = int.Parse(matches[0].Value);
                int start = int.Parse(matches[1].Value);
                await Task.Run(() => _plc.WriteBytes(DataType.DataBlock, dbNum, start, bytesToWrite));
            }
            else
            {
                await Task.Run(() => _plc.Write(cleanAddr, value));
            }
            
            await LogToMonitor("WRITE", cleanAddr, value is byte[] b ? $"BYTES[{b.Length}]" : (value.ToString() ?? ""), "SUCCESS");
            return (true, "SUCCESS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PLC Write Error] " + ex.Message);
            await LogToMonitor("WRITE", address, "ERROR: " + ex.Message, "ERROR");
            return (false, "写入异常: " + ex.Message);
        }
        finally
        {
            _writeLock.Release();
        }
    }


    private async Task LogToMonitor(string action, string address, string value, string status)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceivePlcMonitor", new 
            { 
                time = DateTime.Now.ToString("HH:mm:ss.fff"),
                action = action, 
                address = address, 
                result = value, // 这里改回 result，匹配 UI 表格字段
                status = status
            });
        }
        catch { }
    }

    private async Task TriggerLoopAsync(CancellationToken token)
    {
        _logger.LogInformation("[Trigger] 触发信号监听任务已启动 (200ms)");
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_plc != null && _plc.IsConnected)
                {
                    // 1. 监控 A 面 - 直接使用配置地址
                    if (!string.IsNullOrEmpty(_aStackFinishAddress))
                    {
                        var valA = await ReadValueAsync(_aStackFinishAddress);
                        if (valA is bool bA && bA)
                        {
                            int layers = 0;
                            int moduleSn = 0;
                            int dbNum = _aStackDbNum;
                            
                            _logger.LogInformation("[DEBUG] A面触发! 内部地址状态: 层数={L}, 序号={S}, DB={DB}", _cellLayerAddress, _moduleSnAddress, dbNum);

                            if (!string.IsNullOrEmpty(_cellLayerAddress)) {
                                var layerAddrA = GetSideAgnosticAddress(_cellLayerAddress, dbNum);
                                var layerVal = await ReadValueAsync(layerAddrA);
                                layers = SafeConvertToInt(layerVal);
                                _logger.LogInformation("[PLC] A面层数原始值: {Raw}, 转换后: {Val}, 地址: {Addr}", layerVal, layers, layerAddrA);
                            }
                            if (!string.IsNullOrEmpty(_moduleSnAddress)) {
                                var snAddrA = GetSideAgnosticAddress(_moduleSnAddress, dbNum);
                                var snVal = await ReadValueAsync(snAddrA);
                                moduleSn = SafeConvertToInt(snVal);
                                _logger.LogInformation("[PLC] A面模组序号原始值: {Raw}, 转换后: {Val}, 地址: {Addr}", snVal, moduleSn, snAddrA);
                            }
                             Console.WriteLine($"[Trigger] A面触发, DB: {dbNum}, 层数: {layers}, 模组序号: {moduleSn}");
                            _currentActiveDbNum = dbNum; // 记录当前活跃 DB
                            await HandlePlcTrigger("A面", layers, moduleSn, dbNum);
                            await WriteValueAsync(_aStackFinishAddress, false); // 立即复位
                        }
                    }

                    // 2. 监控 B 面 - 直接使用配置地址
                    if (!string.IsNullOrEmpty(_bStackFinishAddress))
                    {
                        var valB = await ReadValueAsync(_bStackFinishAddress);
                        if (valB is bool bB && bB)
                        {
                            int layers = 0;
                            int moduleSn = 0;
                            int dbNum = _bStackDbNum;

                            _logger.LogInformation("[DEBUG] B面触发! 内部地址状态: 层数={L}, 序号={S}, DB={DB}", _cellLayerAddress, _moduleSnAddress, dbNum);

                            if (!string.IsNullOrEmpty(_cellLayerAddress)) {
                                var layerAddrB = GetSideAgnosticAddress(_cellLayerAddress, dbNum);
                                var layerVal = await ReadValueAsync(layerAddrB);
                                layers = SafeConvertToInt(layerVal);
                                _logger.LogInformation("[PLC] B面层数原始值: {Raw}, 转换后: {Val}, 地址: {Addr}", layerVal, layers, layerAddrB);
                            }
                            if (!string.IsNullOrEmpty(_moduleSnAddress)) {
                                var snAddrB = GetSideAgnosticAddress(_moduleSnAddress, dbNum);
                                var snVal = await ReadValueAsync(snAddrB);
                                moduleSn = SafeConvertToInt(snVal);
                                _logger.LogInformation("[PLC] B面模组序号原始值: {Raw}, 转换后: {Val}, 地址: {Addr}", snVal, moduleSn, snAddrB);
                            }
                            else {
                                _logger.LogWarning("[PLC] ⚠ B面触发成功，但 _moduleSnAddress 为空");
                            }
                            Console.WriteLine($"[Trigger] B面触发, DB: {dbNum}, 层数: {layers}, 模组序号: {moduleSn}");
                            _currentActiveDbNum = dbNum; // 记录当前活跃 DB
                            await HandlePlcTrigger("B面", layers, moduleSn, dbNum);
                            await WriteValueAsync(_bStackFinishAddress, false); // 立即复位
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("触发信号监听异常: {Msg}", ex.Message);
            }

            await Task.Delay(200, token); // 触发信号监控改为 200ms 一次，与日志描述一致
        }
    }

    private int SafeConvertToInt(object? val)
    {
        if (val == null) return 0;
        try {
            if (val is bool b) return b ? 1 : 0;
            return Convert.ToInt32(val);
        } catch {
            return 0;
        }
    }

    private async Task HandlePlcTrigger(string side, int layers, int moduleSn, int dbNum)
    {
        _logger.LogInformation("[Trigger] 检测到 {Side} 堆叠完成信号! 层数: {Layers}, 模组序号: {ModuleSn}, DB: {DB}", side, layers, moduleSn, dbNum);
        await LogToFrontend("success", $"[PLC触发] 收到 {side} 堆叠完成信号，电芯层数: {layers}, 模组序号: {moduleSn} (DB{dbNum})");
        await _hubContext.Clients.All.SendAsync("ReceivePlcTrigger", new { side, layers, moduleSn, dbNum, time = DateTime.Now.ToString("HH:mm:ss") });
    }

    private async Task LogToFrontend(string type, string message)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveLog", new 
            { 
                type = type, 
                message = message, 
                time = DateTime.Now.ToString("HH:mm:ss") 
            });
        }
        catch { }
    }

    private void LoadConfigFromFile()
    {
        try
        {
            var rootPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../"));
            string configPath = Path.Combine(rootPath, "Config", "app_config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string ip = root.TryGetProperty("plcIp", out var pIp) ? pIp.GetString() ?? "192.168.0.1" : "192.168.0.1";
                string cpu = root.TryGetProperty("plcCpu", out var pCpu) ? pCpu.GetString() ?? "S71200" : "S71200";
                short rack = root.TryGetProperty("plcRack", out var pRack) ? (short)pRack.GetInt32() : (short)0;
                short slot = root.TryGetProperty("plcSlot", out var pSlot) ? (short)pSlot.GetInt32() : (short)1;
                string hb = root.TryGetProperty("plcHeartbeatAddress", out var pHb) ? pHb.GetString() ?? "" : "";
                string aStack = root.TryGetProperty("plcAStackFinishAddress", out var pA) ? pA.GetString() ?? "" : "";
                string bStack = root.TryGetProperty("plcBStackFinishAddress", out var pB) ? pB.GetString() ?? "" : "";
                string layers = root.TryGetProperty("plcCellLayerAddress", out var pL) ? pL.GetString() ?? "" : "";
                string moduleSn = root.TryGetProperty("plcModuleSnAddress", out var pM) ? pM.GetString() ?? "" : "";
                string c1 = root.TryGetProperty("col1StartAddr", out var pc1) ? pc1.GetString() ?? "" : "";
                string c2 = root.TryGetProperty("col2StartAddr", out var pc2) ? pc2.GetString() ?? "" : "";
                string c3 = root.TryGetProperty("col3StartAddr", out var pc3) ? pc3.GetString() ?? "" : "";
                int aDb = root.TryGetProperty("plcAStackDbNum", out var paDb) ? paDb.GetInt32() : 1590;
                int bDb = root.TryGetProperty("plcBStackDbNum", out var pbDb) ? pbDb.GetInt32() : 1591;

                SetConnection(ip, cpu, rack, slot, hb, aStack, bStack, layers, moduleSn, c1, c2, c3, aDb, bDb);
                Console.WriteLine($"[Init] 启动自加载成功: IP={ip}, L={layers}, M={moduleSn}, ADB={aDb}, BDB={bDb}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Init] 启动自加载失败: {ex.Message}");
        }
    }
}
