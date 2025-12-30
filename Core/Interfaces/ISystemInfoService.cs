using Winsane.Core.Models;

namespace Winsane.Core.Interfaces;

public interface ISystemInfoService : IDisposable
{
    Task<SystemInfo> GetSystemInfoAsync();
    float GetCpuUsage();
    float GetGpuUsage();
    (float UsedGb, float TotalGb, float Percentage) GetRamUsage();
}
