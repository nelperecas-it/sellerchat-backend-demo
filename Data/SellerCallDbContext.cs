using Microsoft.EntityFrameworkCore;
using SCIABackendDemo.Models;

namespace SCIABackendDemo.Data
{
    public class SellerCallDbContext : DbContext
    {
        public SellerCallDbContext(DbContextOptions<SellerCallDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Prompt> Prompts => Set<Prompt>();
        public DbSet<CallHistory> CallHistories => Set<CallHistory>();
        public DbSet<ScheduledCall> ScheduledCalls => Set<ScheduledCall>();
         public DbSet<CallMappingEntry> CallMappings => Set<CallMappingEntry>();
        
    }
}
