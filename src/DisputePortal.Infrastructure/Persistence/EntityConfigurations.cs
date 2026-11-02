using DisputePortal.Domain.Accounts;
using DisputePortal.Domain.Disputes;
using DisputePortal.Domain.Transactions;
using DisputePortal.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DisputePortal.Infrastructure.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.FullName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.AccountNumber).HasMaxLength(34).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Ignore(a => a.MaskedAccountNumber);
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Currency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(t => t.Merchant).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Reference).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasOne(t => t.Account).WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);

        // Supports the default "newest first per account" listing.
        builder.HasIndex(t => new { t.AccountId, t.PostedAt }).IsDescending(false, true);
    }
}

internal sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public const string OpenDisputePerTransactionIndex = "ix_disputes_open_per_transaction";
    public const string IdempotencyKeyIndex = "ix_disputes_customer_id_idempotency_key";

    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Description).HasMaxLength(Dispute.DescriptionMaxLength).IsRequired();
        builder.Property(d => d.ResolutionNote).HasMaxLength(Dispute.NoteMaxLength);
        builder.Property(d => d.IdempotencyKey).HasMaxLength(100);
        builder.Property(d => d.Version).IsRowVersion();
        builder.Ignore(d => d.IsOpen);

        builder.HasOne(d => d.Transaction).WithMany().HasForeignKey(d => d.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.CustomerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.History).WithOne().HasForeignKey(h => h.DisputeId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Backstop for the application-level check: at most one open dispute per transaction, even under races.
        builder.HasIndex(d => d.TransactionId)
            .HasDatabaseName(OpenDisputePerTransactionIndex)
            .IsUnique()
            .HasFilter($"status IN ('{nameof(DisputeStatus.Submitted)}', '{nameof(DisputeStatus.UnderReview)}')");

        builder.HasIndex(d => new { d.CustomerId, d.IdempotencyKey })
            .HasDatabaseName(IdempotencyKeyIndex)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");

        builder.HasIndex(d => new { d.CustomerId, d.CreatedAt });
        builder.HasIndex(d => new { d.Status, d.CreatedAt });
    }
}

internal sealed class DisputeStatusChangeConfiguration : IEntityTypeConfiguration<DisputeStatusChange>
{
    public void Configure(EntityTypeBuilder<DisputeStatusChange> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Note).HasMaxLength(Dispute.NoteMaxLength);
        builder.HasIndex(h => new { h.DisputeId, h.ChangedAt });
    }
}
