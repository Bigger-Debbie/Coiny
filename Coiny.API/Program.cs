using Coiny.API.Extensions;
using Coiny.API.Configuration;
using System.Text.Json.Serialization;
using Coiny.API.Data;

var seedDemo = args.Contains("--seed-demo");
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--seed-demo").ToArray());

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddIdentityServices();

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplicationServices();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDevClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (seedDemo)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Demo seeding is only available in Development.");

    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngularDevClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
