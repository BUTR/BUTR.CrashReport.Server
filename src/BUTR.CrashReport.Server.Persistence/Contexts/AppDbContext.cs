using BUTR.CrashReport.Server.Contexts.Config;
using BUTR.CrashReport.Server.Models.Database;

using Microsoft.EntityFrameworkCore;

using System;
using System.Linq;

namespace BUTR.CrashReport.Server.Contexts;

public class AppDbContext : DbContext
{
    public DbSet<ReportEntity> ReportEntities { get; set; }
    public DbSet<IdAliasEntity> IdAliasEntities { get; set; }
    public DbSet<HtmlEntity> HtmlEntities { get; set; }
    public DbSet<JsonEntity> JsonEntities { get; set; }
    public DbSet<OldHtmlEntity> OldHtmlEntities { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Maps the <c>insert_crash_report</c> table-valued function. Tries each candidate file id until one is free
    /// within the tenant, inserts the report and its optional html/json, and returns the assigned id in one round
    /// trip. The function body is created/updated by migrations (see CrashReportInsertFunction).
    /// </summary>
    public IQueryable<CrashReportInsertResult> InsertCrashReport(
        Guid crashReportId, byte tenant, byte version, DateTime created,
        byte[] deleteTokenHash, string[] fileIds, byte[]? htmlCompressed, string? json) =>
        FromExpression(() => InsertCrashReport(crashReportId, tenant, version, created, deleteTokenHash, fileIds, htmlCompressed, json));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ReportEntityConfiguration());
        modelBuilder.ApplyConfiguration(new IdAliasEntityConfiguration());
        modelBuilder.ApplyConfiguration(new HtmlEntityConfiguration());
        modelBuilder.ApplyConfiguration(new JsonEntityConfiguration());
        modelBuilder.ApplyConfiguration(new OldHtmlEntityConfiguration());

        // Keyless result of the insert_crash_report table-valued function (mapped via HasDbFunction below).
        // ToView(null) keeps EF from scaffolding a table for it - it's only ever read via the function.
        modelBuilder.Entity<CrashReportInsertResult>(b =>
        {
            b.HasNoKey();
            b.ToView(null);
            b.Property(x => x.FileId).HasColumnName("o_file_id");
            b.Property(x => x.Tenant).HasColumnName("o_tenant");
            b.Property(x => x.Created).HasColumnName("o_created");
        });

        // Map the InsertCrashReport method to the database function. The function body is created by migrations; this
        // only tells EF how to translate the call. No parameter store types are configured: HasStoreType on a TVF
        // parameter is not honored by the provider, so the function's parameters are typed to match what EF sends for
        // each CLR type (Guid->uuid, byte->smallint, DateTime->timestamp with time zone, string[]->text[],
        // byte[]->bytea, string->text). The json string arrives as text and the function casts it to the column type.
        modelBuilder.HasDbFunction(typeof(AppDbContext).GetMethod(nameof(InsertCrashReport))!).HasName(CrashReportInsertFunction.FunctionName);

        // Enable the insert_crash_report function. The actual SQL is generated from the fully-resolved relational
        // model (table/column names AND store types) by CrashReportAnnotationProvider; the migrations differ then
        // recreates the function whenever any referenced mapping changes. See CrashReportMigrationsSqlGenerator.
        modelBuilder.HasAnnotation(CrashReportInsertFunction.MarkerAnnotationName, true);
    }
}