using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Fatty
{
    public class LoggingContext : DbContext
    {
        public DbSet<IrcLogUser> Users { get; set; }
        public DbSet<ChannelMessageLog> Messages { get; set; }
        public DbSet<ServerLog> Servers { get; set; }
        public DbSet<ChannelLog> Channels { get; set; }

        public LoggingContext(DbContextOptions<LoggingContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IrcLogUser>().HasKey(x => new { x.Nick, x.ServerId });
        }
    }

    public class IrcLogUser
    {
        [Key]
        public string Nick { get; set; }
        [Key, ForeignKey("Server")]
        public int ServerId { get; set; }
        public ServerLog Server { get; set; }
        public int UserId { get; set; }

        public IrcLogUser(string nick, int serverId)
        {
            Nick = nick;
            ServerId = serverId;
        }
    }

    public class ChannelMessageLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public IrcLogUser User { get; set; }
        [Required]
        public ChannelLog Channel { get; set; }
        [Required]
        public string Message { get; set; }
        [Required]
        public DateTime Date { get; set; }
    }

    public class ServerLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public string ServerName { get; set; }

        public ServerLog(string serverName)
        {
            ServerName = serverName;
        }
    }

    public class ChannelLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Required]
        public string ChannelName { get; set; }
        [Required]
        public ServerLog Server { get; set; }

        //public ChannelLog(string channelName, ServerLog server)
        //{
        //    ChannelName = channelName;
        //    Server = server;
        //}
    }

    public class BloggingContextFactory : IDesignTimeDbContextFactory<LoggingContext>
    {
        public LoggingContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LoggingContext>();
            optionsBuilder.UseSqlite("Data Source=Logging.db");

            return new LoggingContext(optionsBuilder.Options);
        }
    }
}