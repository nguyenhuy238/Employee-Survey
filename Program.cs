using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// -------------------- MVC + Swagger --------------------
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// -------------------- Cookie Auth ----------------------
builder.Services
    .AddAuthentication("cookie")
    .AddCookie("cookie", o =>
    {
        o.LoginPath = "/auth/login";
        o.AccessDeniedPath = "/auth/denied";
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.Name = "emp_survey_auth";
    });
builder.Services.AddAuthorization();

// -------------------- Multipart upload limit -----------
builder.Services.Configure<FormOptions>(opt =>
{
    opt.MultipartBodyLengthLimit = 50L * 1024 * 1024;
});

// -------------------- Repositories (JSON) --------------
builder.Services.AddSingleton<IRepository<User>, JsonFileRepository<User>>();
builder.Services.AddSingleton<IRepository<Team>, JsonFileRepository<Team>>();
builder.Services.AddSingleton<IRepository<Question>, JsonFileRepository<Question>>();
builder.Services.AddSingleton<IRepository<Test>, JsonFileRepository<Test>>();
builder.Services.AddSingleton<IRepository<Assignment>, JsonFileRepository<Assignment>>();
builder.Services.AddSingleton<IRepository<Session>, JsonFileRepository<Session>>();
builder.Services.AddSingleton<IRepository<Feedback>, JsonFileRepository<Feedback>>();

// (Nếu bạn không cần, có thể bỏ dòng generic dưới — tránh trùng đăng ký)
builder.Services.AddSingleton(typeof(IRepository<>), typeof(JsonFileRepository<>));

// -------------------- App Services ---------------------
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddSingleton<IQuestionService, QuestionService>();
builder.Services.AddSingleton<IQuestionExcelService, QuestionExcelService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<AssignmentService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<PasswordResetService>();

// ===== NEW: Engine tạo đề tự động & phân bổ điểm =====
builder.Services.AddSingleton<ITestGenerationService, TestGenerationService>();

// -------------------- Options --------------------------
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("App"));     // NEW
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));

// -------------------- Email & Notification -------------
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<INotificationService, NotificationService>();

var app = builder.Build();

// -------------------- Middleware pipeline --------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Root "/" -> về trang đăng nhập
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/auth/login");
    return Task.CompletedTask;
});

// MVC route chuẩn + attribute routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

// -------------------- Ensure folders & Seed ------------
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads"));

// Nếu bạn có Seeder, giữ lại; nếu không dùng, có thể bỏ khối using này
using (var scope = app.Services.CreateScope())
{
    await Seeder.RunAsync(scope.ServiceProvider);
}

app.Run();
