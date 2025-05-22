using Microsoft.EntityFrameworkCore;
using Project.Application;
using Project.Infrastructure;
using Project.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// ��������� ���� ����������
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// ��������� ��������� SignalR ��� real-time ����������
builder.Services.AddSignalR();

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

app.UseAuthorization();

// �������� ��� ������������
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// API ��������
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action=Index}/{id?}");

// SignalR ����
app.MapHub<ProductionHub>("/productionHub");

// ��������� �������� ��� ������� (������ ��� ����������)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ManufacturingDbContext>();
    context.Database.EnsureCreated();
}

app.Run();

// SignalR Hub ��� real-time ����������
public class ProductionHub : Microsoft.AspNetCore.SignalR.Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}