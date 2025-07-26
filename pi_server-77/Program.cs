// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using PiServer.Models;
// using PiServer.Services;

// namespace PiServer
// {
//     class Program
//     {
//         static async Task Main(string[] args)
//         {
//             var environment = new EnvironmentManager();

//             try
//             {
//                 await StartInteractiveBuilder(environment);
//                 Console.WriteLine("Процесс успешно завершен");
//             }
//             catch (Exception ex)
//             {
//                 Console.WriteLine($"Ошибка: {ex.Message}");
//             }

//             Console.WriteLine("Нажмите любую клавишу для выхода...");
//             Console.ReadKey();
//         }

//         static async Task StartInteractiveBuilder(EnvironmentManager environment)
//         {
//             var builder = new ProcessBuilder(environment);

//             while (true)
//             {
//                 Console.Clear();
//                 Console.WriteLine("Текущий процесс:\n" + builder.GetProcessDiagram());
//                 Console.WriteLine("\nВыберите действие:");
//                 Console.WriteLine("1. Добавить Send");
//                 Console.WriteLine("2. Добавить Receive");
//                 Console.WriteLine("3. Добавить Parallel");
//                 Console.WriteLine("4. Выполнить");
//                 Console.WriteLine("5. Сбросить");
//                 Console.WriteLine("6. Выход");

//                 var choice = Console.ReadLine()?.Trim();

//                 try
//                 {
//                     switch (choice)
//                     {
//                         case "1":
//                             await HandleSendProcess(environment, builder);
//                             break;

//                         case "2":
//                             await HandleReceiveProcess(environment, builder);
//                             break;

//                         case "3":
//                             await HandleParallelProcess(environment, builder);
//                             break;

//                         case "4":
//                             await builder.ExecuteAsync();
//                             Console.WriteLine("Выполнение завершено. Нажмите Enter...");
//                             Console.ReadLine();
//                             break;

//                         case "5":
//                             builder = new ProcessBuilder(environment);
//                             break;

//                         case "6":
//                             return;
//                     }
//                 }
//                 catch (Exception ex)
//                 {
//                     Console.WriteLine($"Ошибка: {ex.Message}");
//                     Console.ReadLine();
//                 }
//             }
//         }

//         private static async Task HandleSendProcess(EnvironmentManager environment, ProcessBuilder builder)
//         {
//             Console.Write("Введите канал: ");
//             var channel = Console.ReadLine() ?? "default_channel";
//             environment.GetOrCreateChannel(channel, ChannelStrategy.PassiveEnvironment);
//             Console.Write("Введите сообщение: ");
//             var message = Console.ReadLine() ?? "";

//             Console.WriteLine("Добавить продолжение после Send? (y/n)");
//             if (Console.ReadLine()?.ToLower() == "y")
//             {
//                 var continuationBuilder = new ProcessBuilder(environment);
//                 await BuildProcessChain(environment, continuationBuilder);
//                 builder.AddSend(channel, message, continuationBuilder.GetCurrentProcess());
//             }
//             else
//             {
//                 builder.AddSend(channel, message);
//             }
//         }

//         private static async Task HandleReceiveProcess(EnvironmentManager environment, ProcessBuilder builder)
//         {
//             Console.Write("Введите канал: ");
//             var channel = Console.ReadLine() ?? "default_channel";
//             environment.GetOrCreateChannel(channel, ChannelStrategy.PassiveEnvironment);
//             Console.Write("Введите фильтр: ");
//             var filter = Console.ReadLine() ?? "";

//             Console.WriteLine("Добавить обработку полученного сообщения? (y/n)");
//             if (Console.ReadLine()?.ToLower() == "y")
//             {
//                 var continuationBuilder = new ProcessBuilder(environment);
//                 await BuildProcessChain(environment, continuationBuilder);
                
//                 builder.AddReceive(channel, filter, msg =>
//                 {
//                     Console.WriteLine($"Получено: {msg}");
//                     return continuationBuilder.GetCurrentProcess();
//                 });
//             }
//             else
//             {
//                 builder.AddReceive(channel, filter, msg => 
//                     Console.WriteLine($"Получено: {msg}"));
//             }
//         }

//         private static async Task HandleParallelProcess(EnvironmentManager environment, ProcessBuilder builder)
//         {
//             Console.Write("Введите кол-во параллельных процессов: ");
//             if (int.TryParse(Console.ReadLine(), out int count) && count > 0)
//             {
//                 var processes = new List<IProcess>();
//                 for (int i = 0; i < count; i++)
//                 {
//                     Console.WriteLine($"\nНастройка процесса {i + 1}/{count}:");
//                     var processBuilder = new ProcessBuilder(environment);
//                     await BuildProcessChain(environment, processBuilder);
//                     processes.Add(processBuilder.GetCurrentProcess());
//                 }
//                 builder.AddParallel(processes.ToArray());
//             }
//         }

//         private static async Task BuildProcessChain(EnvironmentManager environment, ProcessBuilder builder)
//         {
//             Console.WriteLine("\nВыберите тип процесса:");
//             Console.WriteLine("1. SendProcess");
//             Console.WriteLine("2. ReceiveProcess");
//             Console.WriteLine("3. ParallelProcess");
//             Console.WriteLine("4. InactiveProcess (завершить цепочку)");

//             var choice = Console.ReadLine();
//             switch (choice)
//             {
//                 case "1":
//                     await HandleSendProcess(environment, builder);
//                     break;

//                 case "2":
//                     await HandleReceiveProcess(environment, builder);
//                     break;

//                 case "3":
//                     await HandleParallelProcess(environment, builder);
//                     break;

//                 default:
//                     builder.AddInactive();
//                     break;
//             }
//         }
//     }
// }




// var builder = WebApplication.CreateBuilder(args);

// // Минимально необходимые сервисы
// builder.Services.AddControllers();
// builder.Services.AddSingleton<EnvironmentManager>();
// builder.Services.AddSingleton<ProcessService>();

// var app = builder.Build();

// app.UseHttpsRedirection();
// app.UseAuthorization();
// app.MapControllers();

// // Явно указываем URL и отключаем HTTPS для простоты
// app.Run("http://0.0.0.0:5000");





// using Microsoft.AspNetCore.Builder;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
// using System.Text.Json;

// var builder = WebApplication.CreateBuilder(args);

// // Добавляем сервисы
// builder.Services.AddControllers()
//     .AddJsonOptions(options =>
//     {
//         options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
//         options.JsonSerializerOptions.WriteIndented = true;
//     });

// builder.Services.AddSingleton<EnvironmentManager>();

// var app = builder.Build();

// if (app.Environment.IsDevelopment())
// {
//     app.UseDeveloperExceptionPage();
// }

// app.UseRouting();
// app.MapControllers();

// app.Run();


using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using PiServer.Services;
using PiServer.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.None;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(63878); // Только HTTP
});

// Добавляем контроллеры
builder.Services.AddControllers();

// Добавляем сессии
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Добавляем EnvironmentManager и InMemoryProcessStorage как singleton
builder.Services.AddSingleton<EnvironmentManager>();
builder.Services.AddSingleton<ISessionProcessStorage, InMemoryProcessStorage>();

var app = builder.Build();

app.Use((context, next) =>
{
    context.Request.Scheme = "http";
    return next();
});

// Используем сессии
app.UseSession();

app.MapControllers();
app.MapGet("/", () => "π-Calculus Server is running (HTTP only)!");

app.Run();
