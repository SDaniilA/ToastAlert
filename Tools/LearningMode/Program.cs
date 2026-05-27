using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace ToastAlert.LearningMode
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("================================================");
            Console.WriteLine("Toast Alert LearningMode - сбор приложений");
            Console.WriteLine("================================================");
            Console.WriteLine("Программа будет собирать имена приложений,");
            Console.WriteLine("от которых поступают уведомления.");
            Console.WriteLine("Новые имена будут ДОБАВЛЯТЬСЯ к уже имеющимся в discovered_apps.txt (без дублей).");
            Console.WriteLine();
            Console.WriteLine("Нажмите любую клавишу для начала сбора...");
            Console.ReadKey();
            Console.WriteLine();

            var listener = UserNotificationListener.Current;
            var accessStatus = await listener.RequestAccessAsync();
            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                Console.WriteLine("❌ Нет доступа к уведомлениям. Разрешите доступ в настройках Windows.");
                Console.WriteLine("   Настройки → Конфиденциальность → Уведомления");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("✅ Доступ получен. Начинаю сбор уведомлений...");
            Console.WriteLine("Для остановки сбора нажмите Ctrl+C.\n");

            // Загружаем уже существующий список, если файл есть
            string outputFile = "discovered_apps.txt";
            var existingApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(outputFile))
            {
                var lines = File.ReadAllLines(outputFile);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        existingApps.Add(trimmed);
                }
                Console.WriteLine($"📁 Загружено {existingApps.Count} ранее сохранённых приложений.");
            }

            var newApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\n⏹️ Остановка сбора...");
            };

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var notifications = await listener.GetNotificationsAsync(NotificationKinds.Toast);
                    foreach (var notif in notifications)
                    {
                        var appName = notif.AppInfo.DisplayInfo.DisplayName;
                        if (!string.IsNullOrEmpty(appName))
                        {
                            // Если имя уже есть в старом списке или уже добавлено в новой сессии – пропускаем
                            if (!existingApps.Contains(appName) && newApps.Add(appName))
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Обнаружено новое приложение: {appName}");
                            }
                        }
                    }
                    await Task.Delay(2000, cts.Token);
                }
            }
            catch (OperationCanceledException) { }

            // Объединяем старые и новые имена
            var allApps = new HashSet<string>(existingApps, StringComparer.OrdinalIgnoreCase);
            foreach (var app in newApps) allApps.Add(app);

            Console.WriteLine("\n================================================");
            Console.WriteLine("Сбор завершён. Найдены следующие приложения:");
            Console.WriteLine("================================================");
            var sortedApps = allApps.OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < sortedApps.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {sortedApps[i]}");
            }

            // Сохраняем объединённый список (перезаписываем файл)
            File.WriteAllLines(outputFile, sortedApps);
            Console.WriteLine($"\n✅ Список сохранён в файл: {outputFile}");
            Console.WriteLine($"   (добавлено {newApps.Count} новых, всего {allApps.Count})");
            Console.WriteLine("Скопируйте нужные имена в config.json в секцию Monitoring -> AllowedApps.");
            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}