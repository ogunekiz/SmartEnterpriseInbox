using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Embeddings;
using SmartEnterpriseInbox.Infrastructure;
using SmartEnterpriseInbox.Infrastructure.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
		options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var redisConfig = builder.Configuration.GetSection("Redis");
var multiplexer = ConnectionMultiplexer.Connect(redisConfig["ConnectionString"] ?? "localhost:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

var geminiConfig = builder.Configuration.GetSection("Gemini");
var apiKey = geminiConfig["ApiKey"] ?? throw new InvalidOperationException("Gemini API Key bulunamadý!");
var modelId = geminiConfig["ModelId"] ?? "gemini-3.5-flash";

builder.Services.AddSingleton<IChatCompletionService>(sp =>
		new GoogleAIGeminiChatCompletionService(
				modelId: modelId,
				apiKey: apiKey
		));

builder.Services.AddTransient<Kernel>(sp => new Kernel(sp));

builder.Services.AddScoped<AiWorkflowService>();
builder.Services.AddScoped<SemanticCacheService>();

builder.Services.AddHostedService<EmailRoutingBackgroundService>();
builder.Services.AddHostedService<OutboxPublisherBackgroundService>();
builder.Services.AddHostedService<EmailReceiverBackgroundService>();

builder.Services.AddSingleton<IEmbeddingGenerationService<string, float>>(sp =>
		new GoogleAITextEmbeddingGenerationService(
				modelId: "text-embedding-004",
				apiKey: builder.Configuration["Gemini:ApiKey"]
		));


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}
app.UseAuthorization();
app.MapControllers();
app.Run();