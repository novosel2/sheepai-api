using Microsoft.EntityFrameworkCore;
using SheepAI.Domain.Entities;

namespace SheepAI.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<CityFile> Files => Set<CityFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.CreatedAt).IsRequired();

            e.HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.Token).IsRequired();
            e.HasIndex(rt => rt.Token).IsUnique();
            e.Property(rt => rt.ExpiresAt).IsRequired();
            e.Property(rt => rt.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Chat>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.CreatedAt).IsRequired();
            e.Property(c => c.IsUrgent).IsRequired().HasDefaultValue(false);
            e.Property(c => c.IsAdminTaken).IsRequired().HasDefaultValue(false);
            e.Property(c => c.LastMessageAt).IsRequired();

            e.HasMany(c => c.Messages)
                .WithOne(m => m.Chat)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.CreatedAt).IsRequired();
            e.Property(m => m.Role).IsRequired();
            e.Property(m => m.Content).IsRequired();
        });

        modelBuilder.Entity<CityFile>(e =>
        {
            e.ToTable("files");
            e.HasKey(f => f.Id);
            e.Property(f => f.CreatedAt).IsRequired();
            e.Property(f => f.Name).IsRequired();
            e.Property(f => f.Category).IsRequired().HasDefaultValue("General");
            e.Property(f => f.AnthropicFileId).IsRequired();
            e.HasIndex(f => f.AnthropicFileId).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
