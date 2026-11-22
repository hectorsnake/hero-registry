using System.Reflection;

namespace HeroRegistry.Infrastructure;

/// <summary>
/// Marker used to locate the Infrastructure assembly (architecture tests, assembly scanning).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
