using System.Reflection;
using System.Reflection.Emit;
using Deadbelt.Application.Environments;
using Deadbelt.Application.Workspaces;

namespace Deadbelt.Application.Tests;

public sealed class ArchitectureTests
{
    private static readonly OpCode[] OneByteOpCodes = BuildOpCodeLookup(multiByte: false);
    private static readonly OpCode[] MultiByteOpCodes = BuildOpCodeLookup(multiByte: true);

    [Fact]
    public void ApplicationDoesNotCallOperatingSystemFileSystemInspectionApis()
    {
        var applicationAssembly = typeof(WorkspaceService).Assembly;

        var methodCalls = applicationAssembly
            .GetTypes()
            .SelectMany(GetDeclaredMethods)
            .SelectMany(GetCalledMethods)
            .ToArray();

        Assert.Contains(
            methodCalls,
            call =>
                call.Caller.DeclaringType == typeof(EnvironmentService)
                && call.Callee.DeclaringType == typeof(Path)
                && call.Callee.Name == nameof(Path.Combine));

        var prohibitedCalls = methodCalls
            .Where(call => IsProhibitedFileSystemCall(call.Callee))
            .Select(call =>
                $"{call.Caller.DeclaringType?.FullName}.{call.Caller.Name} -> " +
                $"{call.Callee.DeclaringType?.FullName}.{call.Callee.Name}")
            .OrderBy(call => call, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            prohibitedCalls.Length == 0,
            "Deadbelt.Application contains prohibited filesystem calls:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, prohibitedCalls)}");
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
        var methodBody = caller.GetMethodBody();

        if (methodBody is null)
            yield break;

        var il = methodBody.GetILAsByteArray();

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

            position += GetOperandSize(opCode.OperandType, il, position);
        }
    }

    private static OpCode ReadOpCode(byte[] il, ref int position)
    {
        var firstByte = il[position++];

        if (firstByte != 0xfe)
            return OneByteOpCodes[firstByte];

        return MultiByteOpCodes[il[position++]];
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

    private static bool IsProhibitedFileSystemCall(MethodBase method)
    {
        var declaringType = method.DeclaringType;

        if (declaringType is null)
            return false;

        if (declaringType == typeof(Path))
            return method.Name != nameof(Path.Combine);

        if (declaringType == typeof(System.Environment))
            return method.Name == nameof(System.Environment.GetFolderPath);

        return declaringType == typeof(Directory)
            || declaringType == typeof(File)
            || declaringType == typeof(FileStream)
            || declaringType == typeof(FileInfo)
            || declaringType == typeof(DirectoryInfo)
            || declaringType == typeof(StreamReader)
            || declaringType == typeof(StreamWriter);
    }
}
