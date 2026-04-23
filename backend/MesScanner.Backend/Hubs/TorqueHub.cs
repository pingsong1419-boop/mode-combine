using Microsoft.AspNetCore.SignalR;
using MesScanner.Backend.Models;

namespace MesScanner.Backend.Hubs;

public class TorqueHub : Hub
{
    private readonly ILogger<TorqueHub> _logger;

    public TorqueHub(ILogger<TorqueHub> logger)
    {
        _logger = logger;
    }

    public async Task SendCommand(string mid, string pset = "")
    {
        _logger.LogInformation("Received command from client: {MID} {PSet}", mid, pset);
        // 这里会调用 TorqueControllerService 发送指令
    }
}
