using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// The API's contract is its own types, not the database's.
//
// Requests bind to purpose-made request types, never to a domain entity:
// binding an entity lets a client set any column on it (ids, ownership,
// status, audit fields) - OWASP API3, mass assignment.
//
// Responses: a service method a controller calls returns a response type,
// never an entity - not directly, not in a list, and not as a property of a
// response type. An entity's JSON follows the database schema and whatever
// EF happened to load (including a user's password hash when a navigation
// to User is loaded). The calls are read from the controllers' compiled IL,
// so a method only other services use may still return entities.
public class ApiContractTests
{
    private static readonly Assembly Domain = typeof(MicroLIMS.Domain.Entities.Sample).Assembly;
    private static readonly Assembly Application = typeof(MicroLIMS.Application.Services.SampleSummaryService).Assembly;
    private static readonly Assembly Api = typeof(MicroLIMS.API.Controllers.SampleController).Assembly;

    private static bool IsEntity(Type t) => t.Assembly == Domain && t.Namespace == "MicroLIMS.Domain.Entities";

    private static bool IsOrContainsEntity(Type t) =>
        IsEntity(t)
        || (t.IsGenericType && t.GetGenericArguments().Any(IsOrContainsEntity))
        || (t.IsArray && IsOrContainsEntity(t.GetElementType()!));

    // Like IsOrContainsEntity, but also looks inside the Application's own
    // response types, whose properties end up in the JSON too.
    private static string? EntityPath(Type t, HashSet<Type> seen)
    {
        if (!seen.Add(t)) return null;
        if (IsEntity(t)) return t.Name;
        if (t.IsArray) return EntityPath(t.GetElementType()!, seen);
        if (t.IsGenericType)
            foreach (var arg in t.GetGenericArguments())
                if (EntityPath(arg, seen) is { } inner) return inner;
        if (t.Assembly == Application)
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (EntityPath(p.PropertyType, seen) is { } inner) return $"{t.Name}.{p.Name} -> {inner}";
        return null;
    }

    private static IEnumerable<Type> Controllers() =>
        Api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    [Fact]
    public void NoAction_BindsADomainEntity()
    {
        var offenders = Controllers()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters().Select(p => (Method: m, Param: p)))
            .Where(x => IsOrContainsEntity(x.Param.ParameterType))
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}({x.Param.ParameterType.Name} {x.Param.Name})")
            .ToList();

        Assert.True(offenders.Count == 0, "Bind a request DTO instead:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void NoServiceMethodACallsController_ReturnsAnEntity()
    {
        var called = Controllers().SelectMany(CalledMethods)
            .OfType<MethodInfo>()
            .Where(m => m.DeclaringType?.Assembly == Application)
            .Distinct()
            .ToList();
        Assert.NotEmpty(called);

        var offenders = called
            .Select(m => (Method: m, Path: EntityPath(m.ReturnType, new HashSet<Type>())))
            .Where(x => x.Path is not null)
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name} returns {x.Path}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0, "Return a response type, not an entity:\n" + string.Join("\n", offenders));
    }

    // Every method called from a controller's own methods, its async state
    // machines and its lambdas (compiler-generated nested types).
    private static IEnumerable<MethodBase> CalledMethods(Type controller)
    {
        foreach (var type in WithNested(controller))
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il is null) continue;
            foreach (var token in MethodTokens(il))
            {
                MethodBase? callee = null;
                try
                {
                    callee = type.Module.ResolveMethod(token,
                        type.IsGenericType ? type.GetGenericArguments() : null,
                        method.IsGenericMethod ? method.GetGenericArguments() : null);
                }
                catch (ArgumentException) { }
                if (callee is not null) yield return callee;
            }
        }
    }

    private static IEnumerable<Type> WithNested(Type t) =>
        new[] { t }.Concat(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(WithNested));

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    // Walks the IL stream opcode by opcode and yields the token of every
    // instruction with a method operand (call, callvirt, newobj, ldftn...).
    private static IEnumerable<int> MethodTokens(byte[] il)
    {
        var i = 0;
        while (i < il.Length)
        {
            short value = il[i++];
            if (value == 0xFE) value = unchecked((short)(0xFE00 | il[i++]));
            if (!OpCodesByValue.TryGetValue(value, out var op)) yield break;

            switch (op.OperandType)
            {
                case OperandType.InlineMethod:
                    yield return BitConverter.ToInt32(il, i);
                    i += 4;
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    i += 4 + 4 * BitConverter.ToInt32(il, i);
                    break;
                default:
                    i += 4;
                    break;
            }
        }
    }
}
