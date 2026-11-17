using Microsoft.AspNetCore.Authentication.Cookies;
using TalkToLiveChat.BusinessLogic;
using TalkToLiveChat.DataAccess;
using TalkToLiveChat.DataAccess.Repositories;
using TalkToLiveChat.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// Authorization
builder.Services.AddAuthorization();

// Data Access Layer (ADO.NET)
builder.Services.AddSingleton<IDatabaseHelper, DatabaseHelper>();

// Repositories
builder.Services.AddScoped<IVisitorRepository, VisitorRepository>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IInternalNoteRepository, InternalNoteRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();

// Business Logic Layer
builder.Services.AddScoped<IChatAssignmentService, ChatAssignmentService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExportService, ExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();
