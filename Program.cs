using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Server.Models;
using Server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.AddTransient<IMongoDbServices,MongoDbServices>();
builder.Services.AddTransient<IGroupServices,GroupServices>();


var mongoDbSettings = builder.Configuration.GetSection("MongoDb").Get<MongoDBSettings>();

builder.Services.AddIdentity<User, Role>()
    .AddMongoDbStores<User, Role, Guid>
    (
        mongoDbSettings.ConnectionURI, mongoDbSettings.DatabaseName
    );
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.SetIsOriginAllowed(_ => true).AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});
builder.Services.AddAuthorization();
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("+)3@5!7#9$0%2^4&6*8(0"));
        options.SaveToken = true;
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKey = key
        };

    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
