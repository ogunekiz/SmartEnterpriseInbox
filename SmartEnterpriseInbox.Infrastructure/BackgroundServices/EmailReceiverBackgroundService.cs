using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartEnterpriseInbox.Infrastructure.Services;

namespace SmartEnterpriseInbox.Infrastructure
{
	public class EmailReceiverBackgroundService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;

		public EmailReceiverBackgroundService(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			_serviceProvider = serviceProvider;
			_configuration = configuration;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var imapServer = _configuration["EmailSettings:ImapServer"];
			var imapPort = int.Parse(_configuration["EmailSettings:ImapPort"] ?? "993");
			var username = _configuration["EmailSettings:SenderEmail"];
			var password = _configuration["EmailSettings:AppPassword"];

			while (!stoppingToken.IsCancellationRequested)
			{
				using (var client = new ImapClient())
				{
					try
					{
						await client.ConnectAsync(imapServer, imapPort, true, stoppingToken);
						await client.AuthenticateAsync(username, password, stoppingToken);

						var inbox = client.Inbox;
						await inbox.OpenAsync(FolderAccess.ReadWrite, stoppingToken);

						var uids = await inbox.SearchAsync(SearchQuery.NotSeen, stoppingToken);

						if (uids.Any())
						{
							using (var scope = _serviceProvider.CreateScope())
							{
								var aiWorkflowService = scope.ServiceProvider.GetRequiredService<AiWorkflowService>();
								var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

								foreach (var uid in uids)
								{
									var message = await inbox.GetMessageAsync(uid, stoppingToken);

									string sender = message.From.Mailboxes.FirstOrDefault()?.Address ?? "Unknown";
									string content = string.IsNullOrWhiteSpace(message.TextBody) ? message.Subject : message.TextBody;

									Console.WriteLine($"[Gmail Worker] Yeni mail yakalandı: {sender} - {message.Subject}");

									using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
									try
									{
										var result = await aiWorkflowService.ProcessRequestAsync(sender, content);
										result.Request.TargetSystemStatus = "GMAIL_AUTOMATION_SYNCED";

										dbContext.CustomerRequests.Add(result.Request);
										dbContext.OutboxMessages.Add(result.Outbox);

										await dbContext.SaveChangesAsync(stoppingToken);
										await transaction.CommitAsync();

										await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, stoppingToken);
									}
									catch (Exception ex)
									{
										await transaction.RollbackAsync();
										Console.WriteLine($"[Gmail Worker Hata] Mail işlenirken DB hatası: {ex.Message}");
									}
								}
							}
						}

						await client.DisconnectAsync(true, stoppingToken);
					}
					catch (Exception ex)
					{
						Console.WriteLine($"[Gmail Worker Bağlantı Hatası]: {ex.Message}");
					}
				}

				await Task.Delay(30000, stoppingToken);
			}
		}
	}
}