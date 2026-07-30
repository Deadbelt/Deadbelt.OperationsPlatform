using Deadbelt.Domain.Doctor;
using Deadbelt.Domain.Environments;

namespace Deadbelt.Application.Doctor;

public interface IDoctorScanner
{
    bool Supports(GameType gameType);

    Task<DoctorScanResult> ScanAsync(
        DoctorScanRequest request,
        CancellationToken cancellationToken = default);
}
