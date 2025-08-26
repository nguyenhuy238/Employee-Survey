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
    // request tối đa 50MB (có thể chỉnh)
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

// Audit service
builder.Services.AddSingleton<IAuditService, AuditService>();
// Services
builder.Services.AddSingleton<IQuestionService, QuestionService>();
builder.Services.AddSingleton<IQuestionExcelService, QuestionExcelService>();

// NEW: repo + service reset mật khẩu (OTP) — nếu đã có thì giữ nguyên
builder.Services.AddSingleton<IRepository<PasswordReset>, JsonFileRepository<PasswordReset>>();
builder.Services.AddSingleton(typeof(IRepository<>), typeof(JsonFileRepository<>));

// -------------------- App Services ---------------------
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<AssignmentService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<PasswordResetService>();

// -------------------- Email (SMTP) ---------------------
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

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

using (var scope = app.Services.CreateScope())
{
    await Seeder.RunAsync(scope.ServiceProvider); // nếu đã có Seeder; nếu chưa có, có thể bỏ đi.
}

app.Run();
