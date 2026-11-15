
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();
app.MapControllers();

app.Run();
