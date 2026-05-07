using MpesaPaymentApi.Data;
using MpesaPaymentApi.Models.Configuration;
using MpesaPaymentApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MpesaOptions>(builder.Configuration.GetSection("Mpesa"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("MpesaClient", client =>
{
    var baseUrl = builder.Configuration["Mpesa:BaseUrl"] ?? "https://sandbox.safaricom.co.ke";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IMpesaService, MpesaService>();
builder.Services.AddControllers();

var app = builder.Build();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();