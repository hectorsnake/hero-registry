using System.Data;
using HeroRegistry.Domain.Users;
using HeroRegistry.Infrastructure.Persistence.Mapping;
using HeroRegistry.Infrastructure.Persistence.Maps;

namespace HeroRegistry.Infrastructure.Tests.Persistence.Mapping;

/// <summary>
/// Maps rows to objects with Reflection, using a fake reader (DataTable.CreateDataReader) so no
/// database is needed.
/// </summary>
public class DataRecordMapperTests
{
    private static readonly EntityMetadata Gadgets = EntityMetadata.For(new GadgetMap());

    [Fact]
    public void Maps_a_row_to_an_entity_through_its_private_constructor_and_setters()
    {
        var gadget = MapSingle(GadgetRow());

        gadget.Id.ShouldBe(7);
        gadget.Name.ShouldBe("Grappling hook");
        gadget.Notes.ShouldBe("Fits in a belt");
        gadget.Kind.ShouldBe(GadgetKind.Weapon);
        gadget.Charges.ShouldBe(3);
        gadget.IsDeleted.ShouldBeFalse();
        gadget.BuiltOn.ShouldBe(new DateOnly(2020, 5, 17));
        gadget.Tags.ShouldBe(["rope", "hook"]);
    }

    [Fact]
    public void Database_nulls_become_null_for_nullable_properties()
    {
        var gadget = MapSingle(GadgetRow(notes: DBNull.Value, charges: DBNull.Value));

        gadget.Notes.ShouldBeNull();
        gadget.Charges.ShouldBeNull();
    }

    [Fact]
    public void DateTime_values_are_read_as_UTC()
    {
        // DATETIME2 has no time zone; the application always stores UTC.
        var gadget = MapSingle(GadgetRow(updatedAt: new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Unspecified)));

        gadget.UpdatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        gadget.UpdatedAt.ShouldBe(new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Column_names_are_matched_ignoring_case_and_extra_columns_are_ignored()
    {
        var table = GadgetTable();
        table.Columns["Name"]!.ColumnName = "NAME";
        table.Columns.Add("TotalCount", typeof(int)); // for example COUNT(*) OVER() in a paged query
        AddGadgetRow(table);
        table.Rows[0]["TotalCount"] = 42;

        MapSingle(table).Name.ShouldBe("Grappling hook");
    }

    [Fact]
    public void Columns_missing_from_the_result_keep_their_default_values()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "Partial");

        var gadget = MapSingle(table);

        gadget.Name.ShouldBe("Partial");
        gadget.Tags.ShouldBeEmpty();
    }

    [Fact]
    public void Maps_every_row_of_a_reader()
    {
        var table = GadgetTable();
        AddGadgetRow(table, id: 1, name: "First");
        AddGadgetRow(table, id: 2, name: "Second");

        using var reader = table.CreateDataReader();
        var mapper = new DataRecordMapper<Gadget>(Gadgets, reader);
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(mapper.Map(reader).Name);
        }

        names.ShouldBe(["First", "Second"]);
    }

    [Fact]
    public void A_null_in_a_non_nullable_column_is_reported_with_the_column_name()
    {
        var exception = Should.Throw<InvalidOperationException>(() => MapSingle(GadgetRow(name: DBNull.Value)));

        exception.Message.ShouldContain("Name");
        exception.Message.ShouldContain(nameof(Gadget));
    }

    [Fact]
    public void A_value_of_the_wrong_type_is_reported_with_the_column_name()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Charges", typeof(string));
        table.Rows.Add(1, "not a number");

        Should.Throw<InvalidOperationException>(() => MapSingle(table)).Message.ShouldContain("Charges");
    }

    [Fact]
    public void Numeric_values_are_converted_to_the_property_type()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long)); // e.g. a BIGINT or a SCOPE_IDENTITY() decimal
        table.Columns.Add("Charges", typeof(short));
        table.Rows.Add(5L, (short)9);

        var gadget = MapSingle(table);

        gadget.Id.ShouldBe(5);
        gadget.Charges.ShouldBe(9);
    }

    [Fact]
    public void Maps_the_User_domain_entity_including_base_class_audit_properties()
    {
        var table = new DataTable();
        foreach (var (name, type) in new (string, Type)[]
                 {
                     ("Id", typeof(int)), ("UserName", typeof(string)), ("Email", typeof(string)),
                     ("Password", typeof(string)), ("IsAdmin", typeof(bool)), ("IsDeleted", typeof(bool)),
                     ("CreatedBy", typeof(int)), ("CreatedAt", typeof(DateTime)),
                     ("UpdatedBy", typeof(int)), ("UpdatedAt", typeof(DateTime)),
                 })
        {
            table.Columns.Add(name, type);
        }

        table.Rows.Add(1, "Admin", "admin@heroregistry.com", "$2a$11$hash", true, false,
            DBNull.Value, new DateTime(2026, 11, 22), DBNull.Value, DBNull.Value);

        using var reader = table.CreateDataReader();
        reader.Read();
        var user = new DataRecordMapper<User>(EntityMetadata.For(new UserMap()), reader).Map(reader);

        user.Id.ShouldBe(1);
        user.UserName.ShouldBe("Admin");
        user.IsAdmin.ShouldBeTrue();
        user.CreatedBy.ShouldBeNull();
        user.CreatedAt.ShouldBe(new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc));
        user.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void The_mapper_requires_metadata_for_its_entity_type()
    {
        using var reader = GadgetTable().CreateDataReader();

        Should.Throw<ArgumentException>(() => new DataRecordMapper<User>(Gadgets, reader));
    }

    private static Gadget MapSingle(DataTable table)
    {
        using var reader = table.CreateDataReader();
        reader.Read().ShouldBeTrue();
        return new DataRecordMapper<Gadget>(Gadgets, reader).Map(reader);
    }

    private static DataTable GadgetRow(
        object? name = null, object? notes = null, object? charges = null, object? updatedAt = null)
    {
        var table = GadgetTable();
        AddGadgetRow(table, name: name, notes: notes, charges: charges, updatedAt: updatedAt);
        return table;
    }

    private static DataTable GadgetTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("Kind", typeof(int));
        table.Columns.Add("Charges", typeof(int));
        table.Columns.Add("IsDeleted", typeof(bool));
        table.Columns.Add("BuiltOn", typeof(DateTime)); // SqlDataReader returns DATE as DateTime
        table.Columns.Add("UpdatedAt", typeof(DateTime));
        table.Columns.Add("Tags", typeof(string));
        return table;
    }

    private static void AddGadgetRow(
        DataTable table, int id = 7, object? name = null, object? notes = null, object? charges = null, object? updatedAt = null)
    {
        table.Rows.Add(
            id,
            name ?? "Grappling hook",
            notes ?? "Fits in a belt",
            (int)GadgetKind.Weapon,
            charges ?? 3,
            false,
            new DateTime(2020, 5, 17),
            updatedAt ?? new DateTime(2026, 11, 22, 10, 0, 0),
            """["rope","hook"]""");
    }
}
