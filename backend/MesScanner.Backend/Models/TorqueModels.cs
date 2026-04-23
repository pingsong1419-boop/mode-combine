namespace MesScanner.Backend.Models;

public class LogEntry
{
    public string Time { get; set; } = DateTime.Now.ToString("HH:mm:ss");
    public string Level { get; set; } = "info";
    public string Msg { get; set; } = string.Empty;
    public bool IsHeartbeat { get; set; } = false;
}

public class TorqueResult
{
    public string Torque { get; set; } = "0.00";
    public string Angle { get; set; } = "0";
    public string Status { get; set; } = "OK";
}
