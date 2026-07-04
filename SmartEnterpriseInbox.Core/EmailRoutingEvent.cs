using System;

namespace SmartEnterpriseInbox.Core
{
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