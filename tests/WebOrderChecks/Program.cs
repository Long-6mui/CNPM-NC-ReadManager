using System.Net;
using System.Text;
using System.Text.Json;
using ReadManager.Web.Services;

void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); }
var handler = new Stub();
var client = new StoriesApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://test/") });
handler.Body = "{\"orderId\":42,\"orderType\":\"Plan\",\"amount\":50000,\"status\":\"Pending\"}";
var result = await client.CreatePlanOrderAsync(3,7,"test-token");
Check(result.Ok && result.Id==42, "plan purchase returns real API order ID");
Check(handler.Url=="/api/orders" && handler.Method==HttpMethod.Post && handler.Auth=="Bearer test-token", "purchase posts to authenticated orders API");
using(var json=JsonDocument.Parse(handler.Sent!))
{
    var root=json.RootElement;
    Check(root.GetProperty("planVersionId").GetInt32()==7 && root.GetProperty("planId").GetInt32()==3, "displayed plan version sent for price-change protection");
    Check(!root.TryGetProperty("amount",out _) && !root.TryGetProperty("status",out _), "client cannot choose price or payment status");
}
handler.Status=HttpStatusCode.BadRequest;handler.Body="{\"message\":\"Price changed\"}";
result=await client.CreatePlanOrderAsync(3,7,"test-token");
Check(!result.Ok && result.Message=="Price changed", "API validation error preserved");
handler.Status=HttpStatusCode.OK;handler.Body="{\"items\":[],\"totalCount\":0,\"page\":2,\"pageSize\":20}";
var orders=await client.MyOrdersAsync("test-token",2);
Check(orders.Page==2 && handler.Url=="/api/me/orders?page=2&pageSize=20", "history comes from paginated current-user API");
Console.WriteLine("ALL WEB ORDER CHECKS PASSED");

sealed class Stub : HttpMessageHandler
{
    public HttpStatusCode Status=HttpStatusCode.OK;
    public string Body="{}";
    public string? Url,Auth,Sent;
    public HttpMethod? Method;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
    {
        Url=req.RequestUri!.PathAndQuery;Auth=req.Headers.Authorization?.ToString();Method=req.Method;
        Sent=req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(Status) { Content=new StringContent(Body,Encoding.UTF8,"application/json") };
    }
}
