using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartEnterpriseInbox.Core;
using System.Text.Json;

namespace SmartEnterpriseInbox.Infrastructure.Services
{
	public class AiWorkflowService
	{
		private readonly IChatCompletionService _chatService;
		private readonly SemanticCacheService _semanticCache;

		public AiWorkflowService(IChatCompletionService chatService, SemanticCacheService semanticCache)
		{
			_chatService = chatService;
			_semanticCache = semanticCache;
		}

		public async Task<(CustomerRequest Request, OutboxMessage Outbox)> ProcessRequestAsync(string sender, string content)
		{
			var requestId = Guid.NewGuid();
			string aiTextResponse = string.Empty;

			var cacheResult = await _semanticCache.GetCachedResponseAsync(content);

			if (cacheResult.IsHit && cacheResult.CachedResponse != null)
			{
				aiTextResponse = cacheResult.CachedResponse;
				Console.WriteLine("[Semantic Cache HIT] Önbellekten getiriliyor.");
			}
			else
			{
				Console.WriteLine("[Semantic Cache MISS] Gemini çağrılıyor...");

				var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins", "InboxAutomation");
				var promptPath = Path.Combine(pluginsDirectory, "AnalyzeRequest.txt");

				string systemPrompt = "Sen kurumsal bir e-posta analiz asistanısın. Gelen isteği analiz et ve JSON dön.";
				if (File.Exists(promptPath))
				{
					systemPrompt = await File.ReadAllTextAsync(promptPath);
				}

				var chatHistory = new ChatHistory();
				chatHistory.AddSystemMessage(systemPrompt);
				chatHistory.AddUserMessage(content);

				var settings = new PromptExecutionSettings
				{
					ExtensionData = new Dictionary<string, object>
										{
												{ "response_mime_type", "application/json" }
										}
				};

				var response = await _chatService.GetChatMessageContentAsync(chatHistory, settings);
				aiTextResponse = response.ToString().Trim();

				if (aiTextResponse.StartsWith("```json")) aiTextResponse = aiTextResponse.Substring(7);
				if (aiTextResponse.EndsWith("```")) aiTextResponse = aiTextResponse.Substring(0, aiTextResponse.Length - 3);

				await _semanticCache.SetCacheResponseAsync(content, aiTextResponse);
			}

			var aiResult = JsonSerializer.Deserialize<AIAnalysisResult>(aiTextResponse,
							new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

			var category = aiResult?.Category ?? "Belirsiz";
			var urgency = aiResult?.Urgency ?? "Orta";

			var emailEvent = new EmailRoutingEvent
			{
				RequestId = requestId,
				Sender = sender,
				Content = content,
				Category = category,
				Urgency = urgency,
				Summary = aiResult?.Summary ?? "Özet yok.",
				ActionPlan = aiResult?.ActionPlan ?? "Aksiyon yok."
			};

			var outboxMessage = new OutboxMessage
			{
				Id = Guid.NewGuid(),
				Type = nameof(EmailRoutingEvent),
				Content = JsonSerializer.Serialize(emailEvent),
				OccurredOn = DateTime.UtcNow
			};

			var customerRequest = new CustomerRequest
			{
				Id = requestId,
				Sender = sender,
				Content = content,
				Category = category,
				Urgency = urgency,
				Summary = aiResult?.Summary ?? "Özet çıkarılamadı.",
				ActionPlan = aiResult?.ActionPlan ?? "Aksiyon belirlenemedi.",
				IsProcessed = true,
				ProcessedAt = DateTime.UtcNow
			};

			return (customerRequest, outboxMessage);
		}
	}

	public class AIAnalysisResult
	{
		public string Category { get; set; } = string.Empty;
		public string Urgency { get; set; } = string.Empty;
		public string Summary { get; set; } = string.Empty;
		public string ActionPlan { get; set; } = string.Empty;
	}

	public class EmailRoutingEvent
	{
		public Guid RequestId { get; set; }
		public string Sender { get; set; } = string.Empty;
		public string Content { get; set; } = string.Empty;
		public string Category { get; set; } = string.Empty;
		public string Urgency { get; set; } = string.Empty;
		public string Summary { get; set; } = string.Empty;
		public string ActionPlan { get; set; } = string.Empty;
	}
}