using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Deadbelt.Infrastructure.Doctor;
using Microsoft.Win32;

namespace Deadbelt.Infrastructure.Tests;

public sealed class DoctorArchitectureTests
{
    private static readonly OpCode[] OneByteOpCodes = BuildOpCodeLookup(multiByte: false);
    private static readonly OpCode[] MultiByteOpCodes = BuildOpCodeLookup(multiByte: true);

    [Fact]
    public void DoctorImplementationDoesNotCallWriteCapableOrExecutionApis()
    {
        var assembly = typeof(DayZLocalDoctorScanner).Assembly;
        var roots = assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "Deadbelt.Infrastructure.Doctor",
                StringComparison.Ordinal) == true)
            .SelectMany(GetDeclaredMethods)
            .ToArray();
        var calls = GetReachableCalls(roots, assembly);

        Assert.NotEmpty(roots);
        Assert.NotEmpty(calls);
        Assert.Contains(
            calls,
            call =>
                call.Caller.DeclaringType == typeof(OperatingSystemDoctorFileSystem)
                && call.Callee.DeclaringType == typeof(File)
                && call.Callee.Name == nameof(File.GetAttributes));
        Assert.Contains(
            calls,
            call =>
                call.Caller.DeclaringType == typeof(DayZLocalDoctorScanner)
                && call.Callee.DeclaringType == typeof(Path)
                && call.Callee.Name == nameof(Path.Combine));
        Assert.Contains(
            calls,
            call =>
                call.Caller.DeclaringType == typeof(DayZConfigurationParser)
                && call.Callee.DeclaringType == typeof(DayZTextParser));
        Assert.Contains(
            calls,
            call =>
                call.Caller.DeclaringType == typeof(DayZLocalDoctorScanner)
                && call.Callee.DeclaringType == typeof(DayZPowerShellStartupParser)
                && call.Callee.Name == nameof(DayZPowerShellStartupParser.Parse));
        Assert.True(
            IsWriteCapableOrExecutionCall(
                typeof(Registry).GetMethod(
                    nameof(Registry.SetValue),
                    [typeof(string), typeof(string), typeof(object)])!));
        Assert.True(
            IsWriteCapableOrExecutionCall(
                typeof(File).GetMethod(
                    nameof(File.SetAttributes),
                    [typeof(string), typeof(FileAttributes)])!));
        Assert.True(
            IsProhibitedByName(
                "System.Management.Automation.PowerShell",
                "Create"));
        Assert.True(
            IsProhibitedByName(
                "System.Management.Automation.PowerShell",
                "AddScript"));
        Assert.True(
            IsProhibitedByName(
                "System.Management.Automation.PowerShell",
                "Invoke"));

        var bypasses = calls
            .Where(call =>
                IsActualFileSystemRead(call.Callee)
                && call.Caller.DeclaringType != typeof(OperatingSystemDoctorFileSystem))
            .Select(Describe)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            bypasses.Length == 0,
            "Doctor filesystem reads bypass IDoctorFileSystem:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, bypasses)}");

        var prohibited = calls
            .Where(call => IsWriteCapableOrExecutionCall(call.Callee))
            .Select(Describe)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            prohibited.Length == 0,
            "Doctor production code contains write-capable or execution calls:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, prohibited)}");
    }

    private static (MethodBase Caller, MethodBase Callee)[] GetReachableCalls(
        IEnumerable<MethodBase> roots,
        Assembly assembly)
    {
        var calls = new List<(MethodBase Caller, MethodBase Callee)>();
        var pending = new Queue<MethodBase>(roots);
        var visited = new HashSet<MethodBase>();

        while (pending.Count > 0)
        {
            var caller = pending.Dequeue();

            if (!visited.Add(caller))
                continue;

            foreach (var call in GetCalledMethods(caller))
            {
                calls.Add(call);

                if (call.Callee.DeclaringType?.Assembly == assembly)
                    pending.Enqueue(call.Callee);
            }
        }

        return calls.ToArray();
    }

    private static bool IsActualFileSystemRead(MethodBase method)
    {
        var declaringType = method.DeclaringType;

        return declaringType == typeof(File)
            || declaringType == typeof(Directory)
            || declaringType == typeof(FileInfo)
            || declaringType == typeof(DirectoryInfo)
            || declaringType == typeof(FileStream);
    }

    private static string Describe((MethodBase Caller, MethodBase Callee) call) =>
        $"{call.Caller.DeclaringType?.FullName}.{call.Caller.Name} -> " +
        $"{call.Callee.DeclaringType?.FullName}.{call.Callee.Name}";

    private static bool IsWriteCapableOrExecutionCall(MethodBase method)
    {
        var declaringType = method.DeclaringType;

        if (declaringType == typeof(Process))
            return method.Name == nameof(Process.Start);

        if (declaringType == typeof(FileStream))
        {
            return method.Name.StartsWith("Write", StringComparison.Ordinal)
                || method.Name == nameof(FileStream.SetLength);
        }

        if (declaringType == typeof(StreamWriter)
            || declaringType == typeof(BinaryWriter))
            return true;

        if (declaringType == typeof(Stream))
        {
            return method.Name.StartsWith("Write", StringComparison.Ordinal)
                || method.Name == nameof(Stream.SetLength);
        }

        if (declaringType == typeof(File))
        {
            return method.Name.StartsWith("Create", StringComparison.Ordinal)
                || method.Name.StartsWith("Write", StringComparison.Ordinal)
                || method.Name.StartsWith("Append", StringComparison.Ordinal)
                || method.Name.StartsWith("Delete", StringComparison.Ordinal)
                || method.Name.StartsWith("Copy", StringComparison.Ordinal)
                || method.Name.StartsWith("Move", StringComparison.Ordinal)
                || method.Name.StartsWith("Replace", StringComparison.Ordinal)
                || method.Name.StartsWith("Set", StringComparison.Ordinal)
                || method.Name is nameof(File.Open) or nameof(File.OpenWrite);
        }

        if (declaringType == typeof(Directory))
        {
            return method.Name is nameof(Directory.CreateDirectory)
                or nameof(Directory.Delete)
                or nameof(Directory.Move)
                || method.Name.StartsWith("Set", StringComparison.Ordinal);
        }

        if (declaringType == typeof(FileInfo))
        {
            return method.Name is nameof(FileInfo.AppendText)
                or nameof(FileInfo.CopyTo)
                or nameof(FileInfo.Create)
                or nameof(FileInfo.CreateText)
                or nameof(FileInfo.Delete)
                or nameof(FileInfo.MoveTo)
                or nameof(FileInfo.Open)
                or nameof(FileInfo.OpenWrite)
                or nameof(FileInfo.Replace)
                || method.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        if (declaringType == typeof(DirectoryInfo))
        {
            return method.Name is nameof(DirectoryInfo.Create)
                or nameof(DirectoryInfo.CreateSubdirectory)
                or nameof(DirectoryInfo.Delete)
                or nameof(DirectoryInfo.MoveTo)
                || method.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        if (declaringType == typeof(FileSystemInfo))
        {
            return method.Name is nameof(FileSystemInfo.Delete)
                || method.Name.StartsWith("set_", StringComparison.Ordinal);
        }

        if (declaringType == typeof(RandomAccess))
        {
            return method.Name.StartsWith("Write", StringComparison.Ordinal)
                || method.Name == nameof(RandomAccess.SetLength);
        }

        var declaringTypeName = declaringType?.FullName;

        if (declaringTypeName is "System.IO.FileSystemAclExtensions")
            return true;

        if (IsProhibitedByName(declaringTypeName, method.Name))
            return true;

        if (declaringTypeName?.StartsWith(
                "Microsoft.Win32",
                StringComparison.Ordinal) == true)
        {
            return method.Name.StartsWith("Create", StringComparison.Ordinal)
                || method.Name.StartsWith("Set", StringComparison.Ordinal)
                || method.Name.StartsWith("Delete", StringComparison.Ordinal)
                || method.Name.StartsWith("Remove", StringComparison.Ordinal)
                || method.Name.StartsWith("Write", StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsProhibitedByName(
        string? declaringTypeName,
        string methodName)
    {
        if (declaringTypeName?.StartsWith(
                "System.Management.Automation",
                StringComparison.Ordinal) == true)
        {
            return true;
        }

        return declaringTypeName is "Microsoft.VisualBasic.Interaction"
            && methodName == "Shell";
    }

    private static IEnumerable<MethodBase> GetDeclaredMethods(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        return type
            .GetMethods(flags)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(flags));
    }

    private static IEnumerable<(MethodBase Caller, MethodBase Callee)> GetCalledMethods(
        MethodBase caller)
    {
        var il = caller.GetMethodBody()?.GetILAsByteArray();

        if (il is null)
            yield break;

        for (var position = 0; position < il.Length;)
        {
            var opCode = ReadOpCode(il, ref position);

            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var metadataToken = BitConverter.ToInt32(il, position);
                var calledMethod = caller.Module.ResolveMethod(
                    metadataToken,
                    caller.DeclaringType?.GetGenericArguments(),
                    caller.IsGenericMethod ? caller.GetGenericArguments() : null);

                if (calledMethod is not null)
                    yield return (caller, calledMethod);
            }

            position += GetOperandSize(
                opCode.OperandType,
                il,
                position);
        }
    }

    private static OpCode ReadOpCode(
        byte[] il,
        ref int position)
    {
        var firstByte = il[position++];

        return firstByte != 0xfe
            ? OneByteOpCodes[firstByte]
            : MultiByteOpCodes[il[position++]];
    }

    private static int GetOperandSize(
        OperandType operandType,
        byte[] il,
        int operandPosition)
    {
        return operandType switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget
                or OperandType.ShortInlineI
                or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget
                or OperandType.InlineField
                or OperandType.InlineI
                or OperandType.InlineMethod
                or OperandType.InlineSig
                or OperandType.InlineString
                or OperandType.InlineTok
                or OperandType.InlineType
                or OperandType.ShortInlineR => 4,
            OperandType.InlineI8
                or OperandType.InlineR => 8,
            OperandType.InlineSwitch =>
                4 + (BitConverter.ToInt32(il, operandPosition) * 4),
            _ => throw new InvalidOperationException(
                $"Unsupported IL operand type: {operandType}.")
        };
    }

    private static OpCode[] BuildOpCodeLookup(bool multiByte)
    {
        var lookup = new OpCode[256];

        foreach (var field in typeof(OpCodes).GetFields(
                     BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
                continue;

            var value = unchecked((ushort)opCode.Value);
            var isMultiByte = (value & 0xff00) == 0xfe00;

            if (isMultiByte == multiByte)
                lookup[value & 0xff] = opCode;
        }

        return lookup;
    }
}
