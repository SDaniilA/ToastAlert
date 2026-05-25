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

        public MqttService(Config.Config config, Stats stats)
        {
            _config = config;
            _stats = stats;
        }

        public async Task InitializeAsync()
        {
            if (!_config.Mqtt.Enabled || !_config.Additional.MqttEnabled)
            {
                Console.WriteLine("📡 MQTT отключен в настройках\n");
                return;
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
            if (_connected || _manualDisconnect)
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
                if (_client == null) return;
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
            if (!_config.Mqtt.Enabled || _client == null || !_connected) return;
            string mqttText = StringUtilities.SanitizeForMqtt(sender, message, _config.Mqtt.MaxPayloadBytes);
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
            try
            {
                var appMsg = new MqttApplicationMessageBuilder()
                    .WithTopic(_config.Mqtt.Topic)
                    .WithPayload(Encoding.UTF8.GetBytes(jsonPayload))
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
			if (_client != null && _connected)
			{
				_manualDisconnect = true;
				try
				{
					// Таймаут 1 секунда на отключение
					var disconnectTask = _client.DisconnectAsync();
					await Task.WhenAny(disconnectTask, Task.Delay(1000));
					_client.DisconnectedAsync -= OnDisconnected;
					_client.Dispose();
					Console.WriteLine("   ✅ MQTT отключён");
				}
				finally
				{
					_manualDisconnect = false;
					_connected = false;
				}
			}
		}

        public async Task ToggleAsync()
        {
            if (_connected)
            {
                await DisconnectAsync();
                _connected = false;
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
