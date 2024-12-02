using Chat.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Data.Map
{
    public class ChatMap : IEntityTypeConfiguration<ChatSession>
    {
        public void Configure(EntityTypeBuilder<ChatSession> builder)
        {
            builder.ToTable("Chats");

            builder.HasKey(c => c.ChatId);

            builder.Property(c => c.ChatId)
                .IsRequired()
                .ValueGeneratedOnAdd();

            builder.Property(c => c.GroupName)
                .IsRequired();

            builder.HasMany(c => c.Participants)
                .WithMany(u => u.Chats)
                .UsingEntity(j => j.ToTable("ChatParticipants"));

            builder.HasMany(c => c.Messages)
                .WithOne()
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
