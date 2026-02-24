using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace Generated.V20260201.Controllers {
    [ApiController]
    [ApiVersion("2026-02-01")]
    [Route(
        "subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Sql/servers/{serverName}/databases")]
    public abstract class DatabasesControllerBase : ControllerBase
    {
        [HttpGet("{databaseName}")]
        public abstract async Task<IActionResult> Get(
            string subscriptionId,
            string resourceGroupName,
            string databaseName,
            CancellationToken cancellationToken
        );[HttpPut("{databaseName}")]
        public abstract async Task<IActionResult> CreateOrUpdate(
            string subscriptionId,
            string resourceGroupName,
            string databaseName,
            [FromBody]
            Database body,
            CancellationToken cancellationToken
        );[HttpPatch("{databaseName}")]
        public abstract async Task<IActionResult> Update(
            string subscriptionId,
            string resourceGroupName,
            string databaseName,
            [FromBody]
            Database body,
            CancellationToken cancellationToken
        );[HttpGet]
        public abstract async Task<IActionResult> ListByResourceGroup(
            string subscriptionId,
            string resourceGroupName,
            CancellationToken cancellationToken
        );
    }
}
