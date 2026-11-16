using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

// Одиниця роботи EF Core: відстежує завантажені рядки й на SaveChanges формує INSERT / UPDATE / DELETE.
// Не потокобезпечний, тож в Api — Scoped (свій екземпляр на HTTP-запит), див. Program.cs.
// Схему описує Fluent API нижче, а не атрибути на Row-класах: усе про БД зібрано в одному місці.
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const int IdLength = 32;

    public DbSet<BookRow> Books => Set<BookRow>();
    public DbSet<CopyRow> Copies => Set<CopyRow>();
    public DbSet<LoanRow> Loans => Set<LoanRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BookRow>(book =>
        {
            book.ToTable("Books");
            book.HasKey(b => b.Id);
            // NOCASE: «b-001» і «B-001» — один запис, як у InMemoryBookStore (StringComparer.OrdinalIgnoreCase).
            book.Property(b => b.Id).HasMaxLength(IdLength).UseCollation("NOCASE");
            book.Property(b => b.Isbn).IsRequired().HasMaxLength(20);
            book.Property(b => b.IsbnKey).IsRequired().HasMaxLength(13);
            book.Property(b => b.Title).IsRequired().HasMaxLength(200);
            book.Property(b => b.Author).HasMaxLength(200);   // необов'язкове: у домені Author — string?
            book.Property(b => b.Year).IsRequired();

            book.HasIndex(b => b.IsbnKey).IsUnique();          // унікальний ISBN — гарантія вже на рівні БД

            // 1:N — одне видання, багато примірників. Видалили видання — зникли й примірники (Cascade).
            book.HasMany(b => b.Copies)
                .WithOne(c => c.Book)
                .HasForeignKey(c => c.BookId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CopyRow>(copy =>
        {
            copy.ToTable("Copies");
            copy.HasKey(c => c.Id);
            copy.Property(c => c.Id).HasMaxLength(IdLength).UseCollation("NOCASE");
            copy.Property(c => c.BookId).IsRequired().HasMaxLength(IdLength).UseCollation("NOCASE");
            copy.Property(c => c.IsIssued).IsRequired();
            copy.Property(c => c.RegisteredAt).IsRequired();

            // 1:N — примірник і його видачі. Restrict: примірник з історією видач видалити не можна —
            // те саме правило, що CatalogService перевіряє до видалення; БД — останній рубіж.
            copy.HasMany(c => c.Loans)
                .WithOne(l => l.Copy)
                .HasForeignKey(l => l.CopyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanRow>(loan =>
        {
            loan.ToTable("Loans");
            loan.HasKey(l => l.Id);
            loan.Property(l => l.Id).HasMaxLength(IdLength).UseCollation("NOCASE");
            loan.Property(l => l.CopyId).IsRequired().HasMaxLength(IdLength).UseCollation("NOCASE");
            loan.Property(l => l.ReaderId).IsRequired().HasMaxLength(IdLength);
            loan.Property(l => l.IssuedOn).IsRequired();
            loan.Property(l => l.Status).IsRequired().HasConversion<string>().HasMaxLength(16); // "Open", а не 0
            loan.Property(l => l.LoanDays).IsRequired();
            loan.HasIndex(l => l.ReaderId);                     // «видачі читача» — частий запит
        });
    }
}
