using System.Reflection;

namespace HeroRegistry.Domain;

/// <summary>
/// Marker used to locate the Domain assembly (architecture tests, assembly scanning).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
