﻿using AuthenticationAppMVC.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC.Data
{
    public class AppDBContext:IdentityDbContext<User>
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options)
        {
            
        }

        public DbSet<User> Users {  get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }

        public DbSet<FileAttachment> FileAttachments { get; set; }

        public DbSet<FriendRequest> FriendRequests { get; set; }

        public DbSet<FriendShip> FriendShips { get; set; }

        public DbSet<GroupMessageReadStatus> GroupMessageReadStatuses { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Cấu hình quan hệ giữa GroupMember và Group
            builder.Entity<GroupMember>()
                .HasOne(gm => gm.Group)
                .WithMany()
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // Cấu hình quan hệ giữa GroupMember và User
            builder.Entity<GroupMember>()
                .HasOne(gm => gm.User)
                .WithMany()
                .HasForeignKey(gm => gm.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Cấu hình quan hệ giữa GroupMessage và Group
            builder.Entity<GroupMessage>()
                .HasOne(gm => gm.Group)
                .WithMany()
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // Cấu hình quan hệ giữa GroupMessage và User (Sender)
            builder.Entity<GroupMessage>()
                .HasOne(gm => gm.Sender)
                .WithMany()
                .HasForeignKey(gm => gm.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            // Cấu hình quan hệ giữa Message và Files
            builder.Entity<Message>()
                .HasMany(m => m.Attachments)
                .WithOne(a => a.Message)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Cấu hình quan hệ giữa GroupMessage và Files
            builder.Entity<GroupMessage>()
                .HasMany(m => m.Attachments)
                .WithOne(a => a.GroupMessage)
                .HasForeignKey(a => a.GroupMessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Cấu hình quan hệ giữa GroupMessage và GroupMessageReadStatus
            builder.Entity<GroupMessage>()
                .HasMany(gm => gm.ReadBys)
                .WithOne(r => r.GroupMessage)
                .HasForeignKey(r => r.GroupMessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình quan hệ giữa GroupMessageReadStatus và User
            builder.Entity<GroupMessageReadStatus>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);


            // Cấu hình quan hệ trong giữa FriendRequest và User
            builder.Entity<FriendRequest>()
               .HasKey(fr => fr.Id);

            builder.Entity<FriendRequest>()
                .HasOne(fr => fr.Sender)
                .WithMany()
                .HasForeignKey(fr => fr.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<FriendRequest>()
                .HasOne(fr => fr.Receiver)
                .WithMany()
                .HasForeignKey(fr => fr.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            // Cấu hình quan hệ giữa FriendShip và User
            builder.Entity<FriendShip>()
               .HasKey(fr => fr.Id);

            builder.Entity<FriendShip>()
                .HasOne(fs => fs.User1)
                .WithMany()
                .HasForeignKey(fs => fs.User1Id)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<FriendShip>()
                .HasOne(fs => fs.User2)
                .WithMany()
                .HasForeignKey(fs => fs.User2Id)
                .OnDelete(DeleteBehavior.NoAction);

            // Đảm bảo unique cho mỗi mqh bạn bè
            builder.Entity<FriendShip>()
                .HasIndex(f => new { f.User1Id, f.User2Id })
                .IsUnique();
        }
    }
}
