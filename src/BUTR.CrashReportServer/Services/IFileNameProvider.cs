using System.Threading;
using System.Threading.Tasks;

namespace BUTR.CrashReportServer.Services
{
    public interface IFilePathProvider
    {
        Task<string?> GenerateUniqueFilePath(CancellationToken ct);
    }
}