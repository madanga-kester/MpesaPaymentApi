using Microsoft.EntityFrameworkCore;

namespace MpesaPaymentApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MpesaTransaction> MpesaTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.CheckoutRequestID)
            .IsUnique()
            .HasFilter("[CheckoutRequestID] IS NOT NULL");

        modelBuilder.Entity<MpesaTransaction>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

       
        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.PhoneNumber);

        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.Status);

        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.CreatedAt);

        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.UserId);

        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.OriginClientId);
       
        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => new { e.UserId, e.Status, e.CreatedAt });
    }
}