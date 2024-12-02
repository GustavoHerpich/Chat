using Chat.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chat.Data.Map;

public class MessageMap : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages"); 
        builder.HasKey(m => m.Id); 
        builder.Property(m => m.Id).ValueGeneratedOnAdd(); 
        builder.Property(m => m.Sender)
            .IsRequired() 
            .HasMaxLength(50); 

        builder.Property(m => m.Content)
            .IsRequired() 
            .HasMaxLength(500); 

        builder.Property(m => m.Timestamp)
            .IsRequired(); 

        builder.HasOne<ChatSession>()
            .WithMany(c => c.Messages) 
            .HasForeignKey(m => m.ChatId) 
            .OnDelete(DeleteBehavior.Cascade);
    }
}
