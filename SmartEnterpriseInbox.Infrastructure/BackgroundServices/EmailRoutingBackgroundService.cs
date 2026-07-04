using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using MailKit.Net.Smtp;
using MimeKit;
using SmartEnterpriseInbox.Core;

namespace SmartEnterpriseInbox.Infrastructure
{
	public class EmailRoutingBackgroundService : BackgroundService
	{
		private readonly IConfiguration _configuration;

		public EmailRoutingBackgroundService(IConfiguration configuration)
		{
			_configuration = configuration;
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

			var consumer = new AsyncEventingBasicConsumer(channel);

			consumer.ReceivedAsync += async (model, ea) =>
			{
				var body = ea.Body.ToArray();
				var messageJson = Encoding.UTF8.GetString(body);

				try
				{
					var emailEvent = JsonSerializer.Deserialize<EmailRoutingEvent>(messageJson);
					if (emailEvent != null)
					{
						Console.WriteLine($"[Routing Service] Kuyruktan mesaj alındı. Kategori: {emailEvent.Category}");

						await SendRoutingEmailAsync(emailEvent);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[Routing Service Hata] Mesaj işlenemedi: {ex.Message}");
				}
			};

			await channel.BasicConsumeAsync("email_routing_queue", autoAck: true, consumer: consumer, cancellationToken: stoppingToken);

			await Task.Delay(Timeout.Infinite, stoppingToken);
		}

		private async Task SendRoutingEmailAsync(EmailRoutingEvent ev)
		{
			string targetConfigKey = ev.Category switch
			{
				"İnsan Kaynakları" or "IK" or "HR" => "EmailSettings:Departments:InsanKaynaklari",
				"Muhasebe" or "Finance" or "Finans" => "EmailSettings:Departments:Muhasebe",
				"Teknik Destek" or "IT" => "EmailSettings:Departments:TeknikDestek",
				_ => "EmailSettings:Departments:Belirsiz"
			};

			string targetEmail = _configuration[targetConfigKey] ?? _configuration["EmailSettings:Departments:Belirsiz"]!;

			var smtpServer = _configuration["EmailSettings:SmtpServer"];
			var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
			var senderEmail = _configuration["EmailSettings:SenderEmail"];
			var appPassword = _configuration["EmailSettings:AppPassword"];

			var message = new MimeMessage();
			message.From.Add(new MailboxAddress("Smart Enterprise Inbox", senderEmail));
			message.To.Add(new MailboxAddress($"{ev.Category} Departmanı", targetEmail));
			message.Subject = $"[OTOMATİK YÖNLENDİRME] [{ev.Urgency} Önem] - {ev.Category} Talebi";

			var bodyBuilder = new BodyBuilder();
			bodyBuilder.HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; border: 1px solid #ddd; padding: 20px; border-radius: 8px;'>
                    <h2 style='color: #2c3e50;'>Yapay Zeka Otomatik E-Posta Yönlendirmesi</h2>
                    <p><strong>Gönderen Müşteri:</strong> {ev.Sender}</p>
                    <p><strong>Tespit Edilen Kategori:</strong> <span style='background: #e74c3c; color: #fff; padding: 3px 8px; border-radius: 4px;'>{ev.Category}</span></p>
                    <p><strong>Aciliyet Durumu:</strong> {ev.Urgency}</p>
                    <hr style='border: 0; border-top: 1px solid #eee;' />
                    
                    <h3>Yapay Zeka Özeti:</h3>
                    <p style='background: #f9f9f9; padding: 10px; border-left: 4px solid #3498db;'>{ev.Summary}</p>
                    
                    <h3>Önerilen Aksiyon Planı:</h3>
                    <p style='background: #f9f9f9; padding: 10px; border-left: 4px solid #2ecc71;'>{ev.ActionPlan}</p>
                    <hr style='border: 0; border-top: 1px solid #eee;' />
                    
                    <h3>✉️ Orijinal E-Posta İçeriği:</h3>
                    <blockquote style='font-style: italic; color: #555;'>{ev.Content}</blockquote>
                </div>";

			message.Body = bodyBuilder.ToMessageBody();

			using (var client = new SmtpClient())
			{
				await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
				await client.AuthenticateAsync(senderEmail, appPassword);
				await client.SendAsync(message);
				await client.DisconnectAsync(true);
			}

			Console.WriteLine($"[Routing Service SUCCESS] Mail başarıyla {ev.Category} departmanına ({targetEmail}) iletildi.");
		}
	}
}