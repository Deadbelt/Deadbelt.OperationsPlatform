namespace Deadbelt.Infrastructure.Doctor;

internal interface IDoctorFileSystem
{
    DoctorPathInspection InspectFile(
        string path,
        CancellationToken cancellationToken);

    DoctorPathInspection InspectDirectory(
        string path,
        CancellationToken cancellationToken);

    DoctorTextReadResult ReadText(
        string path,
        long maximumBytes,
        CancellationToken cancellationToken);

    DoctorDirectoryEnumerationResult EnumerateDirectory(
        string path,
        int maximumEntries,
        CancellationToken cancellationToken);
}
