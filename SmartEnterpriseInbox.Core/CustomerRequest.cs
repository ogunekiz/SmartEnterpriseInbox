namespace SmartEnterpriseInbox.Core
{
	public class CustomerRequest
	{
		public Guid Id { get; set; } = Guid.NewGuid();

		public string Sender { get; set; } = string.Empty;
		public string Content { get; set; } = string.Empty;
		public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

		public string? Category { get; set; }
		public string? Urgency { get; set; }
		public string? Summary { get; set; }
		public string? ActionPlan { get; set; }

		public bool IsProcessed { get; set; } = false;
		public DateTime? ProcessedAt { get; set; }
		public string? TargetSystemStatus { get; set; }
	}
}