﻿using Microsoft.EntityFrameworkCore;
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

        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    //modelBuilder.Entity<IrcLogUser>().Property(u => u.Id).ValueGeneratedOnAdd();
        //}
    }


    public class IrcLogUser
    {
        [Key]
        public string Nick { get; set; }
        public int UserId { get; set; }
        public ServerLog Server { get; set; }

        public IrcLogUser(string nick)
        {
            Nick = nick;
        }
    }

    public class ChannelMessageLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public IrcLogUser User { get; set; }
        public ChannelLog Channel { get; set; }
        public string Message { get; set; }
        public DateTime Date { get; set; }
    }

    public class ServerLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
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
        public string ChannelName { get; set; }
        public ServerLog Server { get; set; }

        //public ChannelLog(string channelName, ServerLog server)
        //{
        //    ChannelName = channelName;
        //    Server = server;
        //}
    }
}