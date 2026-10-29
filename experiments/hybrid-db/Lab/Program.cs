using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HybridLab;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Microsoft.Data.Sqlite;

var builder=WebApplication.CreateBuilder(args);
if(builder.Configuration["Lab:Enabled"]!="true")throw new InvalidOperationException("This isolated lab must be explicitly enabled.");
var connection=builder.Configuration["Lab:ControlConnection"]??throw new InvalidOperationException("Missing lab database.");
if(new NpgsqlConnectionStringBuilder(connection).Database!="coiny_hybrid_lab")throw new InvalidOperationException("Only the dedicated coiny_hybrid_lab database is permitted.");
var options=new LabOptions(connection,builder.Configuration["Lab:Key"]??"",builder.Configuration["Lab:SigningKey"]??"",
    Path.GetFullPath(builder.Configuration["Lab:DataRoot"]??"/data"),builder.Configuration["Lab:Node"]??"lab-node-1",builder.Configuration.GetValue("Lab:TargetSchema",2));
if(options.Key.Length<32||options.SigningKey.Length<32||options.TargetSchema!=2)throw new InvalidOperationException("Invalid isolated-lab configuration.");
Directory.CreateDirectory(Path.Combine(options.DataRoot,"households"));Directory.CreateDirectory(Path.Combine(options.DataRoot,"backups"));
builder.Services.AddSingleton(options);builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton<Catalog>();builder.Services.AddSingleton<Coordinator>();builder.Services.AddSingleton<HouseholdStorage>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt=>{
    jwt.MapInboundClaims=false;jwt.TokenValidationParameters=new TokenValidationParameters {ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,ValidateIssuerSigningKey=true,
        ValidIssuer="coiny-hybrid-lab",ValidAudience="coiny-hybrid-lab",ClockSkew=TimeSpan.Zero,IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),ValidAlgorithms=[SecurityAlgorithms.HmacSha256]};});
builder.Services.AddAuthorization();
var app=builder.Build();
app.Use(async(context,next)=>{
    try{
        if(context.Request.Path.StartsWithSegments("/lab")){
            var supplied=Encoding.UTF8.GetBytes(context.Request.Headers["X-Lab-Key"].ToString());var expected=Encoding.UTF8.GetBytes(options.Key);
            if(!CryptographicOperations.FixedTimeEquals(supplied,expected)){context.Response.StatusCode=401;return;}
        }
        await next();
    }catch(LabFault error){context.Response.StatusCode=error.Status;await context.Response.WriteAsJsonAsync(new {error=error.Message});}
    catch(Exception error) when(error is NpgsqlException or SqliteException or IOException){
        app.Logger.LogWarning("Lab storage failure: {FailureType}",error.GetType().Name);
        context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new {error="Storage unavailable; request denied."});
    }
});
app.UseAuthentication();app.UseAuthorization();
var catalog=app.Services.GetRequiredService<Catalog>();var storage=app.Services.GetRequiredService<HouseholdStorage>();
await catalog.Initialize();await catalog.SeedUsers();
if(await catalog.Find(Fixtures.A) is null){await catalog.Create(Fixtures.Alice,"Synthetic household A",2,Fixtures.A);await catalog.AddMember(Fixtures.A,Fixtures.Amy);}
if(await catalog.Find(Fixtures.B) is null)await catalog.Create(Fixtures.Bob,"Synthetic household B",2,Fixtures.B);
await storage.Recover();
static Guid User(HttpContext context)=>Guid.TryParse(context.User.FindFirstValue("sub"),out var id)?id:throw new LabFault(401,"Invalid identity.");
async Task<Household> Find(Guid id)=>await catalog.Find(id)??throw new LabFault(404,"Household not found.");
app.MapGet("/health",async(NpgsqlDataSource db)=>{await using var command=db.CreateCommand("SELECT 1");await command.ExecuteScalarAsync();return Results.Ok(new {status="ready",prototype=true});});
app.MapGet("/lab/fixtures",()=>{
    var signing=new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),SecurityAlgorithms.HmacSha256);
    return new {Households=new {A=Fixtures.A,B=Fixtures.B},Users=Fixtures.Users.Select(user=>new {user.Id,user.Name,Token=new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("coiny-hybrid-lab","coiny-hybrid-lab",[new Claim("sub",user.Id.ToString())],expires:DateTime.UtcNow.AddHours(2),signingCredentials:signing))})};
});
app.MapGet("/api/households/{id:guid}/snapshot",async(Guid id,HttpContext context)=>await storage.Snapshot(await catalog.Authorize(id,User(context)))).RequireAuthorization();
app.MapPost("/api/households/{id:guid}/imports",async(Guid id,ImportRequest request,HttpContext context)=>await storage.Import(await catalog.Authorize(id,User(context)),request)).RequireAuthorization();
app.MapGet("/lab/inventory",()=>storage.Inventory());
app.MapGet("/lab/metrics",()=>new {FinancialFileOpens=storage.FileOpenCount});
app.MapPost("/lab/swap-storage/{a:guid}/{b:guid}",async(Guid a,Guid b)=>{await catalog.SwapStorageForFaultTest(a,b);return Results.Ok();});
app.MapPost("/lab/households",async(ProvisionRequest request)=>{var household=await catalog.Create(request.UserId,request.Name,request.SchemaVersion);await storage.Provision(household,request.FailAt);return await catalog.Find(household.Id);});
app.MapPost("/lab/recover",async()=>{await storage.Recover();return Results.Ok();});
app.MapPost("/lab/households/{id:guid}/hold",async(Guid id,int milliseconds)=>{await storage.HoldWriter(await Find(id),milliseconds);return Results.Ok();});
app.MapGet("/lab/households/{id:guid}/holding",(Guid id,Coordinator coordinator)=>new {holding=coordinator.Holds.TryGetValue(id,out var active)&&active});
app.MapPost("/lab/households/{id:guid}/migrate",async(Guid id,bool? failAfterCommit)=>{await storage.Migrate(await Find(id),failAfterCommit??false);return await catalog.Find(id);});
app.MapPost("/lab/households/{id:guid}/backup",async(Guid id)=>await storage.Backup(await Find(id)));
app.MapPost("/lab/households/{id:guid}/restore",async(Guid id,RestoreRequest request)=>{await storage.Restore(await Find(id),request.BackupId,request.FailAfterMarker);return Results.Ok();});
app.MapDelete("/lab/households/{id:guid}/members/{user:guid}",async(Guid id,Guid user)=>{await catalog.RemoveMember(id,user);return Results.NoContent();});
app.MapPost("/lab/households/{id:guid}/members/{user:guid}",async(Guid id,Guid user)=>{await catalog.AddMember(id,user);return Results.NoContent();});
app.MapPut("/lab/households/{id:guid}/node/{node}",async(Guid id,string node)=>{await catalog.SetNode(id,node);return Results.NoContent();});
await app.RunAsync();
