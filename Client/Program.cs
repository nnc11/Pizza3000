using System;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using Common;
using System.Configuration;

namespace CustomerClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "🍕 Pizza Ordering Customer";
            var customer = new CustomerApplication();
            await customer.RunAsync();
        }
    }

    class CustomerApplication
    {
        private readonly string SERVER_IP = ConfigurationManager.AppSettings["ServerIP"] ?? "127.0.0.1";
        private readonly int TCP_PORT = int.Parse(ConfigurationManager.AppSettings["TcpPort"] ?? "5000");
        private readonly int UDP_PORT = int.Parse(ConfigurationManager.AppSettings["UdpPort"] ?? "6000");

        private TcpClient _tcpClient;
        private StreamReader _reader;
        private StreamWriter _writer;
        private UdpClient _udpListener;

        private string _customerId;
        private string _customerName;
        private bool _isConnected = false;
        private bool _udpEnabled = false;

        private Dictionary<int, string> _myOrders = new Dictionary<int, string>();

        public async Task RunAsync()
        {
            try
            {
                DisplayWelcomeBanner();

                // Get customer name
                Console.Write("Enter your name: ");
                _customerName = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(_customerName))
                {
                    Console.WriteLine("[ERROR] Invalid name!");
                    return;
                }

                // Connect to server
                await ConnectToServerAsync();

                // Start background tasks
                _ = Task.Run(() => ReceiveMessagesAsync());
                _ = Task.Run(() => TryStartUdpListenerAsync()); // Non-blocking UDP

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
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║      🍕 PIZZA ORDERING SYSTEM 🍕         ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private async Task ConnectToServerAsync()
        {
            try
            {
                Console.WriteLine($"[CONNECTING] Connecting to restaurant at {SERVER_IP}:{TCP_PORT}...");

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(SERVER_IP, TCP_PORT);

                NetworkStream stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };

                // Register as customer
                await _writer.WriteLineAsync($"{Protocol.CUSTOMER_REGISTER}|{_customerName}");

                // Wait for confirmation
                string response = await _reader.ReadLineAsync();
                var parts = response.Split('|');

                if (parts[0] == Protocol.SUCCESS)
                {
                    _customerId = parts[1];
                    _isConnected = true;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[CONNECTED] Welcome, {_customerName}!");
                    Console.WriteLine($"[ID] Your customer ID: {_customerId}");
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

        // NON-BLOCKING UDP LISTENER
        private async Task TryStartUdpListenerAsync()
        {
            try
            {
                // Try to bind to UDP port (might fail if another customer is running)
                _udpListener = new UdpClient();
                _udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Client.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, UDP_PORT));
                _udpEnabled = true;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[UDP] Driver location tracking enabled ✓");
                Console.ResetColor();

                await ListenForDriverLocationsAsync();
            }
            catch (SocketException)
            {
                // UDP port already in use (another customer running)
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[UDP] Location tracking unavailable (another customer is using it)");
                Console.WriteLine("[INFO] You will still receive order updates via TCP");
                Console.ResetColor();
                _udpEnabled = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] UDP listener error: {ex.Message}");
                _udpEnabled = false;
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
                case Protocol.ORDER_ACCEPTED:
                    HandleOrderAccepted(parts);
                    break;

                case Protocol.ORDER_STATUS_UPDATE:
                    HandleOrderStatusUpdate(parts);
                    break;

                case Protocol.LEADERBOARD_UPDATE:
                    HandleLeaderboardUpdate(parts);
                    break;

                case Protocol.ERROR:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] {parts[1]}");
                    Console.ResetColor();
                    break;

                default:
                    Console.WriteLine($"[SERVER] {message}");
                    break;
            }

            await Task.CompletedTask;
        }

        private void HandleOrderAccepted(string[] parts)
        {
            // Format: ORDER_ACCEPTED|orderId|pizzas
            int orderId = int.Parse(parts[1]);
            string pizzas = parts[2];

            _myOrders[orderId] = pizzas;

            Console.WriteLine("\n" + new string('═', 60));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ ORDER CONFIRMED!");
            Console.ResetColor();
            Console.WriteLine($"Order ID: #{orderId}");
            Console.WriteLine($"Pizzas: {pizzas.Replace(",", ", ")}");
            Console.WriteLine("Status: Waiting for driver assignment...");
            Console.WriteLine(new string('═', 60));
            Console.WriteLine();
        }

        private void HandleOrderStatusUpdate(string[] parts)
        {
            // Format: ORDER_STATUS_UPDATE|orderId|status|details
            int orderId = int.Parse(parts[1]);
            string status = parts[2];
            string details = parts.Length > 3 ? parts[3] : "";

            Console.WriteLine($"\n📦 [ORDER #{orderId}] Status Update:");

            switch (status)
            {
                case "Preparing":
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   🔥 Your pizza is being prepared!");
                    Console.WriteLine($"   👨‍🍳 {details}");
                    break;

                case "OutForDelivery":
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"   🚗 Your pizza is on the way!");
                    Console.WriteLine($"   📍 {details}");
                    break;

                case "Delivered":
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   ✅ Delivered!");
                    Console.WriteLine($"   ⭐ {details}");
                    _myOrders.Remove(orderId);
                    break;

                default:
                    Console.WriteLine($"   Status: {status}");
                    Console.WriteLine($"   Details: {details}");
                    break;
            }

            Console.ResetColor();
            Console.WriteLine();
        }

        private string _latestLeaderboard = ""; // Class'a field ekle (üstte)

        private void HandleLeaderboardUpdate(string[] parts)
        {
            // Format: LEADERBOARD_UPDATE|driver1,orders,satisfaction;driver2,orders,satisfaction... 
            if (parts.Length < 2) return;

            // Silently store leaderboard (don't spam console)
            _latestLeaderboard = parts[1];

            // Optional: Show notification once
            // Console.WriteLine("[INFO] Leaderboard updated (type 4 to view)");
        }
        private async Task ListenForDriverLocationsAsync()
        {
            if (!_udpEnabled) return;

            Dictionary<string, LocationUpdate> driverLocations = new Dictionary<string, LocationUpdate>();

            try
            {
                while (_isConnected && _udpEnabled)
                {
                    var result = await _udpListener.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);

                    var locationUpdate = LocationUpdate.Parse(message);
                    if (locationUpdate != null)
                    {
                        // Silently track driver locations (no console spam)
                        driverLocations[locationUpdate.DriverId] = locationUpdate;

                        // Optional:  Uncomment below to see updates every 30 seconds
                        // if (DateTime.Now.Second % 30 == 0)
                        // {
                        //     Console.WriteLine($"[UDP] Tracking {driverLocations.Count} driver(s) - Last update: {DateTime.Now:HH:mm:ss}");
                        // }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_udpEnabled)
                {
                    Console.WriteLine($"[WARNING] UDP listener stopped:  {ex.Message}");
                }
            }
        }

        private async Task MainMenuAsync()
        {
            while (_isConnected)
            {
                Console.WriteLine("\n╔════════════ CUSTOMER MENU ════════════╗");
                Console.WriteLine("║ 1. Order Pizza                        ║");
                Console.WriteLine("║ 2. Order RUSH Pizza (Priority)        ║");
                Console.WriteLine("║ 3. View My Orders                     ║");
                Console.WriteLine("║ 4. Exit                               ║");
                Console.WriteLine("╚═══════════════════════════════════════╝");
                Console.Write("Select option: ");

                string input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        await PlaceOrder(isRush: false);
                        break;

                    case "2":
                        await PlaceOrder(isRush: true);
                        break;

                    case "3":
                        DisplayMyOrders();
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

        private async Task PlaceOrder(bool isRush)
        {
            Console.WriteLine($"\n🍕 {(isRush ? "RUSH " : "")}Pizza Order:");
            Console.Write("Enter delivery address: ");
            string address = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(address) || address.Length < 5)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Invalid address! Must be at least 5 characters.");
                Console.ResetColor();
                return;
            }

            string rushFlag = isRush ? "|RUSH" : "";
            await _writer.WriteLineAsync($"{Protocol.CUSTOMER_ORDER}|{address}{rushFlag}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ORDERING] {(isRush ? "⚡ RUSH " : "")}Order placed!  Waiting for confirmation...");
            Console.ResetColor();
        }

        private void DisplayMyOrders()
        {
            Console.WriteLine("\n📋 Your Active Orders:");

            if (_myOrders.Count == 0)
            {
                Console.WriteLine("   No active orders");
            }
            else
            {
                foreach (var order in _myOrders)
                {
                    Console.WriteLine($"   Order #{order.Key}: {order.Value.Replace(",", ", ")} (In Progress)");
                }
            }
        }

        private void Cleanup()
        {
            _reader?.Close();
            _writer?.Close();
            _tcpClient?.Close();

            if (_udpEnabled)
            {
                _udpListener?.Close();
            }

            Console.WriteLine("[DISCONNECTED] Customer app closed.");
        }
    }
}