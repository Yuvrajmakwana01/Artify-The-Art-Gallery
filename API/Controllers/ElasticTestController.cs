using Microsoft.AspNetCore.Mvc;
using Repository.Services;

namespace API.Controllers;

[ApiController]
[Route("api/elastic")]
public class ElasticTestController : ControllerBase
{
    private readonly ElasticService _elasticService;

    public ElasticTestController(ElasticService elasticService)
    {
        _elasticService = elasticService;
    }

    [HttpGet("info")]
    public async Task<IActionResult> Info()
    {
        var version = await _elasticService.GetInfoAsync();
        return Ok(new
        {
            success = true,
            version
        });
    }
}
