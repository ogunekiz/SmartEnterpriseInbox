using Microsoft.EntityFrameworkCore;
using SmartEnterpriseInbox.Core;

namespace SmartEnterpriseInbox.Infrastructure
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
		{
		}

		public DbSet<CustomerRequest> CustomerRequests { get; set; }
		public DbSet<OutboxMessage> OutboxMessages { get; set; }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<CustomerRequest>(entity =>
			{
				entity.HasKey(e => e.Id);

				entity.HasIndex(e => e.Category);
				entity.HasIndex(e => e.Urgency);
				entity.HasIndex(e => e.IsProcessed);
			});
		}
	}
}