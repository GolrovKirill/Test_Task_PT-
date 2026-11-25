using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PT.Domain.Entities;

namespace PT.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ValueComparer<Dictionary<string, string>> MetadataComparer = new(
        (left, right) =>
            ReferenceEquals(left, right) ||
            (left != null &&
             right != null &&
             left.Count == right.Count &&
             left.All(pair => right.ContainsKey(pair.Key) && right[pair.Key] == pair.Value)),
        value => value.Aggregate(
            0,
            (hash, pair) => HashCode.Combine(
                hash,
                pair.Key.GetHashCode(StringComparison.Ordinal),
                pair.Value.GetHashCode(StringComparison.Ordinal))),
        value => new Dictionary<string, string>(value));

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureDocument(modelBuilder);
        ConfigureOutbox(modelBuilder);
    }

    private static void ConfigureDocument(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<Document>();

        document.ToTable("Documents");
        document.HasKey(x => x.Id);

        document.Property(x => x.Title).HasMaxLength(200).IsRequired();
        document.Property(x => x.Description).HasMaxLength(2000);
        document.Property(x => x.Content).HasMaxLength(20000).IsRequired();
        document.Property(x => x.OwnerEmail).HasMaxLength(254).IsRequired();
        document.Property(x => x.QualityScore).HasPrecision(4, 2);

        document.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(50);
        document.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
        document.Property(x => x.Confidentiality).HasConversion<string>().HasMaxLength(50);

        // Npgsql: нативные массивы; metadata — jsonb через конвертер.
        document.Property(x => x.Tags).HasColumnType("text[]").IsRequired();
        document.Property(x => x.RelatedDocumentIds).HasColumnType("uuid[]").IsRequired();

        var metadataConverter = new ValueConverter<Dictionary<string, string>, string>(
            value => JsonSerializer.Serialize(value, MetadataJsonOptions),
            value => JsonSerializer.Deserialize<Dictionary<string, string>>(value, MetadataJsonOptions)
                     ?? new Dictionary<string, string>());

        document.Property(x => x.Metadata)
            .HasConversion(metadataConverter, MetadataComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        // Внутренняя колонка для регистронезависимой уникальности title.
        document.Property<string>("TitleKey")
            .HasComputedColumnSql("lower(\"Title\")", stored: true);
        document.HasIndex("TitleKey").IsUnique();

        document.HasIndex(x => x.CreatedAt);
        document.HasIndex(x => x.Status);
    }

    private static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        var outbox = modelBuilder.Entity<OutboxMessage>();

        outbox.ToTable("OutboxMessages");
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.Sequence).UseIdentityByDefaultColumn();
        outbox.HasIndex(x => x.Sequence).IsUnique();
        outbox.Property(x => x.Type).HasMaxLength(100).IsRequired();
        outbox.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        outbox.HasIndex(x => new { x.ProcessedOn, x.NextAttemptOn, x.Sequence });
    }
}
