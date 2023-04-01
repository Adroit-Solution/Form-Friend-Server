using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.Mongo;
using Hangfire.SqlServer;
using MailKit;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using Server.JobFactory;
using Server.Jobs;
using Server.Models;
using Server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
//builder.Services.AddQuartz(q =>
//{
//    q.UseMicrosoftDependencyInjectionJobFactory();
//});
//builder.Services.AddQuartzHostedService(opt =>
//{
//    opt.WaitForJobsToComplete = true;
//});
builder.Services.AddSingleton<IJobFactory, MyJobFactory>();
builder.Services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

#region Adding JobType
builder.Services.AddSingleton<NotificationJob>();
builder.Services.AddSingleton<LoggerJob>();
#endregion

#region Adding Jobs 
List<JobMetadata> jobMetadatas = new()
{
    new JobMetadata(Guid.NewGuid(), typeof(NotificationJob), "Notify Job", "0/10 * * * * ?"),
    new JobMetadata(Guid.NewGuid(), typeof(LoggerJob), "Log Job", "0/5 * * * * ?")
};
#endregion

builder.Services.AddSingleton(jobMetadatas);
// Add services to the container.
//builder.Services.AddHangfire(configuration => configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_170).UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings().UseSqlServerStorage(builder.Configuration.GetConnectionString("HangFireConnection"), new SqlServerStorageOptions
//{
//    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
//    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
//    QueuePollInterval = TimeSpan.Zero,
//    UseRecommendedIsolationLevel = true,
//    DisableGlobalLocks = true
//}));
//builder.Services.AddHangfireServer();
builder.Services.AddControllersWithViews();
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.AddTransient<IMongoDbServices,MongoDbServices>();
builder.Services.AddTransient<IGroupServices,GroupServices>();


var mongoDbSettings = builder.Configuration.GetSection("MongoDb").Get<MongoDBSettings>();
var mongoUrlBuilder = builder.Configuration.GetSection("Reminder").Get<MongoDBSettings>();




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

//var schedulerFactory = builder.Services.GetRequiredService<ISchedulerFactory>();
//var scheduler = await schedulerFactory.GetScheduler();

//// define the job and tie it to our HelloJob class
//var job = JobBuilder.Create<IMongoDbServices>()
//    .WithIdentity("myJob", "group1")
//    .Build();

//// Trigger the job to run now, and then every 40 seconds
//var trigger = TriggerBuilder.Create()
//    .WithIdentity("myTrigger", "group1")
//    .StartNow()
//    .WithSimpleSchedule(x => x
//        .WithIntervalInSeconds(40)
//        .RepeatForever())
//    .Build();

//await scheduler.ScheduleJob(job, trigger);
var app = builder.Build();
//IBackgroundJobClient? background = new BackgroundJobClient();
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

//app.UseHangfireDashboard();
//app.MapHangfireDashboard();


app.Run();
