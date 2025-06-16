using FunctionalTests.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace FunctionalTests.Controllers;

[ApiController]
[Route("api")]
public class UtilityController : ControllerBase
{
    private readonly CacheService _cacheService;
    private readonly Serilog.ILogger _logger;

    public UtilityController(CacheService cacheService, Serilog.ILogger logger)
    {
        _cacheService = cacheService;
        _logger = logger.ForContext<UtilityController>();
    }

    [HttpPost("clear-cache")]
    public IActionResult ClearCache()
    {
        _logger.Information("API: Clearing cache");
        _cacheService.Clear();
        return Ok(new { message = "Cache cleared successfully" });
    }
}
