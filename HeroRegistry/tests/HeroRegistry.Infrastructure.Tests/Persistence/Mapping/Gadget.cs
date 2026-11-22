using HeroRegistry.Infrastructure.Persistence.Mapping;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Mapping;

public enum GadgetKind
{
    Tool = 1,
    Weapon = 2,
}

/// <summary>
/// A test entity that exercises every mapping feature: private constructor and setters, an enum,
/// a nullable value type, a nullable reference type, DateOnly, DateTime, a JSON list, a renamed
/// column, an ignored property and a computed (get-only) property.
/// </summary>
public sealed class Gadget
{
    private Gadget()
    {
    }

    public int Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Stored in the "Description" column.</summary>
    public string? Notes { get; private set; }

    public GadgetKind Kind { get; private set; }

    public int? Charges { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Insert-only: never changed by an UPDATE.</summary>
    public DateOnly BuiltOn { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<string> Tags { get; private set; } = [];

    /// <summary>Get-only: not mapped.</summary>
    public string DisplayName => $"{Name} ({Kind})";

    /// <summary>Explicitly ignored.</summary>
    public string Secret { get; private set; } = "not persisted";

    public static Gadget New(
        string name,
        string? notes = null,
        GadgetKind kind = GadgetKind.Tool,
        int? charges = null,
        DateOnly builtOn = default,
        DateTime updatedAt = default,
        IReadOnlyList<string>? tags = null,
        int id = 0,
        bool isDeleted = false) =>
        new()
        {
            Id = id,
            Name = name,
            Notes = notes,
            Kind = kind,
            Charges = charges,
            BuiltOn = builtOn,
            UpdatedAt = updatedAt,
            Tags = tags ?? [],
            IsDeleted = isDeleted,
        };
}

public sealed class GadgetMap : EntityMap<Gadget>
{
    public GadgetMap()
    {
        ToTable("Gadgets");
        HasKey(g => g.Id);
        Property(g => g.Name).HasMaxLength(50);
        Property(g => g.Notes).HasColumnName("Description").HasMaxLength(200);
        Property(g => g.BuiltOn).IsInsertOnly();
        Property(g => g.Tags).StoredAsJson();
        Ignore(g => g.Secret);
        HasSoftDelete(g => g.IsDeleted, g => g.UpdatedAt);
    }
}
