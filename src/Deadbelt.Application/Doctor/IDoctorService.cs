using Deadbelt.Domain.Doctor;

namespace Deadbelt.Application.Doctor;

public interface IDoctorService
{
    Task<DoctorScanResult> ScanAsync(
        DoctorScanRequest request,
        CancellationToken cancellationToken = default);
}
