using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartEnterpriseInbox.Infrastructure
{
	public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext(string[] args)
		{
			var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "SmartEnterpriseInbox.WebApi");

			if (!Directory.Exists(basePath))
			{
				basePath = Directory.GetCurrentDirectory();
			}

			var configuration = new ConfigurationBuilder()
					.SetBasePath(basePath)
					.AddJsonFile("appsettings.json", optional: true)
					.AddEnvironmentVariables()
					.Build();

			var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

			var connectionString = configuration.GetConnectionString("DefaultConnection");

			// Use Npgsql for PostgreSQL
			optionsBuilder.UseNpgsql(connectionString);

			// Use SQL Server (uncomment the following line if you want to use SQL Server instead)
			// optionsBuilder.UseSqlServer(connectionString);

			return new AppDbContext(optionsBuilder.Options);
		}
	}
}