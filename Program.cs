
using Microsoft.AspNetCore.Authentication.Cookies;
using Employee_Survey.Application;
using Employee_Survey.Domain;
using Employee_Survey.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// MVC + Swagger
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cookie Auth
builder.Services.AddAuthentication("cookie")
    .AddCookie("cookie", o =>
    {
        o.LoginPath = "/auth/login";          // chưa login -> tới đây
        o.AccessDeniedPath = "/auth/denied";
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Repositories (JSON)
builder.Services.AddSingleton<IRepository<User>, JsonFileRepository<User>>();
builder.Services.AddSingleton<IRepository<Team>, JsonFileRepository<Team>>();
builder.Services.AddSingleton<IRepository<Question>, JsonFileRepository<Question>>();
builder.Services.AddSingleton<IRepository<Test>, JsonFileRepository<Test>>();
builder.Services.AddSingleton<IRepository<Assignment>, JsonFileRepository<Assignment>>();
builder.Services.AddSingleton<IRepository<Session>, JsonFileRepository<Session>>();
builder.Services.AddSingleton<IRepository<Feedback>, JsonFileRepository<Feedback>>();
builder.Services.AddSingleton(typeof(IRepository<>), typeof(JsonFileRepository<>));
builder.Services.AddSingleton<IAuditService, AuditService>();
builder.Services.AddSingleton<IQuestionService, QuestionService>();
builder.Services.AddSingleton<IQuestionExcelService, QuestionExcelService>();

// Services
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<AssignmentService>();
builder.Services.AddSingleton<ReportService>();

var app = builder.Build();

// Swagger (dev)
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// ✅ Root "/" -> redirect tới /auth/login (vì Login đang attribute route)
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/auth/login");
    return Task.CompletedTask;
});

// Route MVC thông thường cho các controller khác
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

// Seed
using (var scope = app.Services.CreateScope())
{
    await Seeder.RunAsync(scope.ServiceProvider);
}

app.Run();



//using Employee_Survey.Application;
//using Employee_Survey.Domain;
//using Employee_Survey.Infrastructure;


//var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddControllersWithViews();
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

//// Auth cookie
//builder.Services.AddAuthentication("cookie").AddCookie("cookie", o =>
//{
//    o.LoginPath = "/auth/login";
//});
//builder.Services.AddAuthorization();

//// Repositories (JSON)
//builder.Services.AddSingleton<IRepository<User>, JsonFileRepository<User>>();
//builder.Services.AddSingleton<IRepository<Team>, JsonFileRepository<Team>>();
//builder.Services.AddSingleton<IRepository<Question>, JsonFileRepository<Question>>();
//builder.Services.AddSingleton<IRepository<Test>, JsonFileRepository<Test>>();
//builder.Services.AddSingleton<IRepository<Assignment>, JsonFileRepository<Assignment>>();
//builder.Services.AddSingleton<IRepository<Session>, JsonFileRepository<Session>>();
//builder.Services.AddSingleton<IRepository<Feedback>, JsonFileRepository<Feedback>>();

//// Services
//builder.Services.AddSingleton<AuthService>();
//builder.Services.AddSingleton<TestService>();
//builder.Services.AddSingleton<AssignmentService>();

//var app = builder.Build();

//// Swagger dev
//if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

//app.UseStaticFiles();
//app.UseRouting();
//app.UseAuthentication();
//app.UseAuthorization();

//// MVC route + API
//app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
//app.MapControllers();

//// Seed dữ liệu
//using (var scope = app.Services.CreateScope())
//{
//    await Seeder.RunAsync(scope.ServiceProvider);
//}

//app.Run();



////var builder = WebApplication.CreateBuilder(args);

////// Add services to the container.
////builder.Services.AddControllersWithViews();

////var app = builder.Build();

////// Configure the HTTP request pipeline.
////if (!app.Environment.IsDevelopment())
////{
////    app.UseExceptionHandler("/Home/Error");
////    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
////    app.UseHsts();
////}

////app.UseHttpsRedirection();
////app.UseStaticFiles();

////app.UseRouting();

////app.UseAuthorization();

////app.MapControllerRoute(
////    name: "default",
////    pattern: "{controller=Home}/{action=Index}/{id?}");

////app.Run();
