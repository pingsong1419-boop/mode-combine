using MesScanner.Backend.Hubs;
using MesScanner.Backend.Services;
using MesScanner.Backend.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSignalR();
builder.Services.AddSingleton<PlcService>();
builder.Services.AddSingleton<TorqueControllerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlcService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TorqueControllerService>());
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // SignalR 需要此项
    });
});

var app = builder.Build();

Console.WriteLine("==================================================");
Console.WriteLine("🚀 MES PLC Backend 正在启动...");
Console.WriteLine($"启动时间: {DateTime.Now}");
Console.WriteLine("==================================================");

app.UseCors("AllowAll");

app.MapHub<TorqueHub>("/torqueHub");

// Configure the HTTP request pipeline.

app.MapGet("/", () => "MES PLC Backend is Running");

// --- PLC Commands ---
app.MapPost("/api/plc/config", (PlcRequest req, PlcService service) => {
    Console.WriteLine("--------------------------------------------------");
    Console.WriteLine($"[API] 收到 PLC 配置更新请求:");
    Console.WriteLine($"      IP: {req.Ip}");
    Console.WriteLine($"      OK信号地址: {req.PlcOkAddr}");
    Console.WriteLine($"      NG信号地址: {req.PlcNgAddr}");
    Console.WriteLine("--------------------------------------------------");

    service.SetConnection(req.Ip, req.CpuType, req.Rack, req.Slot, req.HeartbeatAddress, 
        req.AStackAddr, req.BStackAddr, req.CellLayerAddr, req.ModuleSnAddr,
        req.Col1StartAddr, req.Col2StartAddr, req.Col3StartAddr,
        req.AStackDbNum, req.BStackDbNum,
        req.PlcOkAddr, req.PlcNgAddr);
    return Results.Ok(new { message = "PLC Config Updated" });
});

app.MapGet("/api/plc/read", async (string address, [Microsoft.AspNetCore.Mvc.FromQuery] int count, PlcService service) => {
    Console.WriteLine($"[API] Read Request: Address={address}, Count={count}");
    var val = await service.ReadValueAsync(address, count);
    if (val == null) return Results.BadRequest("Read Failed or Disconnected");
    return Results.Ok(new { address, value = val });
});

app.MapPost("/api/plc/read-barcodes", async (HttpContext context, PlcService service) => {
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var doc = System.Text.Json.JsonDocument.Parse(body);
    var root = doc.RootElement;
    
    int layers = root.GetProperty("layers").GetInt32();
    int barcodeLength = root.GetProperty("barcodeLength").GetInt32();
    string c1 = root.GetProperty("col1Addr").GetString() ?? "";
    string c2 = root.GetProperty("col2Addr").GetString() ?? "";
    string c3 = root.GetProperty("col3Addr").GetString() ?? "";
    int dbNum = root.TryGetProperty("dbNum", out var pDb) ? pDb.GetInt32() : 0;

    var codes = await service.ReadCellBarcodesAsync(layers, barcodeLength, c1, c2, c3, dbNum);
    return Results.Ok(new { barcodes = codes });
});

app.MapPost("/api/plc/write", async (PlcWriteRequest req, PlcService service) => {
    var (success, error) = await service.WriteValueAsync(req.Address, req.Value);
    if (!success) return Results.BadRequest(new { message = error });
    return Results.Ok(new { message = "Write Success", address = req.Address, value = req.Value });
});

// --- Configuration Endpoints ---
// 路径定位到项目根目录下的 Config 文件夹
var rootPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../"));
string ConfigPath = Path.Combine(rootPath, "Config", "app_config.json");
string RecipeConfigPath = Path.Combine(rootPath, "Config", "Recipe_config.json");

app.MapGet("/api/config", async () => {
    if (!File.Exists(ConfigPath)) return Results.NotFound("Config file not found");
    var json = await File.ReadAllTextAsync(ConfigPath);
    return Results.Content(json, "application/json");
});

// --- Proxy Endpoint for CORS bypass ---

app.MapPost("/api/proxy", async (HttpContext context, IHttpClientFactory clientFactory) => {
    try {
        var targetUrl = context.Request.Query["url"].ToString();
        if (string.IsNullOrEmpty(targetUrl)) return Results.BadRequest("Target URL missing");

        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();

        var client = clientFactory.CreateClient();
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        
        Console.WriteLine($"[Proxy] >>> Requesting: {targetUrl}");
        Console.WriteLine($"[Proxy] >>> Body: {json}");
        
        var response = await client.PostAsync(targetUrl, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        Console.WriteLine($"[Proxy] <<< Response Status: {response.StatusCode}");
        Console.WriteLine($"[Proxy] <<< Response Body: {responseBody}");

        return Results.Content(responseBody, "application/json");
    } catch (Exception ex) {
        Console.WriteLine($"[Proxy] !!! Error: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

// --- Torque Commands ---
app.MapPost("/api/command", async (string mid, string? pset, TorqueControllerService service) => {
    Console.WriteLine($"[API] Torque Command: MID={mid}, PSet={pset}");
    string packet = mid.PadLeft(4, '0');
    if (mid == "0018" && !string.IsNullOrEmpty(pset))
    {
        packet = "0020" + mid + pset.PadLeft(3, '0') + "         ";
    }
    else if (mid == "0043" || mid == "0042" || mid == "0060")
    {
        packet = "0020" + mid + "            ";
    }
    
    await service.SendPacketAsync(packet);
    return Results.Ok(new { message = "Command Sent", mid, pset });
});

app.MapPost("/api/config", async (HttpContext context) => {
    try {
        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(json)) return Results.BadRequest("Empty config");
        
        await File.WriteAllTextAsync(ConfigPath, json);
        Console.WriteLine($"[Config] Saved successfully to {ConfigPath}");
        return Results.Ok(new { message = "Config Saved Successfully" });
    } catch (Exception ex) {
        Console.WriteLine($"[Config] Save Error: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

// --- Recipe Configuration Endpoints ---

app.MapGet("/api/recipe/config", async () => {
    if (!File.Exists(RecipeConfigPath)) {
        await File.WriteAllTextAsync(RecipeConfigPath, "{\"masters\":[], \"details\":[]}");
    }
    var json = await File.ReadAllTextAsync(RecipeConfigPath);
    return Results.Content(json, "application/json");
});

app.MapPost("/api/recipe/config", async (HttpContext context) => {
    try {
        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        if (string.IsNullOrEmpty(json)) return Results.BadRequest("Empty recipe data");
        
        await File.WriteAllTextAsync(RecipeConfigPath, json);
        Console.WriteLine($"[Recipe] Saved successfully to {RecipeConfigPath}");
        return Results.Ok(new { message = "Recipe Saved Successfully" });
    } catch (Exception ex) {
        Console.WriteLine($"[Recipe] Save Error: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.Run("http://localhost:5247");
