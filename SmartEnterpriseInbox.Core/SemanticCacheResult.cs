namespace SmartEnterpriseInbox.Core
{
	public class SemanticCacheResult
	{
		public bool IsHit { get; set; }
		public string? CachedResponse { get; set; }
	}
}