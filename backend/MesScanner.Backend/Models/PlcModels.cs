namespace MesScanner.Backend.Models;

public class PlcRequest
{
    public string Ip { get; set; } = "192.168.0.1";
    public string CpuType { get; set; } = "S71200";
    public short Rack { get; set; } = 0;
    public short Slot { get; set; } = 1;
    public string HeartbeatAddress { get; set; } = "";
    public string AStackAddr { get; set; } = "";
    public string BStackAddr { get; set; } = "";
    public string CellLayerAddr { get; set; } = "";
    public string Col1StartAddr { get; set; } = "";
    public string Col2StartAddr { get; set; } = "";
    public string Col3StartAddr { get; set; } = "";
}

public class PlcReadRequest
{
    public string Address { get; set; } = "";
}

public class PlcWriteRequest
{
    public string Address { get; set; } = "";
    public object Value { get; set; } = 0;
}
