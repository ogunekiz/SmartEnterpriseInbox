using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System.Text;

namespace SmartEnterpriseInbox.Infrastructure
{
	public class OutboxPublisherBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;

		public OutboxPublisherBackgroundService(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var factory = new ConnectionFactory() { HostName = "localhost" };
			using var connection = await factory.CreateConnectionAsync(stoppingToken);
			using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

			await channel.QueueDeclareAsync(
					queue: "email_routing_queue",
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null,
					cancellationToken: stoppingToken);

			while (!stoppingToken.IsCancellationRequested)
			{
				using (var scope = _serviceProvider.CreateScope())
				{
					var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

					var pendingMessages = await dbContext.OutboxMessages
							.Where(m => m.ProcessedOn == null)
							.OrderBy(m => m.OccurredOn)
							.Take(20)
							.ToListAsync(stoppingToken);

					if (pendingMessages.Any())
					{
						foreach (var message in pendingMessages)
						{
							try
							{
								var body = Encoding.UTF8.GetBytes(message.Content);

								await channel.BasicPublishAsync(
										exchange: string.Empty,
										routingKey: "email_routing_queue",
										body: body,
										cancellationToken: stoppingToken);

								message.ProcessedOn = DateTime.UtcNow;
							}
							catch (Exception ex)
							{
								Console.WriteLine($"[Outbox Hata] Mesaj kuyruğa atılamadı: {ex.Message}");
							}
						}

						await dbContext.SaveChangesAsync(stoppingToken);
					}
				}

				await Task.Delay(5000, stoppingToken);
			}
		}
	}
}