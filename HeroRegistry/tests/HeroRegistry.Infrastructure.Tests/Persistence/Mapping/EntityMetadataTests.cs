using System.Data;
using HeroRegistry.Domain.Heroes;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence.Mapping;
using HeroRegistry.Infrastructure.Persistence.Maps;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Mapping;

public class EntityMetadataTests
{
    private static readonly EntityMetadata Gadgets = EntityMetadata.For(new GadgetMap());

    [Fact]
    public void Metadata_is_built_once_per_map_and_cached()
    {
        EntityMetadata.For(new GadgetMap()).ShouldBeSameAs(EntityMetadata.For(new GadgetMap()));
    }

    [Fact]
    public void Resolves_the_table_and_the_key()
    {
        Gadgets.EntityType.ShouldBe(typeof(Gadget));
        Gadgets.TableName.ShouldBe("Gadgets");
        Gadgets.QualifiedTableName.ShouldBe("[dbo].[Gadgets]");
        Gadgets.Key.Name.ShouldBe("Id");
        Gadgets.Key.IsKey.ShouldBeTrue();
    }

    [Fact]
    public void Maps_every_property_with_a_setter_except_ignored_ones()
    {
        Gadgets.Columns.Select(c => c.Name).ShouldBe(
            ["Id", "Name", "Description", "Kind", "Charges", "IsDeleted", "BuiltOn", "UpdatedAt", "Tags"]);
    }

    [Fact]
    public void Get_only_and_ignored_properties_are_not_mapped()
    {
        Gadgets.FindColumn(nameof(Gadget.DisplayName)).ShouldBeNull();
        Gadgets.FindColumn(nameof(Gadget.Secret)).ShouldBeNull();
    }

    [Fact]
    public void Columns_are_found_by_name_ignoring_case()
    {
        Gadgets.FindColumn("description")!.Property.Name.ShouldBe(nameof(Gadget.Notes));
    }

    [Theory]
    [InlineData("Id", DbType.Int32, 0)]
    [InlineData("Name", DbType.String, 50)]
    [InlineData("Description", DbType.String, 200)]
    [InlineData("Kind", DbType.Int32, 0)] // enum stored as its underlying integer
    [InlineData("Charges", DbType.Int32, 0)]
    [InlineData("IsDeleted", DbType.Boolean, 0)]
    [InlineData("BuiltOn", DbType.Date, 0)]
    [InlineData("UpdatedAt", DbType.DateTime2, 0)]
    [InlineData("Tags", DbType.String, -1)] // JSON in NVARCHAR(MAX)
    public void Each_column_gets_an_explicit_database_type_and_size(string column, DbType dbType, int size)
    {
        var metadata = Gadgets.FindColumn(column)!;

        metadata.DbType.ShouldBe(dbType);
        metadata.Size.ShouldBe(size);
    }

    [Fact]
    public void Nullability_follows_the_property_declaration()
    {
        Gadgets.FindColumn("Description")!.IsNullable.ShouldBeTrue(); // string?
        Gadgets.FindColumn("Charges")!.IsNullable.ShouldBeTrue();     // int?
        Gadgets.FindColumn("Name")!.IsNullable.ShouldBeFalse();       // string
        Gadgets.FindColumn("Kind")!.IsNullable.ShouldBeFalse();       // enum
    }

    [Fact]
    public void Insert_only_JSON_and_soft_delete_settings_are_applied()
    {
        Gadgets.FindColumn("BuiltOn")!.IsInsertOnly.ShouldBeTrue();
        Gadgets.FindColumn("Tags")!.IsJson.ShouldBeTrue();
        Gadgets.SoftDeleteFlag!.Name.ShouldBe("IsDeleted");
        Gadgets.SoftDeleteAlsoSets.Select(c => c.Name).ShouldBe(["UpdatedAt"]);
    }

    // Domain maps (T04 entities, mapped without attributes)

    [Fact]
    public void SuperHero_maps_to_the_SuperHeroes_table_with_every_column()
    {
        var heroes = EntityMetadata.For(new SuperHeroMap());

        heroes.QualifiedTableName.ShouldBe("[dbo].[SuperHeroes]");
        heroes.Key.Name.ShouldBe("Id");
        heroes.Columns.Select(c => c.Name).ShouldBe(
        [
            "Id", "HeroName", "FirstName", "LastName", "BirthDate", "Email", "Superpowers", "PowerLevel",
            "Address1", "Address2", "City", "State", "ZipCode",
            "IsDeleted", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt",
        ], ignoreOrder: true);
        heroes.FindColumn("Superpowers")!.IsJson.ShouldBeTrue();
        heroes.FindColumn("State")!.DbType.ShouldBe(DbType.AnsiStringFixedLength);
        heroes.FindColumn("ZipCode")!.DbType.ShouldBe(DbType.AnsiString);
        heroes.FindColumn("Address2")!.IsNullable.ShouldBeTrue();
    }

    [Theory]
    [InlineData(typeof(SuperHeroMap))]
    [InlineData(typeof(UserMap))]
    public void Auditable_entities_protect_the_creation_audit_and_soft_delete(Type mapType)
    {
        var metadata = mapType == typeof(UserMap)
            ? EntityMetadata.For(new UserMap())
            : EntityMetadata.For(new SuperHeroMap());

        metadata.FindColumn("CreatedBy")!.IsInsertOnly.ShouldBeTrue();
        metadata.FindColumn("CreatedAt")!.IsInsertOnly.ShouldBeTrue();
        metadata.SoftDeleteFlag!.Name.ShouldBe("IsDeleted");
        metadata.SoftDeleteAlsoSets.Select(c => c.Name).ShouldBe(["UpdatedBy", "UpdatedAt"]);
    }

    [Fact]
    public void User_maps_to_the_Users_table()
    {
        var users = EntityMetadata.For(new UserMap());

        users.QualifiedTableName.ShouldBe("[dbo].[Users]");
        users.Columns.Select(c => c.Name).ShouldBe(
            ["Id", "UserName", "Email", "Password", "IsAdmin", "IsDeleted", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"],
            ignoreOrder: true);
    }

    // Invalid maps fail fast, with a clear message

    [Fact]
    public void A_map_without_a_table_is_rejected()
    {
        Should.Throw<InvalidOperationException>(() => EntityMetadata.For(new NoTableMap())).Message.ShouldContain("ToTable");
    }

    [Fact]
    public void A_map_without_a_key_is_rejected()
    {
        Should.Throw<InvalidOperationException>(() => EntityMetadata.For(new NoKeyMap())).Message.ShouldContain("HasKey");
    }

    [Theory]
    [InlineData("Users; DROP TABLE Users")]
    [InlineData("Users]")]
    [InlineData("1Users")]
    [InlineData("")]
    public void Table_names_must_be_plain_identifiers(string tableName)
    {
        Should.Throw<ArgumentException>(() => EntityMetadata.For(new BadTableMap(tableName)));
    }

    [Fact]
    public void Column_names_must_be_plain_identifiers()
    {
        Should.Throw<ArgumentException>(() => EntityMetadata.For(new BadColumnMap()));
    }

    [Fact]
    public void Unsupported_property_types_are_reported_with_a_hint()
    {
        var exception = Should.Throw<NotSupportedException>(() => EntityMetadata.For(new UnconfiguredListMap()));

        exception.Message.ShouldContain("Gadget.Tags");
        exception.Message.ShouldContain("StoredAsJson");
    }

    [Fact]
    public void Property_expressions_must_select_a_property_of_the_entity()
    {
        Should.Throw<ArgumentException>(() => EntityMetadata.For(new NotAPropertyMap()));
    }

    private sealed class NoTableMap : EntityMap<Gadget>
    {
        public NoTableMap() => HasKey(g => g.Id);
    }

    private sealed class NoKeyMap : EntityMap<Gadget>
    {
        public NoKeyMap() => ToTable("Gadgets");
    }

    private sealed class BadTableMap : EntityMap<Gadget>
    {
        public BadTableMap(string tableName)
        {
            ToTable(tableName);
            HasKey(g => g.Id);
        }
    }

    private sealed class BadColumnMap : EntityMap<Gadget>
    {
        public BadColumnMap()
        {
            ToTable("Gadgets");
            HasKey(g => g.Id);
            Property(g => g.Name).HasColumnName("Name]; DROP TABLE Users;--");
        }
    }

    private sealed class UnconfiguredListMap : EntityMap<Gadget>
    {
        public UnconfiguredListMap()
        {
            ToTable("Gadgets");
            HasKey(g => g.Id);
            Ignore(g => g.Secret);
        }
    }

    private sealed class NotAPropertyMap : EntityMap<Gadget>
    {
        public NotAPropertyMap()
        {
            ToTable("Gadgets");
            HasKey(g => g.Id);
            Property(g => g.Name.Length);
        }
    }
}
