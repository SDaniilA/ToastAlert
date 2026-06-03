using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using ToastAlert.Config;
using ToastAlert.Models;
using ToastAlert.Utilities;

namespace ToastAlert.Services
{
    public class MqttService
    {
        private readonly Config.Config _config;
        private IMqttClient? _client;
        private bool _connected = false;
        private Timer? _reconnectTimer;
        private int _reconnectAttempts = 0;
        private DateTime _reconnectStartTime;
        private readonly object _reconnectLock = new();
        private bool _manualDisconnect = false;
        private readonly Stats _stats;
        private Mutex? _clientIdMutex;

        public MqttService(Config.Config config, Stats stats)
        {
            _config = config;
            _stats = stats;
        }

        public async Task<bool> CheckAndResolveConflictAsync()
        {
            if (!_config.Mqtt.Enabled || !_config.Additional.MqttEnabled)
                return false;

            string mutexName = $"MqttClient_{_config.Mqtt.ClientId}";
            bool createdNew = false;
            try
            {
                _clientIdMutex = new Mutex(true, mutexName, out createdNew);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка создания мьютекса: {ex.Message}");
                createdNew = false;
                _clientIdMutex = null;
            }

            if (createdNew)
            {
                Console.WriteLine($"✅ Мьютекс для ClientId = {_config.Mqtt.ClientId} захвачен.");
                await Task.CompletedTask;
                return true;
            }
            else
            {
                if (_clientIdMutex != null)
                {
                    _clientIdMutex.Dispose();
                    _clientIdMutex = null;
                }

                Console.WriteLine($"\n⚠️ MQTT: другой экземпляр уже использует ClientId = {_config.Mqtt.ClientId}");
                Console.Write("   Сгенерировать новый уникальный ID? (Y/N, Enter = N): ");
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Y)
                {
                    string newId = $"telegram_alert_{Environment.MachineName}_{Guid.NewGuid():N}";
                    if (newId.Length > 23) newId = newId.Substring(0, 23);
                    _config.Mqtt.ClientId = newId;
                    ConfigLoader.Save(_config);
                    Console.WriteLine($"\n✅ Сгенерирован новый ClientId: {newId}");
                    string newMutexName = $"MqttClient_{_config.Mqtt.ClientId}";
                    try
                    {
                        _clientIdMutex = new Mutex(true, newMutexName, out createdNew);
                        await Task.CompletedTask;
                        return createdNew;
                    }
                    catch
                    {
                        await Task.CompletedTask;
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("\n   MQTT будет отключён (режим теста без отправки).");
                    _config.Mqtt.Enabled = false;
                    _config.Additional.MqttEnabled = false;
                    _config.Mqtt.TestMode = true;
                    ConfigLoader.Save(_config);
                    await Task.CompletedTask;
                    return false;
                }
            }
        }

        public async Task InitializeAsync()
        {
            if (!_config.Mqtt.Enabled || !_config.Additional.MqttEnabled)
            {
                Console.WriteLine("📡 MQTT отключен в настройках\n");
                return;
            }

            StopReconnectTimer();

            if (_client != null)
            {
                try
                {
                    await _client.DisconnectAsync();
                    _client.Dispose();
                }
                catch { }
                _client = null;
            }

            try
            {
                var factory = new MqttFactory();
                _client = factory.CreateMqttClient();
                _client.DisconnectedAsync += OnDisconnected;

                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_config.Mqtt.Broker, _config.Mqtt.Port)
                    .WithClientId(_config.Mqtt.ClientId)
                    .WithCleanSession()
                    .Build();

                var result = await _client.ConnectAsync(options);
                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _connected = true;
                    StopReconnectTimer();
                    Console.WriteLine($"✅ MQTT подключён к {_config.Mqtt.Broker}:{_config.Mqtt.Port}\n");
                }
                else
                {
                    Console.WriteLine($"⚠️ MQTT ошибка подключения: {result.ResultCode}\n");
                    if (_config.Mqtt.ReconnectEnabled) StartReconnectTimer();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ MQTT ошибка: {ex.Message}\n");
                if (_config.Mqtt.ReconnectEnabled) StartReconnectTimer();
            }
        }

        private Task OnDisconnected(MqttClientDisconnectedEventArgs args)
        {
            Console.WriteLine($"⚠️ MQTT: соединение потеряно. Причина: {args.Reason}");
            _connected = false;
            if (_config.Mqtt.ReconnectEnabled && !_manualDisconnect && args.ClientWasConnected)
                StartReconnectTimer();
            return Task.CompletedTask;
        }

        private void StartReconnectTimer()
        {
            lock (_reconnectLock)
            {
                if (_reconnectTimer != null) return;
                _reconnectAttempts = 0;
                _reconnectStartTime = DateTime.Now;
                int intervalSec = _config.HealthCheck.IntervalSeconds;
                _reconnectTimer = new Timer(async _ => await TryReconnect(), null,
                    TimeSpan.FromSeconds(intervalSec), TimeSpan.FromSeconds(intervalSec));
                Console.WriteLine($"🔄 MQTT: reconnect запущен (интервал {intervalSec}с)");
            }
        }

        private void StopReconnectTimer()
        {
            lock (_reconnectLock)
            {
                _reconnectTimer?.Dispose();
                _reconnectTimer = null;
                _reconnectAttempts = 0;
                Console.WriteLine("✅ MQTT: reconnect остановлен");
            }
        }

        private async Task TryReconnect()
        {
            if (_connected || _manualDisconnect || _client == null)
            {
                StopReconnectTimer();
                return;
            }
            int maxAttempts = _config.HealthCheck.MaxAttempts;
            int maxTimeSec = _config.HealthCheck.MaxRetryTimeSeconds;
            _reconnectAttempts++;
            if ((maxAttempts > 0 && _reconnectAttempts > maxAttempts) ||
                (maxTimeSec > 0 && (DateTime.Now - _reconnectStartTime).TotalSeconds > maxTimeSec))
            {
                Console.WriteLine($"❌ MQTT: reconnect остановлен (попыток: {_reconnectAttempts})");
                StopReconnectTimer();
                return;
            }
            Console.WriteLine($"🔄 MQTT: попытка переподключения #{_reconnectAttempts}...");
            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_config.Mqtt.Broker, _config.Mqtt.Port)
                    .WithClientId(_config.Mqtt.ClientId)
                    .WithCleanSession()
                    .Build();
                var result = await _client.ConnectAsync(options);
                if (result.ResultCode == MqttClientConnectResultCode.Success)
                {
                    _connected = true;
                    Console.WriteLine($"✅ MQTT: связь восстановлена! (попытка #{_reconnectAttempts})");
                    StopReconnectTimer();
                }
                else Console.WriteLine($"❌ MQTT: reconnect failed (#{_reconnectAttempts}): {result.ResultCode}");
            }
            catch (Exception ex) { Console.WriteLine($"❌ MQTT: reconnect ошибка: {ex.Message}"); }
        }

        public async Task PublishAsync(string sender, string message)
        {
            // Тестовый режим — работает без подключения и без проверки Enabled
            if (_config.Mqtt.TestMode)
            {
                string mqttText = StringUtilities.SanitizeForMqtt(sender, message, _config.Mqtt.MaxPayloadBytes);
                int originalLen = Encoding.UTF8.GetBytes(message).Length;
                int truncatedLen = Encoding.UTF8.GetBytes(mqttText).Length;
                if (truncatedLen < originalLen)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   📏 [TEST] сообщение обрезано: {originalLen} → {truncatedLen} байт (лимит {_config.Mqtt.MaxPayloadBytes})");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"📏 [TEST] сообщение  {originalLen} → {truncatedLen} байт (лимит {_config.Mqtt.MaxPayloadBytes})");
                }

                var payload = new
                {
                    from = _config.Mqtt.FromNodeId,
                    to = _config.Mqtt.ToNodeId,
                    channel = _config.Mqtt.ChannelIndex,
                    type = "sendtext",
                    payload = mqttText,
                    hopLimit = 3
                };
                string jsonPayload = JsonConvert.SerializeObject(payload);
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"   📡 [TEST MODE] MQTT payload: {jsonPayload}");
                Console.ResetColor();
                return;
            }

            // Реальная отправка — требуются оба флага и активное подключение
            if (!_config.Mqtt.Enabled || !_config.Additional.MqttEnabled)
                return;
            if (_client == null || !_connected)
                return;

            string mqttTextReal = StringUtilities.SanitizeForMqtt(sender, message, _config.Mqtt.MaxPayloadBytes);
            var payloadReal = new
            {
                from = _config.Mqtt.FromNodeId,
                to = _config.Mqtt.ToNodeId,
                channel = _config.Mqtt.ChannelIndex,
                type = "sendtext",
                payload = mqttTextReal,
                hopLimit = 3
            };
            string jsonPayloadReal = JsonConvert.SerializeObject(payloadReal);

            try
            {
                var appMsg = new MqttApplicationMessageBuilder()
                    .WithTopic(_config.Mqtt.Topic)
                    .WithPayload(Encoding.UTF8.GetBytes(jsonPayloadReal))
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                await _client.PublishAsync(appMsg);
                Interlocked.Increment(ref _stats.MqttSent);
                if (_config.Monitoring.ConsoleOutputEnabled)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   📡 MQTT: отправлено → {_config.Mqtt.Topic}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex) { Console.WriteLine($"   ⚠️ MQTT публикация: {ex.Message}"); }
        }

        public async Task DisconnectAsync()
        {
            StopReconnectTimer();
            if (_client != null && _connected)
            {
                _manualDisconnect = true;
                try
                {
                    await _client.DisconnectAsync();
                }
                catch { }
                finally
                {
                    _client.Dispose();
                    _client = null;
                    _connected = false;
                    _manualDisconnect = false;
                }
            }
            // Освобождаем мьютекс в любом случае
            if (_clientIdMutex != null)
            {
                try
                {
                    _clientIdMutex.ReleaseMutex();
                }
                catch (ApplicationException) { }
                _clientIdMutex.Dispose();
                _clientIdMutex = null;
            }
            Console.WriteLine("   ✅ MQTT отключён");
        }

        public async Task ToggleAsync()
        {
            if (_connected)
            {
                await DisconnectAsync();
                _config.Additional.MqttEnabled = false;
                ConfigLoader.Save(_config);
                Console.WriteLine("\n❌ MQTT ВЫКЛЮЧЁН");
            }
            else
            {
                _config.Additional.MqttEnabled = true;
                ConfigLoader.Save(_config);
                await InitializeAsync();
                if (_connected)
                    Console.WriteLine("\n✅ MQTT ВКЛЮЧЁН");
                else
                    Console.WriteLine("\n⚠️ Не удалось подключить MQTT");
            }
        }
    }
}