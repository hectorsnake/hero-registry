using System.Reflection;

namespace HeroRegistry.Application;

/// <summary>
/// Marker used to locate the Application assembly (architecture tests, assembly scanning).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
