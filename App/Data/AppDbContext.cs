using Microsoft.EntityFrameworkCore;

namespace MpesaPaymentApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MpesaTransaction> MpesaTransactions { get; set; } = null!;

    
    public DbSet<PayoutDetail> PayoutDetails { get; set; } = null!;
    public DbSet<FreelancerPayout> FreelancerPayouts { get; set; } = null!;

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

        modelBuilder.Entity<MpesaTransaction>()
            .HasIndex(e => e.RecipientFreelancerId);

        modelBuilder.Entity<PayoutDetail>()
            .HasIndex(e => e.UserId)
            .IsUnique();

        modelBuilder.Entity<FreelancerPayout>()
            .Property(e => e.GrossAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<FreelancerPayout>()
            .Property(e => e.PlatformFee)
            .HasPrecision(18, 2);

        modelBuilder.Entity<FreelancerPayout>()
            .Property(e => e.NetAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<FreelancerPayout>()
            .HasIndex(e => e.MpesaTransactionId);

        modelBuilder.Entity<FreelancerPayout>()
            .HasIndex(e => e.FreelancerId);

        modelBuilder.Entity<FreelancerPayout>()
            .HasIndex(e => e.Status);
    }
}