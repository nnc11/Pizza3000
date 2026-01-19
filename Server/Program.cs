using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Common;
using System.Configuration;

namespace Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "🍕 Pizza Delivery Server";
            var server = new DeliveryServer();
            await server.StartAsync();
        }
    }

    class DeliveryServer
    {
        private readonly int TCP_PORT = int.Parse(ConfigurationManager.AppSettings["TcpPort"] ?? "5000");
        private readonly int UDP_PORT = int.Parse(ConfigurationManager.AppSettings["UdpPort"] ?? "6000");

        private TcpListener _tcpListener;
        private UdpClient _udpBroadcaster;

        // Thread-safe collections
        private ConcurrentDictionary<string, ClientHandler> _drivers = new();
        private ConcurrentDictionary<string, ClientHandler> _customers = new();
        private ConcurrentQueue<Order> _normalOrderQueue = new();
        private ConcurrentQueue<Order> _rushOrderQueue = new(); // BONUS: Rush Orders
        private ConcurrentDictionary<int, Order> _activeOrders = new();

        private int _orderIdCounter = 1000;
        private bool _isRunning = false;

        public async Task StartAsync()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
                _tcpListener.Start();
                _isRunning = true;

                _udpBroadcaster = new UdpClient();
                _udpBroadcaster.EnableBroadcast = true;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("╔════════════════════════════════════════════╗");
                Console.WriteLine("║   🍕 PIZZA DELIVERY SERVER STARTED 🍕    ║");
                Console.WriteLine("╚════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine($"[TCP] Listening on port {TCP_PORT}");
                Console.WriteLine($"[UDP] Broadcasting on port {UDP_PORT}");
                Console.WriteLine();

                // Start background tasks
                _ = Task.Run(() => UdpBroadcastLoop());
                _ = Task.Run(() => OrderAssignmentLoop());
                _ = Task.Run(() => LeaderboardUpdateLoop()); // BONUS: Leaderboard

                // Accept client connections
                while (_isRunning)
                {
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleNewClient(client));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Server error: {ex.Message}");
            }
        }

        private async Task HandleNewClient(TcpClient tcpClient)
        {
            ClientHandler handler = new ClientHandler(tcpClient, this);
            await handler.StartAsync();
        }

        // BONUS: Order Assignment with Rush Priority
        private async Task OrderAssignmentLoop()
        {
            while (_isRunning)
            {
                try
                {
                    // Process ALL pending orders in priority order (rush first)
                    bool foundOrder = true;

                    while (foundOrder && _isRunning)
                    {
                        foundOrder = false;
                        Order orderToAssign = null;

                        // Check rush orders first (BONUS: Priority)
                        if (_rushOrderQueue.TryDequeue(out var rushOrder))
                        {
                            orderToAssign = rushOrder;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[PRIORITY ⚡] Processing rush order #{rushOrder.OrderId}");
                            Console.ResetColor();
                            foundOrder = true;
                        }
                        else if (_normalOrderQueue.TryDequeue(out var normalOrder))
                        {
                            orderToAssign = normalOrder;
                            foundOrder = true;
                        }

                        if (orderToAssign != null)
                        {
                            var assignedDriver = FindBestDriver(orderToAssign);

                            if (assignedDriver != null)
                            {
                                await AssignOrderToDriver(orderToAssign, assignedDriver);

                                // Small delay between assignments for visibility
                                await Task.Delay(2000);
                            }
                            else
                            {
                                // No available drivers, re-queue
                                if (orderToAssign.IsRushOrder)
                                    _rushOrderQueue.Enqueue(orderToAssign);
                                else
                                    _normalOrderQueue.Enqueue(orderToAssign);

                                Console.WriteLine($"[QUEUE] Order #{orderToAssign.OrderId} waiting for available driver...");

                                foundOrder = false; // Stop trying if no drivers available
                            }
                        }
                    }

                    await Task.Delay(500); // Check every 500ms (faster response)
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Order assignment error: {ex.Message}");
                }
            }
        }

        private ClientHandler FindBestDriver(Order order)
        {
            ClientHandler bestDriver = null;
            double minDistance = double.MaxValue;

            Console.WriteLine($"[SEARCH] Finding best driver for order #{order.OrderId}:");

            foreach (var driver in _drivers.Values)
            {
                if (driver.Driver.Status == DriverStatus.Available)
                {
                    double distance = Utils.CalculateDistance(
                        driver.Driver.Latitude, driver.Driver.Longitude,
                        order.Latitude, order.Longitude);

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"   - {driver.Driver.Name}: {distance:F2}km");
                    Console.ResetColor();

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestDriver = driver;
                    }
                }
            }

            if (bestDriver != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   ✓ Selected: {bestDriver.Driver.Name} ({minDistance:F2}km)");
                Console.ResetColor();
            }

            return bestDriver;
        }

        private async Task AssignOrderToDriver(Order order, ClientHandler driver)
        {
            driver.Driver.Status = DriverStatus.Busy;
            order.AssignedDriverId = driver.Driver.DriverId;
            order.Status = OrderStatus.Preparing;
            _activeOrders[order.OrderId] = order;

            double distance = Utils.CalculateDistance(
                driver.Driver.Latitude, driver.Driver.Longitude,
                order.Latitude, order.Longitude);

            // BONUS: Traffic Jam Simulation
            bool trafficJam = Utils.ShouldTrafficJamOccur();
            int deliveryTime = Utils.CalculateDeliveryTime(distance, trafficJam);

            string trafficMsg = trafficJam ? " [TRAFFIC JAM! ]" : "";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ASSIGNED] Order #{order.OrderId} → {driver.Driver.Name} ({distance:F2}km, ETA:  {deliveryTime}s){trafficMsg}");
            Console.ResetColor();

            // Send to driver
            string rushTag = order.IsRushOrder ? "|RUSH" : "";
            await driver.SendMessageAsync($"{Protocol.ORDER_ASSIGNED}|{order.OrderId}|" +
                                         $"{string.Join(",", order.Pizzas)}|{order.DeliveryAddress}|" +
                                         $"{deliveryTime}|{trafficJam}{rushTag}");

            // Notify customer
            if (_customers.TryGetValue(order.CustomerId, out var customer))
            {
                await customer.SendMessageAsync($"{Protocol.ORDER_STATUS_UPDATE}|{order.OrderId}|" +
                                               $"{order.Status}|Assigned to {driver.Driver.Name}");
            }
        }

        // UDP Location Broadcast
        private async Task UdpBroadcastLoop()
        {
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, UDP_PORT);

            while (_isRunning)
            {
                try
                {
                    foreach (var driver in _drivers.Values)
                    {
                        var locationUpdate = new LocationUpdate
                        {
                            DriverId = driver.Driver.DriverId,
                            Latitude = driver.Driver.Latitude,
                            Longitude = driver.Driver.Longitude,
                            Timestamp = DateTime.Now
                        };

                        byte[] data = Encoding.UTF8.GetBytes(locationUpdate.ToString());
                        await _udpBroadcaster.SendAsync(data, data.Length, broadcastEndpoint);
                    }

                    await Task.Delay(5000); // Broadcast every 2 seconds
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] UDP broadcast error: {ex.Message}");
                }
            }
        }

        // BONUS: Leaderboard Update
        private async Task LeaderboardUpdateLoop()
        {
            while (_isRunning)
            {
                try
                {
                    await Task.Delay(30000); // Update every 10 seconds

                    var leaderboard = _drivers.Values
                        .OrderByDescending(d => d.Driver.CompletedOrders)
                        .ThenByDescending(d => d.Driver.GetAverageSatisfaction())
                        .Take(5)
                        .ToList();

                    if (leaderboard.Any())
                    {
                        Console.WriteLine("\n╔═══════════════ 🏆 LEADERBOARD 🏆 ═══════════════╗");
                        int rank = 1;
                        foreach (var driver in leaderboard)
                        {
                            Console.WriteLine($"║ {rank}. {driver.Driver.Name,-15} │ " +
                                            $"Orders: {driver.Driver.CompletedOrders,3} │ " +
                                            $"Satisfaction: {driver.Driver.GetAverageSatisfaction():F1}% ║");
                            rank++;
                        }
                        Console.WriteLine("╚════════════════════════════════════════════════════╝\n");

                        // Broadcast to all clients
                        string leaderboardData = string.Join(";", leaderboard.Select(d =>
                            $"{d.Driver.Name},{d.Driver.CompletedOrders},{d.Driver.GetAverageSatisfaction():F1}"));

                        foreach (var client in _drivers.Values)
                        {
                            await client.SendMessageAsync($"{Protocol.LEADERBOARD_UPDATE}|{leaderboardData}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Leaderboard error: {ex.Message}");
                }
            }
        }

        // Inner class for handling individual clients
        public class ClientHandler
        {
            private TcpClient _tcpClient;
            private StreamReader _reader;
            private StreamWriter _writer;
            private DeliveryServer _server;
            private string _clientId;
            private bool _isDriver;
            public Driver Driver { get; private set; }

            public ClientHandler(TcpClient client, DeliveryServer server)
            {
                _tcpClient = client;
                _server = server;
            }

            public async Task StartAsync()
            {
                try
                {
                    NetworkStream stream = _tcpClient.GetStream();
                    _reader = new StreamReader(stream);
                    _writer = new StreamWriter(stream) { AutoFlush = true };

                    // Read first message to identify client type
                    string identityMessage = await _reader.ReadLineAsync();
                    await HandleIdentityMessage(identityMessage);

                    // Main message loop
                    while (_tcpClient.Connected)
                    {
                        string message = await _reader.ReadLineAsync();
                        if (message == null) break;

                        await HandleMessage(message);
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[DISCONNECT] {_clientId}:  {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Client handler error: {ex.Message}");
                }
                finally
                {
                    Cleanup();
                }
            }

            private async Task HandleIdentityMessage(string message)
            {
                var parts = message.Split('|');

                if (parts[0] == Protocol.DRIVER_REGISTER)
                {
                    string driverName = parts[1];
                    _clientId = $"DRIVER_{driverName}";
                    _isDriver = true;

                    Driver = new Driver
                    {
                        DriverId = _clientId,
                        Name = driverName,
                        Status = DriverStatus.Available
                    };

                    // Random starting location
                    var location = Utils.GenerateRandomLocation();
                    Driver.Latitude = location.lat;
                    Driver.Longitude = location.lon;

                    _server._drivers[_clientId] = this;

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[DRIVER CONNECTED] {driverName} at ({Driver.Latitude:F4}, {Driver.Longitude:F4})");
                    Console.ResetColor();

                    await SendMessageAsync($"{Protocol.SUCCESS}|{_clientId}");
                }
                else if (parts[0] == Protocol.CUSTOMER_REGISTER)
                {
                    string customerName = parts[1];
                    _clientId = $"CUSTOMER_{customerName}";
                    _isDriver = false;

                    _server._customers[_clientId] = this;

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"[CUSTOMER CONNECTED] {customerName}");
                    Console.ResetColor();

                    await SendMessageAsync($"{Protocol.SUCCESS}|{_clientId}");
                }
            }

            private async Task HandleMessage(string message)
            {
                var parts = message.Split('|');
                string command = parts[0];

                switch (command)
                {
                    case Protocol.CUSTOMER_ORDER:
                        await HandleCustomerOrder(parts);
                        break;

                    case Protocol.DRIVER_DELIVERED:
                        await HandleDriverDelivered(parts);
                        break;

                    case Protocol.DRIVER_STEAL_ORDER:  // BONUS: Order Stealing
                        await HandleOrderSteal(parts);
                        break;

                    case Protocol.PING:
                        await SendMessageAsync(Protocol.PONG);
                        break;

                    default:
                        Console.WriteLine($"[UNKNOWN] {_clientId}: {message}");
                        break;
                }
            }

            private async Task HandleCustomerOrder(string[] parts)
            {
                // Format:  CUSTOMER_ORDER|address|isRush
                string address = parts[1];
                bool isRush = parts.Length > 2 && parts[2] == "RUSH";

                var order = new Order
                {
                    OrderId = _server._orderIdCounter++,
                    CustomerId = _clientId,
                    Pizzas = Utils.GenerateRandomPizzas(),
                    DeliveryAddress = address,
                    IsRushOrder = isRush
                };

                var location = Utils.GenerateRandomLocation();
                order.Latitude = location.lat;
                order.Longitude = location.lon;

                if (isRush)
                    _server._rushOrderQueue.Enqueue(order);
                else
                    _server._normalOrderQueue.Enqueue(order);

                string rushTag = isRush ? " [RUSH]" : "";
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[NEW ORDER{rushTag}] #{order.OrderId} - {string.Join(", ", order.Pizzas)} → {address}");
                Console.ResetColor();

                await SendMessageAsync($"{Protocol.ORDER_ACCEPTED}|{order.OrderId}|{string.Join(",", order.Pizzas)}");
            }

            private async Task HandleDriverDelivered(string[] parts)
            {
                int orderId = int.Parse(parts[1]);

                if (_server._activeOrders.TryGetValue(orderId, out var order))
                {
                    order.Status = OrderStatus.Delivered;
                    order.DeliveryTime = DateTime.Now;

                    TimeSpan deliveryDuration = order.DeliveryTime.Value - order.OrderTime;
                    int satisfaction = Utils.CalculateSatisfaction(deliveryDuration);

                    Driver.Status = DriverStatus.Available;
                    Driver.CompletedOrders++;
                    Driver.TotalSatisfactionScore += satisfaction;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[DELIVERED] Order #{orderId} by {Driver.Name} - " +
                                    $"Time: {deliveryDuration.TotalMinutes:F1}min - Satisfaction: {satisfaction}%");
                    Console.ResetColor();

                    // Notify customer
                    if (_server._customers.TryGetValue(order.CustomerId, out var customer))
                    {
                        await customer.SendMessageAsync($"{Protocol.ORDER_STATUS_UPDATE}|{orderId}|" +
                                                       $"{OrderStatus.Delivered}|Delivered!  Satisfaction: {satisfaction}%");
                    }

                    _server._activeOrders.TryRemove(orderId, out _);
                }
            }

            // BONUS: Order Stealing Mechanic
            private async Task HandleOrderSteal(string[] parts)
            {
                int targetOrderId = int.Parse(parts[1]);

                if (_server._activeOrders.TryGetValue(targetOrderId, out var order))
                {
                    // Check if order is stealable (less than 30% delivered)
                    var originalDriver = _server._drivers.Values.FirstOrDefault(d => d.Driver.DriverId == order.AssignedDriverId);

                    if (originalDriver != null && Driver.Status == DriverStatus.Available)
                    {
                        double originalDistance = Utils.CalculateDistance(
                            originalDriver.Driver.Latitude, originalDriver.Driver.Longitude,
                            order.Latitude, order.Longitude);

                        double thiefDistance = Utils.CalculateDistance(
                            Driver.Latitude, Driver.Longitude,
                            order.Latitude, order.Longitude);

                        // Can steal if significantly closer (50% closer)
                        if (thiefDistance < originalDistance * 0.5)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[ORDER STOLEN! ] Order #{targetOrderId} stolen by {Driver.Name} from {originalDriver.Driver.Name}");
                            Console.ResetColor();

                            // Reassign
                            originalDriver.Driver.Status = DriverStatus.Available;
                            await originalDriver.SendMessageAsync($"{Protocol.ERROR}|Order #{targetOrderId} was stolen!");

                            order.AssignedDriverId = Driver.DriverId;
                            Driver.Status = DriverStatus.Busy;

                            await SendMessageAsync($"{Protocol.ORDER_ASSIGNED}|{order.OrderId}|{string.Join(",", order.Pizzas)}|{order.DeliveryAddress}");
                        }
                        else
                        {
                            await SendMessageAsync($"{Protocol.ERROR}|Cannot steal order - not close enough");
                        }
                    }
                }
            }

            public async Task SendMessageAsync(string message)
            {
                try
                {
                    await _writer.WriteLineAsync(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Send message failed: {ex.Message}");
                }
            }

            private void Cleanup()
            {
                if (_isDriver)
                {
                    _server._drivers.TryRemove(_clientId, out _);
                    Console.WriteLine($"[DRIVER DISCONNECTED] {_clientId}");
                }
                else
                {
                    _server._customers.TryRemove(_clientId, out _);
                    Console.WriteLine($"[CUSTOMER DISCONNECTED] {_clientId}");
                }

                _reader?.Close();
                _writer?.Close();
                _tcpClient?.Close();
            }
        }
    }
}