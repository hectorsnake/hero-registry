using System.Data;
using HeroRegistry.Domain.Heroes;
using HeroRegistry.Infrastructure.Persistence.Mapping;
using HeroRegistry.Infrastructure.Persistence.Maps;
using HeroRegistry.Infrastructure.Persistence.Sql;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Mapping;

public class SqlStatementBuilderTests
{
    private const string AllColumns =
        "[Id], [Name], [Description], [Kind], [Charges], [IsDeleted], [BuiltOn], [UpdatedAt], [Tags]";

    private static readonly EntityMetadata Gadgets = EntityMetadata.For(new GadgetMap());

    private static readonly DateTime UpdatedAt = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    private static Gadget Sample(string name = "Grappling hook") => Gadget.New(
        name,
        notes: null,
        kind: GadgetKind.Weapon,
        charges: 3,
        builtOn: new DateOnly(2020, 5, 17),
        updatedAt: UpdatedAt,
        tags: ["rope", "hook"],
        id: 7);

    // SELECT

    [Fact]
    public void SelectById_reads_one_active_row_by_key()
    {
        var statement = SqlStatementBuilder.SelectById(Gadgets, 7);

        statement.Sql.ShouldBe($"SELECT {AllColumns} FROM [dbo].[Gadgets] WHERE [Id] = @Id AND [IsDeleted] = 0");
        statement.Parameters.ShouldHaveSingleItem().ShouldBe(new SqlParameterValue("@Id", 7, DbType.Int32, 0));
    }

    [Fact]
    public void SelectAll_reads_every_active_row_in_key_order()
    {
        var statement = SqlStatementBuilder.SelectAll(Gadgets);

        statement.Sql.ShouldBe($"SELECT {AllColumns} FROM [dbo].[Gadgets] WHERE [IsDeleted] = 0 ORDER BY [Id]");
        statement.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void Without_soft_delete_no_IsDeleted_filter_is_added()
    {
        var statement = SqlStatementBuilder.SelectAll(EntityMetadata.For(new PlainGadgetMap()));

        statement.Sql.ShouldNotContain("IsDeleted] = 0");
    }

    // INSERT

    [Fact]
    public void Insert_writes_every_column_except_the_identity_key_and_returns_the_new_id()
    {
        var statement = SqlStatementBuilder.Insert(Gadgets, Sample());

        statement.Sql.ShouldBe(
            "INSERT INTO [dbo].[Gadgets] ([Name], [Description], [Kind], [Charges], [IsDeleted], [BuiltOn], [UpdatedAt], [Tags]) " +
            "OUTPUT INSERTED.[Id] " +
            "VALUES (@Name, @Description, @Kind, @Charges, @IsDeleted, @BuiltOn, @UpdatedAt, @Tags)");
    }

    [Fact]
    public void Insert_parameters_carry_converted_values_with_explicit_types()
    {
        var parameters = SqlStatementBuilder.Insert(Gadgets, Sample()).Parameters.ToDictionary(p => p.Name);

        parameters["@Name"].ShouldBe(new SqlParameterValue("@Name", "Grappling hook", DbType.String, 50));
        parameters["@Description"].ShouldBe(new SqlParameterValue("@Description", null, DbType.String, 200));
        parameters["@Kind"].ShouldBe(new SqlParameterValue("@Kind", 2, DbType.Int32, 0));
        parameters["@Charges"].ShouldBe(new SqlParameterValue("@Charges", 3, DbType.Int32, 0));
        parameters["@IsDeleted"].ShouldBe(new SqlParameterValue("@IsDeleted", false, DbType.Boolean, 0));
        parameters["@BuiltOn"].ShouldBe(new SqlParameterValue("@BuiltOn", new DateTime(2020, 5, 17), DbType.Date, 0));
        parameters["@UpdatedAt"].ShouldBe(new SqlParameterValue("@UpdatedAt", UpdatedAt, DbType.DateTime2, 0));
        parameters["@Tags"].ShouldBe(new SqlParameterValue("@Tags", """["rope","hook"]""", DbType.String, -1));
        parameters.ShouldNotContainKey("@Id");
    }

    // UPDATE

    [Fact]
    public void Update_changes_every_column_except_the_key_insert_only_columns_and_the_soft_delete_flag()
    {
        var statement = SqlStatementBuilder.Update(Gadgets, Sample());

        statement.Sql.ShouldBe(
            "UPDATE [dbo].[Gadgets] SET [Name] = @Name, [Description] = @Description, [Kind] = @Kind, " +
            "[Charges] = @Charges, [UpdatedAt] = @UpdatedAt, [Tags] = @Tags " +
            "WHERE [Id] = @Id AND [IsDeleted] = 0");
        statement.Parameters.Select(p => p.Name).ShouldBe(
            ["@Name", "@Description", "@Kind", "@Charges", "@UpdatedAt", "@Tags", "@Id"]);
    }

    [Fact]
    public void Update_never_touches_the_creation_audit_of_a_hero()
    {
        var hero = SuperHero.Create(
            new SuperHeroDetails("Superman", "Clark", "Kent", new DateOnly(1977, 6, 18), "superman@heroregistry.com",
                ["Flight"], 100, "344 Clinton Street", "Apt 3D", "Metropolis", "NY", "10001"),
            createdBy: 1,
            UpdatedAt);

        var sql = SqlStatementBuilder.Update(EntityMetadata.For(new SuperHeroMap()), hero).Sql;

        sql.ShouldNotContain("[CreatedBy]");
        sql.ShouldNotContain("[CreatedAt]");
        sql.ShouldContain("[UpdatedBy] = @UpdatedBy");
    }

    // SOFT DELETE

    [Fact]
    public void SoftDelete_sets_the_flag_and_its_audit_columns_on_an_active_row()
    {
        var statement = SqlStatementBuilder.SoftDelete(Gadgets, Sample());

        statement.Sql.ShouldBe(
            "UPDATE [dbo].[Gadgets] SET [IsDeleted] = 1, [UpdatedAt] = @UpdatedAt WHERE [Id] = @Id AND [IsDeleted] = 0");
        statement.Parameters.Select(p => p.Name).ShouldBe(["@UpdatedAt", "@Id"]);
    }

    [Fact]
    public void SoftDelete_requires_a_soft_delete_mapping()
    {
        Should.Throw<InvalidOperationException>(
            () => SqlStatementBuilder.SoftDelete(EntityMetadata.For(new PlainGadgetMap()), Sample()));
    }

    // SQL injection

    [Theory]
    [InlineData("'; DROP TABLE Users;--")]
    [InlineData("Robert'); DELETE FROM SuperHeroes;--")]
    [InlineData("]; EXEC xp_cmdshell 'dir';--")]
    public void User_values_only_ever_appear_in_parameters(string malicious)
    {
        var gadget = Sample(name: malicious);

        foreach (var statement in new[]
                 {
                     SqlStatementBuilder.Insert(Gadgets, gadget),
                     SqlStatementBuilder.Update(Gadgets, gadget),
                 })
        {
            statement.Sql.ShouldNotContain(malicious);
            statement.Parameters.ShouldContain(p => Equals(p.Value, malicious));
        }
    }

    [Fact]
    public void No_value_of_a_real_hero_ever_reaches_the_SQL_text()
    {
        // Every free-text field carries a payload; the domain accepts them as ordinary text.
        var hero = SuperHero.Create(
            new SuperHeroDetails(
                HeroName: "x'); DROP TABLE Users;--",
                FirstName: "' OR '1'='1",
                LastName: "]; SHUTDOWN;--",
                BirthDate: new DateOnly(1990, 1, 1),
                Email: "bobby@tables.com",
                Superpowers: ["'; EXEC xp_cmdshell 'dir';--"],
                PowerLevel: 50,
                Address1: "1; DELETE FROM SuperHeroes",
                Address2: "/* comment */",
                City: "' UNION SELECT Password FROM Users--",
                State: "NY",
                ZipCode: "10001"),
            createdBy: 1,
            UpdatedAt);
        hero.SoftDelete(1, UpdatedAt.AddHours(1));
        var heroes = EntityMetadata.For(new SuperHeroMap());

        var values = heroes.Columns
            .Select(c => c.Property.GetValue(hero))
            .OfType<string>()
            .Concat(hero.Superpowers)
            .ToList();

        foreach (var statement in new[]
                 {
                     SqlStatementBuilder.Insert(heroes, hero),
                     SqlStatementBuilder.Update(heroes, hero),
                     SqlStatementBuilder.SoftDelete(heroes, hero),
                     SqlStatementBuilder.SelectById(heroes, hero.Id),
                     SqlStatementBuilder.SelectAll(heroes),
                 })
        {
            values.ShouldAllBe(value => !statement.Sql.Contains(value), $"in: {statement.Sql}");
        }
    }

    [Fact]
    public void Parameter_builds_a_typed_parameter_for_a_custom_query()
    {
        SqlStatementBuilder.Parameter(Gadgets.FindColumn("Name")!, "hook", "@Search")
            .ShouldBe(new SqlParameterValue("@Search", "hook", DbType.String, 50));
    }

    private sealed class PlainGadgetMap : EntityMap<Gadget>
    {
        public PlainGadgetMap()
        {
            ToTable("PlainGadgets");
            HasKey(g => g.Id);
            Ignore(g => g.Secret);
            Ignore(g => g.Tags); // a list needs StoredAsJson(); this map has no JSON column
        }
    }
}
