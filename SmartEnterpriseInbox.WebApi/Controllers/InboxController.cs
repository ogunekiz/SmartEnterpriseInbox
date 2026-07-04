using Microsoft.AspNetCore.Mvc;
using SmartEnterpriseInbox.Infrastructure;
using SmartEnterpriseInbox.Infrastructure.Services;

namespace SmartEnterpriseInbox.WebApi.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class InboxController : ControllerBase
	{
		private readonly AppDbContext _dbContext;
		private readonly AiWorkflowService _aiWorkflowService;

		public InboxController(AppDbContext dbContext, AiWorkflowService aiWorkflowService)
		{
			_dbContext = dbContext;
			_aiWorkflowService = aiWorkflowService;
		}

		[HttpPost("process-request")]
		public async Task<IActionResult> ProcessIncomingRequest([FromBody] IncomingRequestDto dto)
		{
			if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest("Content boş olamaz.");

			using var transaction = await _dbContext.Database.BeginTransactionAsync();

			try
			{
				var result = await _aiWorkflowService.ProcessRequestAsync(dto.Sender, dto.Content);

				result.Request.TargetSystemStatus = "ERP_AND_CRM_SYNCED_SUCCESSFULLY";

				_dbContext.CustomerRequests.Add(result.Request);
				_dbContext.OutboxMessages.Add(result.Outbox);

				await _dbContext.SaveChangesAsync();
				await transaction.CommitAsync();

				return Ok(new { Message = "Talep ve Outbox güvenle kaydedildi.", Data = result.Request });
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync();
				return StatusCode(500, $"İşlem başarısız, veri tabanı geri alındı: {ex.Message}");
			}
		}
	}

	public class IncomingRequestDto
	{
		public string Sender { get; set; } = "anonymous@enterprise.com";
		public string Content { get; set; } = string.Empty;
	}
}