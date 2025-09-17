using BookLoop.Models;
using BookLoop.Data;
using BookLoop.Services;
using BookLoop.Services.Export;
using BookLoop.Services.Reports;
using BorrowSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;



namespace BookLoop
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

			builder.Services.AddDbContext<OrdersysContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("Ordersys"))); // �s�W OrdersysContext

            builder.Services.AddDbContext<BorrowSystemContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("BorrowSystem"))); // �s�W BorrowSystemContext
            builder.Services.AddScoped<ReservationExpiryService>(); // BorrowSystem �A��
            builder.Services.AddHostedService<ReservationExpiryWorker>(); // BorrowSystem �A��
            builder.Services.AddScoped<ReservationQueueService>(); // BorrowSystem �A��
			// ReportMail�]�i�g�B�]�E���F���V�X�᪺֫ DB�^
			builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("ReportMail"),
					x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));

			// Shop�]��Ū�d�߼h�F�@�w�n�� ShopConnection�A���^�h DefaultConnection�^
			builder.Services.AddDbContext<ShopDbContext>(options =>
			{
				var shopConn = builder.Configuration.GetConnectionString("ShopConnection");
				if (string.IsNullOrWhiteSpace(shopConn))
					throw new InvalidOperationException("�ʤֳs�u�r��GShopConnection�]�Ы��V�X�᪺֫��Ʈw�^�C");

				options.UseSqlServer(shopConn);
				options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // ��Ū�̨Τ�
#if DEBUG
				options.EnableDetailedErrors();
				options.EnableSensitiveDataLogging();
#endif
			});


			// �v���A��(�����v��)
			//builder.Services.AddScoped<IPublisherScopeService, PublisherScopeService>();


			// ����A��
			builder.Services.AddScoped<IReportDataService, ShopReportDataService>();
			builder.Services.AddScoped<ReportQueryBuilder>();


			// �ץX/�H�H
			builder.Services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
			builder.Services.AddScoped<MailService>();


			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
				app.UseDeveloperExceptionPage();   // �� �s�W�G�� 500 ������ܰ��|�Ӹ`
				app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();


			app.MapControllerRoute(
	            name: "areas",
	            pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
			app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            app.Run();
        }
    }
}
