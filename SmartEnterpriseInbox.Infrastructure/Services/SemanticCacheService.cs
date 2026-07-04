using Microsoft.Extensions.Configuration;
using SmartEnterpriseInbox.Core;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

namespace SmartEnterpriseInbox.Infrastructure.Services
{
	public class SemanticCacheService
	{
		private readonly IConnectionMultiplexer _redis;
		private readonly IDatabase _db;
		private readonly string _apiKey;

		private static readonly HttpClient _httpClient = new HttpClient();

		private const string CacheKeyPrefix = "semantic_cache:";
		private const float SimilarityThreshold = 0.85f;

		public SemanticCacheService(IConnectionMultiplexer redis, IConfiguration configuration)
		{
			_redis = redis;
			_db = _redis.GetDatabase();
			_apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini API Key not found.");
		}

		private async Task<float[]> GenerateEmbeddingAsync(string text)
		{
			var requestBody = new
			{
				model = "models/gemini-embedding-001",
				content = new
				{
					parts = new[] { new { text } }
				}
			};

			var jsonPayload = JsonSerializer.Serialize(requestBody);
			var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

			var url = $"https://generativelanguage.googleapis.com/v1/models/gemini-embedding-001:embedContent?key={_apiKey}";

			var response = await _httpClient.PostAsync(url, httpContent);

			if (!response.IsSuccessStatusCode)
			{
				var errorContent = await response.Content.ReadAsStringAsync();
				throw new Exception($"Gemini Embedding API Error: {response.StatusCode} - {errorContent}");
			}

			var responseString = await response.Content.ReadAsStringAsync();

			using var doc = JsonDocument.Parse(responseString);
			var valuesElement = doc.RootElement
					.GetProperty("embedding")
					.GetProperty("values");

			var vector = new float[valuesElement.GetArrayLength()];
			int index = 0;
			foreach (var value in valuesElement.EnumerateArray())
			{
				vector[index++] = value.GetSingle();
			}

			return vector;
		}

		public async Task<SemanticCacheResult> GetCachedResponseAsync(string content)
		{
			var newVector = await GenerateEmbeddingAsync(content);

			var server = _redis.GetServer(_redis.GetEndPoints()[0]);
			var keys = server.Keys(pattern: $"{CacheKeyPrefix}*");

			foreach (var key in keys)
			{
				var cachedData = await _db.StringGetAsync(key);
				if (!cachedData.IsNullOrEmpty)
				{
					var cacheEntry = JsonSerializer.Deserialize<SemanticCacheEntry>(cachedData!);
					if (cacheEntry != null)
					{
						float similarity = CalculateCosineSimilarity(newVector, cacheEntry.Vector);
						if (similarity >= SimilarityThreshold)
						{
							return new SemanticCacheResult
							{
								IsHit = true,
								CachedResponse = cacheEntry.ResponseJson
							};
						}
					}
				}
			}

			return new SemanticCacheResult { IsHit = false };
		}

		public async Task SetCacheResponseAsync(string content, string responseJson)
		{
			var vector = await GenerateEmbeddingAsync(content);

			var entry = new SemanticCacheEntry
			{
				Content = content,
				Vector = vector,
				ResponseJson = responseJson,
				CreatedAt = DateTime.UtcNow
			};

			var key = $"{CacheKeyPrefix}{Guid.NewGuid()}";
			await _db.StringSetAsync(key, JsonSerializer.Serialize(entry), TimeSpan.FromDays(7));
		}

		private float CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
		{
			if (vectorA.Length != vectorB.Length) return 0;
			float dotProduct = 0.0f, magnitudeA = 0.0f, magnitudeB = 0.0f;
			for (int i = 0; i < vectorA.Length; i++)
			{
				dotProduct += vectorA[i] * vectorB[i];
				magnitudeA += vectorA[i] * vectorA[i];
				magnitudeB += vectorB[i] * vectorB[i];
			}
			if (magnitudeA == 0 || magnitudeB == 0) return 0;
			return dotProduct / ((float)Math.Sqrt(magnitudeA) * (float)Math.Sqrt(magnitudeB));
		}
	}

	internal class SemanticCacheEntry
	{
		public string Content { get; set; } = string.Empty;
		public float[] Vector { get; set; } = Array.Empty<float>();
		public string ResponseJson { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
	}
}