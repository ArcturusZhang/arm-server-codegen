using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5188";
var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
var jsonOpts = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true
};

var basePath = "subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/databases";

Console.WriteLine("=== Azure SQL Versioning Demo Client ===\n");

// ──────────────────────────────────────────────
// 1. Create databases via V1 (2025-11-01)
// ──────────────────────────────────────────────
Console.WriteLine("── Step 1: Create two databases via V1 (2025-11-01) ──\n");

var db1 = new DatabaseResource
{
    Name = "mydb",
    Location = "eastus",
    Properties = new DatabaseProperties
    {
        Collation = "SQL_Latin1_General_CP1_CI_AS",
        MaxSizeBytes = 268435456000
    }
};
var created1 = await PutAsync("mydb", "2025-11-01", db1);
Print("Created mydb", created1);

var db2 = new DatabaseResource
{
    Name = "testdb",
    Location = "westus",
    Properties = new DatabaseProperties { Collation = "Latin1_General_100_CI_AS" }
};
var created2 = await PutAsync("testdb", "2025-11-01", db2);
Print("Created testdb", created2);

// ──────────────────────────────────────────────
// 2. Get database via V1 — should return without ElasticPoolId
// ──────────────────────────────────────────────
Console.WriteLine("── Step 2: Get mydb via V1 (no ElasticPoolId field) ──\n");

var getV1 = await GetAsync("mydb", "2025-11-01");
Print("GET mydb (V1)", getV1);

// ──────────────────────────────────────────────
// 3. Get same database via V2 — falls back to V1 controller
// ──────────────────────────────────────────────
Console.WriteLine("── Step 3: Get mydb via V2 (falls back to V1 controller) ──\n");

var getV2 = await GetAsync("mydb", "2025-12-01");
Print("GET mydb (V2 → fallback to V1)", getV2);

// ──────────────────────────────────────────────
// 4. Get same database via V3 — returns with ElasticPoolId
// ──────────────────────────────────────────────
Console.WriteLine("── Step 4: Get mydb via V3 (includes ElasticPoolId) ──\n");

var getV3 = await GetAsync("mydb", "2026-02-01");
Print("GET mydb (V3)", getV3);

// ──────────────────────────────────────────────
// 5. List databases via V2 (2025-12-01)
// ──────────────────────────────────────────────
Console.WriteLine("── Step 5: List databases via V2 ──\n");

var list = await ListAsync("2025-12-01");
Console.WriteLine($"  Found {list?.Length ?? 0} database(s):");
if (list != null)
    foreach (var d in list)
        Console.WriteLine($"    - {d.Name} ({d.Location})");
Console.WriteLine();

// ──────────────────────────────────────────────
// 6. Patch mydb via V3 — set ElasticPoolId
// ──────────────────────────────────────────────
Console.WriteLine("── Step 6: Patch mydb via V3 (set ElasticPoolId) ──\n");

var patch = new DatabaseResource
{
    Properties = new DatabaseProperties
    {
        ElasticPoolId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Sql/servers/srv1/elasticPools/pool1"
    }
};
var patched = await PatchAsync("mydb", "2026-02-01", patch);
Print("PATCH mydb (V3)", patched);

// ──────────────────────────────────────────────
// 7. Get mydb via V1 — should NOT show ElasticPoolId
// ──────────────────────────────────────────────
Console.WriteLine("── Step 7: Get mydb via V1 after V3 patch (no ElasticPoolId) ──\n");

var getV1After = await GetAsync("mydb", "2025-11-01");
Print("GET mydb (V1 after V3 patch)", getV1After);

// ──────────────────────────────────────────────
// 8. Delete testdb via V3 — falls back to V1
// ──────────────────────────────────────────────
Console.WriteLine("── Step 8: Delete testdb via V3 (falls back to V1) ──\n");

await DeleteAsync("testdb", "2026-02-01");
Console.WriteLine("  Deleted testdb\n");

// ──────────────────────────────────────────────
// 9. Get deleted database — should 404
// ──────────────────────────────────────────────
Console.WriteLine("── Step 9: Get deleted testdb — expect 404 ──\n");

var resp = await http.GetAsync($"{basePath}/testdb?api-version=2025-11-01");
Console.WriteLine($"  GET testdb → {(int)resp.StatusCode} {resp.StatusCode}\n");

// ──────────────────────────────────────────────
// 10. List after delete — should show only mydb
// ──────────────────────────────────────────────
Console.WriteLine("── Step 10: List via V3 (falls back to V2) — only mydb remains ──\n");

var listAfter = await ListAsync("2026-02-01");
Console.WriteLine($"  Found {listAfter?.Length ?? 0} database(s):");
if (listAfter != null)
    foreach (var d in listAfter)
        Console.WriteLine($"    - {d.Name} ({d.Location})");
Console.WriteLine();

Console.WriteLine("=== Done ===");

// ── Helpers ──

async Task<DatabaseResource?> PutAsync(string name, string version, DatabaseResource body)
{
    var resp = await http.PutAsJsonAsync($"{basePath}/{name}?api-version={version}", body, jsonOpts);
    resp.EnsureSuccessStatusCode();
    return await resp.Content.ReadFromJsonAsync<DatabaseResource>(jsonOpts);
}

async Task<DatabaseResource?> GetAsync(string name, string version)
{
    return await http.GetFromJsonAsync<DatabaseResource>($"{basePath}/{name}?api-version={version}", jsonOpts);
}

async Task<DatabaseResource[]?> ListAsync(string version)
{
    return await http.GetFromJsonAsync<DatabaseResource[]>($"{basePath}?api-version={version}", jsonOpts);
}

async Task<DatabaseResource?> PatchAsync(string name, string version, DatabaseResource body)
{
    var content = JsonContent.Create(body, options: jsonOpts);
    var req = new HttpRequestMessage(HttpMethod.Patch, $"{basePath}/{name}?api-version={version}") { Content = content };
    var resp = await http.SendAsync(req);
    resp.EnsureSuccessStatusCode();
    return await resp.Content.ReadFromJsonAsync<DatabaseResource>(jsonOpts);
}

async Task DeleteAsync(string name, string version)
{
    var resp = await http.DeleteAsync($"{basePath}/{name}?api-version={version}");
    resp.EnsureSuccessStatusCode();
}

void Print(string label, DatabaseResource? r)
{
    Console.WriteLine($"  {label}:");
    Console.WriteLine($"  {JsonSerializer.Serialize(r, jsonOpts)}");
    Console.WriteLine();
}

// ── Models ──

class DatabaseResource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Location { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public DatabaseProperties? Properties { get; set; }
}

class DatabaseProperties
{
    public string? Collation { get; set; }
    public long? MaxSizeBytes { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? CreationDate { get; set; }
    public string? ElasticPoolId { get; set; }
}
