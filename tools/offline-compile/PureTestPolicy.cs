// ============================================================================
// PureTestPolicy.cs
// ============================================================================
// PURPOSE:
//   Separates unavailable engine operations from genuine test regressions.
//   Inspects managed call sites for editor APIs and applies narrow exception
//   rules to NUnit's captured exception message and stack without editor access.
// ARCHITECTURAL ROLE:
//   Offline verification policy · no runtime layer · integration tooling.
// KEY RESPONSIBILITIES:
//   - Identify attributes without executing Unity attribute constructors.
//   - Trace statically reachable project calls to UnityEditor APIs.
//   - Classify only the specified unavailable-engine exception patterns.
// DEPENDENCIES:
//   - NUnit test metadata and .NET reflection/Intermediate Language APIs.
// USAGE NOTES:
//   Editor call scanning is conservative across branches, and cannot resolve
//   reflection or virtual dispatch. Exception stacks cover executed dynamic calls.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using NUnit.Framework.Interfaces;

public static class PureTestPolicy
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)).ToDictionary(code => code.Value);
    private static readonly Dictionary<MethodBase, MethodBase[]> Calls = new Dictionary<MethodBase, MethodBase[]>();

    public static bool Has(MemberInfo member, string name) => member.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Name == name);

    public static string FindEditorCall(ITest test)
    {
        Type type = test.TypeInfo?.Type;
        if (type == null) return null;
        var roots = new List<MethodBase>();
        if (test.Method != null) roots.Add(test.Method.MethodInfo);
        for (Type parent = type; parent != null; parent = parent.BaseType)
        {
            roots.AddRange(parent.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            if (parent.TypeInitializer != null) roots.Add(parent.TypeInitializer);
            roots.AddRange(parent.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => new[] { "SetUpAttribute", "TearDownAttribute", "OneTimeSetUpAttribute", "OneTimeTearDownAttribute" }.Any(name => Has(method, name))));
        }
        var seen = new HashSet<MethodBase>();
        var queue = new Queue<MethodBase>(roots);
        while (queue.Count > 0)
        {
            MethodBase method = queue.Dequeue();
            if (!seen.Add(method)) continue;
            if (IsEditor(method.DeclaringType)) return method.DeclaringType.FullName + "." + method.Name;
            string assembly = method.DeclaringType?.Assembly.GetName().Name ?? "";
            if (method.DeclaringType?.Assembly != type.Assembly && !assembly.StartsWith("Worsen.", StringComparison.Ordinal)) continue;
            foreach (MethodBase called in GetCalls(method)) queue.Enqueue(called);
            Type declaring = method.DeclaringType;
            if (declaring?.TypeInitializer != null) queue.Enqueue(declaring.TypeInitializer);
        }
        return null;
    }

    private static bool IsEditor(Type type) => type?.Namespace == "UnityEditor" || (type?.Namespace?.StartsWith("UnityEditor.", StringComparison.Ordinal) ?? false);

    private static MethodBase[] GetCalls(MethodBase method)
    {
        if (Calls.TryGetValue(method, out var cached)) return cached;
        var calls = new List<MethodBase>();
        byte[] il = method.GetMethodBody()?.GetILAsByteArray();
        if (il != null)
        for (int offset = 0; offset < il.Length;)
        {
            short value = il[offset++];
            if (value == 0xfe) value = (short)(0xfe00 | il[offset++]);
            OpCode code = Codes[value];
            if (code.OperandType == OperandType.InlineMethod)
            {
                int token = BitConverter.ToInt32(il, offset);
                try
                {
                    calls.Add(method.Module.ResolveMethod(token, method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null));
                }
                catch (ArgumentException) { /* Unresolved generic token; runtime stack remains authoritative. */ }
            }
            offset += code.OperandType == OperandType.InlineSwitch ? 4 + 4 * BitConverter.ToInt32(il, offset) : OperandSize(code.OperandType);
        }
        return Calls[method] = calls.ToArray();
    }

    private static int OperandSize(OperandType type)
    {
        switch (type)
        {
            case OperandType.InlineNone: return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar: return 1;
            case OperandType.InlineVar: return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR: return 8;
            default: return 4;
        }
    }

    public static bool IsEnvironment(string message, string stack, string label)
    {
        message ??= "";
        stack ??= "";
        // NUnit assertions must not disappear behind an engine-dependent cleanup.
        if (label != "Error" && label != "Invalid" && label != "") return false;
        string[] errors = Regex.Split(message, @"(?:^|\r?\n)(?:TearDown|OneTimeTearDown)\s*:\s*").Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return errors.Length > 0 && errors.All(error => IsEngineError(error, stack));
    }

    private static bool IsEngineError(string message, string stack)
    {
        // NUnit prefixes cleanup stacks with --TearDown; the first actual frame
        // still determines the null/internal-call origin, not any later frame.
        stack = Regex.Replace(stack, @"\A\s*--(?:OneTime)?TearDown\s*", "");
        if (Regex.IsMatch(stack, @"(?:^|\n)\s*at UnityEditor[.]")) return true;
        bool restricted = Regex.IsMatch(message, @"\b(?:System[.](?:Security[.])?)?(SecurityException|MissingMethodException)\s*:");
        bool engineFrame = Regex.IsMatch(message + "\n" + stack, @"(?:^|\n)\s*at Unity(?:Engine|Editor)[.]");
        // The CLR can reject a caller at JIT time, before the internal-call frame
        // exists. Its exact ECall packaging diagnostic is itself engine evidence.
        bool runtimeECall = message.Contains("ECall methods must be packaged into a system module.");
        if (restricted && (runtimeECall || (engineFrame && Regex.IsMatch(message, "ECall|InternalCall|internal call", RegexOptions.IgnoreCase)) || TopIsInternalCall(stack))) return true;
        if (Regex.IsMatch(message, @"\b(?:UnityEngine[.])?UnityException\s*:") &&
            Regex.IsMatch(message, @"main thread|main-thread|editor", RegexOptions.IgnoreCase)) return true;
        return Regex.IsMatch(message, @"\b(?:System[.])?NullReferenceException\s*:") &&
            Regex.IsMatch(stack.TrimStart(), @"\Aat Unity(?:Engine|Editor)[.]");
    }

    private static bool TopIsInternalCall(string stack)
    {
        Match frame = Regex.Match(stack.TrimStart(), @"\Aat (UnityEngine[.][^\s(]+)[(]");
        if (!frame.Success) return false;
        string full = frame.Groups[1].Value;
        int split = full.LastIndexOf('.');
        string typeName = full.Substring(0, split), methodName = full.Substring(split + 1);
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(typeName);
            if (type == null) continue;
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Any(method => method.Name == methodName && (method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0);
        }
        return false;
    }
}
