using AuthenticationAppMVC.Data;
using AuthenticationAppMVC.Hubs;
using AuthenticationAppMVC.Models;
using AuthenticationAppMVC.Services;
using AuthenticationAppMVC.Services.Impl;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAppMVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<AppDBContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("default"));
            });

            builder.Services.AddIdentity<User, IdentityRole>(options =>
            {
                options.Password.RequiredUniqueChars = 0;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredLength = 8;
            }).AddEntityFrameworkStores<AppDBContext>().AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Authen/Login";
                options.AccessDeniedPath = "/Authen/AccessDenied";
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder => builder.AllowAnyMethod()
                                      .AllowAnyHeader()
                                      .AllowCredentials()
                                      .SetIsOriginAllowed(_ => true));
            });

            var cloudinarySettings = builder.Configuration.GetSection("Cloudinary");
            var cloudName = cloudinarySettings["CloudName"];
            var apiKey = cloudinarySettings["ApiKey"];
            var apiSecret = cloudinarySettings["ApiSecret"];

            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                throw new Exception("Cloudinary settings are missing in configuration.");
            }

            builder.Services.AddSingleton(new Cloudinary(new Account(cloudName, apiKey, apiSecret)));

            builder.Services.AddScoped<FileService>();
            builder.Services.AddScoped<IFriendsService, FriendsServiceImpl>();
            builder.Services.AddScoped<ICloudService, CloudServiceImpl>();

            builder.Services.AddSignalR();

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

            app.UseCors("AllowAll");
            app.UseRouting();

            app.UseAuthorization();
            app.MapHub<Chathub>("/chathub");
            app.MapHub<FriendsHub>("/friendshub");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
