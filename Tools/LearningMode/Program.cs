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
            Console.WriteLine("Toast Alert Learning Mode - сбор приложений");
            Console.WriteLine("================================================");
            Console.WriteLine("Программа будет собирать имена приложений,");
            Console.WriteLine("от которых поступают уведомления.");
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

            var uniqueApps = new HashSet<string>();
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
                            if (uniqueApps.Add(appName))
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Обнаружено новое приложение: {appName}");
                            }
                        }
                    }
                    await Task.Delay(2000, cts.Token);
                }
            }
            catch (OperationCanceledException) { }

            Console.WriteLine("\n================================================");
            Console.WriteLine("Сбор завершён. Найдены следующие приложения:");
            Console.WriteLine("================================================");
            var sortedApps = uniqueApps.OrderBy(a => a).ToList();
            for (int i = 0; i < sortedApps.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {sortedApps[i]}");
            }

            string outputFile = "discovered_apps.txt";
            File.WriteAllLines(outputFile, sortedApps);
            Console.WriteLine($"\n✅ Список сохранён в файл: {outputFile}");
            Console.WriteLine("Скопируйте нужные имена в config.json в секцию Monitoring -> AllowedApps.");
            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}