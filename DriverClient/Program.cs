using System;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using Common;
using System.Configuration;

namespace DriverClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "🚗 Pizza Delivery Driver";
            var driver = new DriverApplication();
            await driver.RunAsync();
        }
    }

    class DriverApplication
    {
        private readonly string SERVER_IP = ConfigurationManager.AppSettings["ServerIP"] ?? "127.0.0.1";
        private readonly int TCP_PORT = int.Parse(ConfigurationManager.AppSettings["TcpPort"] ?? "5000");

        private TcpClient _tcpClient;
        private StreamReader _reader;
        private StreamWriter _writer;

        private string _driverId;
        private string _driverName;
        private bool _isConnected = false;
        private bool _isBusy = false;

        private Driver _driverInfo;

        public async Task RunAsync()
        {
            try
            {
                DisplayWelcomeBanner();

                // Get driver name
                Console.Write("Enter your driver name: ");
                _driverName = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(_driverName))
                {
                    Console.WriteLine("[ERROR] Invalid name!");
                    return;
                }

                _driverInfo = new Driver
                {
                    Name = _driverName,
                    Status = DriverStatus.Available
                };

                // Connect to server
                await ConnectToServerAsync();

                // Start background tasks
                _ = Task.Run(() => ReceiveMessagesAsync());
                _ = Task.Run(() => SimulateDriverMovementAsync());

                // Main menu loop
                await MainMenuAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Fatal error: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private void DisplayWelcomeBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║     🚗 PIZZA DELIVERY DRIVER APP 🚗      ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private async Task ConnectToServerAsync()
        {
            try
            {
                Console.WriteLine($"[CONNECTING] Connecting to server at {SERVER_IP}:{TCP_PORT}...");

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(SERVER_IP, TCP_PORT);

                NetworkStream stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };

                // Register as driver
                await _writer.WriteLineAsync($"{Protocol.DRIVER_REGISTER}|{_driverName}");

                // Wait for confirmation
                string response = await _reader.ReadLineAsync();
                var parts = response.Split('|');

                if (parts[0] == Protocol.SUCCESS)
                {
                    _driverId = parts[1];
                    _isConnected = true;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[CONNECTED] Successfully connected as {_driverId}");
                    Console.WriteLine($"[STATUS] Ready to accept orders!");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                else
                {
                    throw new Exception("Server rejected connection");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Connection failed: {ex.Message}");
                throw;
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                while (_isConnected && _tcpClient.Connected)
                {
                    string message = await _reader.ReadLineAsync();
                    if (message == null) break;

                    await HandleServerMessageAsync(message);
                }
            }
            catch (IOException)
            {
                Console.WriteLine("[DISCONNECTED] Lost connection to server");
                _isConnected = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Receive error: {ex.Message}");
            }
        }

        private async Task HandleServerMessageAsync(string message)
        {
            var parts = message.Split('|');
            string command = parts[0];

            switch (command)
            {
                case Protocol.ORDER_ASSIGNED:
                    await HandleOrderAssigned(parts);
                    break;

                case Protocol.LEADERBOARD_UPDATE:
                    HandleLeaderboardUpdate(parts);
                    break;

                case Protocol.ERROR:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[SERVER ERROR] {parts[1]}");
                    Console.ResetColor();
                    break;

                case Protocol.PONG:
                    // Heartbeat response
                    break;

                default:
                    Console.WriteLine($"[SERVER] {message}");
                    break;
            }
        }

        private async Task HandleOrderAssigned(string[] parts)
        {
            // Format: ORDER_ASSIGNED|orderId|pizzas|address|deliveryTime|trafficJam|RUSH(optional)
            int orderId = int.Parse(parts[1]);
            string pizzas = parts[2];
            string address = parts[3];
            int deliveryTimeSeconds = int.Parse(parts[4]);
            bool hasTrafficJam = bool.Parse(parts[5]);
            bool isRush = parts.Length > 6 && parts[6] == "RUSH";

            _isBusy = true;
            _driverInfo.Status = DriverStatus.Busy;

            Console.WriteLine("\n" + new string('═', 60));
            Console.ForegroundColor = isRush ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.WriteLine($"🍕 NEW ORDER ASSIGNED! {(isRush ? "[⚡ RUSH ORDER]" : "")}");
            Console.ResetColor();
            Console.WriteLine($"Order ID: #{orderId}");
            Console.WriteLine($"Pizzas: {pizzas.Replace(",", ", ")}");
            Console.WriteLine($"Address: {address}");
            Console.WriteLine($"ETA: {deliveryTimeSeconds / 60} minutes {deliveryTimeSeconds % 60} seconds");

            if (hasTrafficJam)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("⚠️ TRAFFIC JAM DETECTED - Delivery delayed!");
                Console.ResetColor();
            }

            Console.WriteLine(new string('═', 60));
            Console.WriteLine();

            // Simulate delivery
            await SimulateDeliveryAsync(orderId, deliveryTimeSeconds, hasTrafficJam);
        }

        private async Task SimulateDeliveryAsync(int orderId, int deliveryTime, bool hasTrafficJam)
        {
            Console.WriteLine($"[DELIVERING] Starting delivery for order #{orderId}.. .");

            // Show progress bar
            int steps = 10;
            int stepTime = deliveryTime * 1000 / steps;

            for (int i = 0; i <= steps; i++)
            {
                int percentage = i * 10;
                string progressBar = new string('█', i) + new string('░', steps - i);

                Console.Write($"\r[PROGRESS] [{progressBar}] {percentage}% ");

                if (hasTrafficJam && i == steps / 2)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(" 🚦 TRAFFIC JAM!");
                    Console.ResetColor();
                }

                await Task.Delay(stepTime);
            }

            Console.WriteLine("\n");

            // Mark as delivered
            await _writer.WriteLineAsync($"{Protocol.DRIVER_DELIVERED}|{orderId}");

            _isBusy = false;
            _driverInfo.Status = DriverStatus.Available;
            _driverInfo.CompletedOrders++;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ [DELIVERED] Order #{orderId} completed successfully!");
            Console.WriteLine($"[STATUS] Available for new orders");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void HandleLeaderboardUpdate(string[] parts)
        {
            // Format: LEADERBOARD_UPDATE|driver1,orders,satisfaction;driver2,orders,satisfaction... 
            if (parts.Length < 2) return;

            var drivers = parts[1].Split(';');

            Console.WriteLine("\n╔═══════════════ 🏆 DRIVER LEADERBOARD 🏆 ═══════════════╗");

            int rank = 1;
            foreach (var driverData in drivers)
            {
                var data = driverData.Split(',');
                if (data.Length < 3) continue;

                string name = data[0];
                string orders = data[1];
                string satisfaction = data[2];

                ConsoleColor color = name == _driverName ? ConsoleColor.Yellow : ConsoleColor.White;
                Console.ForegroundColor = color;

                string medal = rank == 1 ? "🥇" : rank == 2 ? "🥈" : rank == 3 ? "🥉" : "  ";
                Console.WriteLine($"║ {medal} {rank}. {name,-20} │ {orders,3} orders │ {satisfaction,5}% ║");

                Console.ResetColor();
                rank++;
            }

            Console.WriteLine("╚═══════════════════════════════════════════════════════╝\n");
        }

        private async Task SimulateDriverMovementAsync()
        {
            // Simulate driver moving around (random location updates)
            Random random = new Random();

            while (_isConnected)
            {
                await Task.Delay(10000); // Update every 10 seconds

                if (_isBusy)
                {
                    // Simulate movement towards delivery
                    _driverInfo.Latitude += (random.NextDouble() - 0.5) * 0.01;
                    _driverInfo.Longitude += (random.NextDouble() - 0.5) * 0.01;
                }

                _driverInfo.LastLocationUpdate = DateTime.Now;
            }
        }

        private async Task MainMenuAsync()
        {
            while (_isConnected)
            {
                Console.WriteLine("\n╔════════════ DRIVER MENU ════════════╗");
                Console.WriteLine("║ 1. View Status                      ║");
                Console.WriteLine("║ 2. Attempt Order Steal (BONUS)      ║");
                Console.WriteLine("║ 3. View Stats                       ║");
                Console.WriteLine("║ 4. Exit                             ║");
                Console.WriteLine("╚═════════════════════════════════════╝");
                Console.Write("Select option: ");

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        DisplayStatus();
                        break;

                    case "2":
                        await AttemptOrderSteal();
                        break;

                    case "3":
                        DisplayStats();
                        break;

                    case "4":
                        Console.WriteLine("[LOGOUT] Disconnecting...");
                        _isConnected = false;
                        return;

                    default:
                        Console.WriteLine("[ERROR] Invalid option!");
                        break;
                }

                await Task.Delay(100);
            }
        }

        private void DisplayStatus()
        {
            Console.WriteLine($"\n📊 Driver Status:");
            Console.WriteLine($"   Name: {_driverName}");
            Console.WriteLine($"   ID: {_driverId}");
            Console.WriteLine($"   Status: {(_isBusy ? "🔴 BUSY (Delivering)" : "🟢 AVAILABLE")}");
            Console.WriteLine($"   Location: ({_driverInfo.Latitude:F4}, {_driverInfo.Longitude:F4})");
            Console.WriteLine($"   Completed Orders: {_driverInfo.CompletedOrders}");
        }

        private async Task AttemptOrderSteal()
        {
            if (_isBusy)
            {
                Console.WriteLine("[ERROR] You are currently busy with a delivery!");
                return;
            }

            Console.Write("Enter Order ID to steal: ");
            if (int.TryParse(Console.ReadLine(), out int orderId))
            {
                Console.WriteLine($"[ATTEMPT] Trying to steal order #{orderId}.. .");
                await _writer.WriteLineAsync($"{Protocol.DRIVER_STEAL_ORDER}|{orderId}");
            }
            else
            {
                Console.WriteLine("[ERROR] Invalid Order ID!");
            }
        }

        private void DisplayStats()
        {
            Console.WriteLine($"\n📈 Your Stats:");
            Console.WriteLine($"   Total Deliveries: {_driverInfo.CompletedOrders}");
            Console.WriteLine($"   Average Satisfaction: {_driverInfo.GetAverageSatisfaction():F1}%");
            Console.WriteLine($"   Last Update: {_driverInfo.LastLocationUpdate: HH:mm:ss}");
        }

        private void Cleanup()
        {
            _reader?.Close();
            _writer?.Close();
            _tcpClient?.Close();
            Console.WriteLine("[DISCONNECTED] Driver app closed.");
        }
    }
}