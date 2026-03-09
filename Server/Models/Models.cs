namespace RemoteDesk.Server.Models
{
    public record LoginRequest(string Username, string Password);
    public record PcInfo(string PcId, bool Online);
}