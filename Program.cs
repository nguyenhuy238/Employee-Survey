using Microsoft.AspNetCore.Authentication.Cookies;
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

// -------------------- Repositories (JSON) --------------
builder.Services.AddSingleton<IRepository<User>, JsonFileRepository<User>>();
builder.Services.AddSingleton<IRepository<Team>, JsonFileRepository<Team>>();
builder.Services.AddSingleton<IRepository<Question>, JsonFileRepository<Question>>();
builder.Services.AddSingleton<IRepository<Test>, JsonFileRepository<Test>>();
builder.Services.AddSingleton<IRepository<Assignment>, JsonFileRepository<Assignment>>();
builder.Services.AddSingleton<IRepository<Session>, JsonFileRepository<Session>>();
builder.Services.AddSingleton<IRepository<Feedback>, JsonFileRepository<Feedback>>();

// NEW: repo cho reset mật khẩu (OTP)
builder.Services.AddSingleton<IRepository<PasswordReset>, JsonFileRepository<PasswordReset>>();

// (Tùy chọn) generic fallback cho các type khác nếu sau này thêm
builder.Services.AddSingleton(typeof(IRepository<>), typeof(JsonFileRepository<>));

// -------------------- App Services ---------------------
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<AssignmentService>();
builder.Services.AddSingleton<ReportService>();

// NEW: dịch vụ reset mật khẩu qua OTP
builder.Services.AddSingleton<PasswordResetService>();

// -------------------- Email (SMTP) ---------------------
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
// Triển khai gửi mail thực tế bằng SMTP
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

// -------------------- Seed dữ liệu mẫu -----------------
using (var scope = app.Services.CreateScope())
{
    await Seeder.RunAsync(scope.ServiceProvider);
}

app.Run();
