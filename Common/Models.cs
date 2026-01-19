using System;
using System.Collections.Generic;

namespace Common
{
    // Order Status Enum
    public enum OrderStatus
    {
        Pending,
        Preparing,
        OutForDelivery,
        Delivered,
        Cancelled
    }

    // Driver Status Enum
    public enum DriverStatus
    {
        Available,
        Busy,
        Offline
    }

    // Order Model
    public class Order
    {
        public int OrderId { get; set; }
        public string CustomerId { get; set; }
        public List<string> Pizzas { get; set; }
        public string DeliveryAddress { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime OrderTime { get; set; }
        public DateTime? DeliveryTime { get; set; }
        public string AssignedDriverId { get; set; }
        public bool IsRushOrder { get; set; } // BONUS:  Rush Orders
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        public Order()
        {
            Pizzas = new List<string>();
            OrderTime = DateTime.Now;
            Status = OrderStatus.Pending;
        }

        public override string ToString()
        {
            string rush = IsRushOrder ? " [RUSH]" : "";
            return $"Order #{OrderId}{rush} - {string.Join(", ", Pizzas)} to {DeliveryAddress} - Status: {Status}";
        }
    }

    // Driver Model
    public class Driver
    {
        public string DriverId { get; set; }
        public string Name { get; set; }
        public DriverStatus Status { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int CompletedOrders { get; set; }
        public double TotalSatisfactionScore { get; set; }
        public DateTime LastLocationUpdate { get; set; }

        public Driver()
        {
            Status = DriverStatus.Available;
            CompletedOrders = 0;
            TotalSatisfactionScore = 0;
            LastLocationUpdate = DateTime.Now;
        }

        public double GetAverageSatisfaction()
        {
            return CompletedOrders > 0 ? TotalSatisfactionScore / CompletedOrders : 0;
        }
    }

    // Location Update (for UDP)
    public class LocationUpdate
    {
        public string DriverId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"LOCATION|{DriverId}|{Latitude:F4}|{Longitude:F4}|{Timestamp: yyyy-MM-dd HH: mm:ss}";
        }

        public static LocationUpdate Parse(string data)
        {
            var parts = data.Split('|');
            if (parts.Length != 5 || parts[0] != "LOCATION")
                return null;

            return new LocationUpdate
            {
                DriverId = parts[1],
                Latitude = double.Parse(parts[2]),
                Longitude = double.Parse(parts[3]),
                Timestamp = DateTime.Parse(parts[4])
            };
        }
    }

    // Protocol Messages
    public static class Protocol
    {
        // Client to Server
        public const string DRIVER_REGISTER = "DRIVER_REGISTER";
        public const string DRIVER_READY = "DRIVER_READY";
        public const string DRIVER_DELIVERED = "DRIVER_DELIVERED";
        public const string DRIVER_STEAL_ORDER = "DRIVER_STEAL_ORDER"; // BONUS: Order Stealing

        public const string CUSTOMER_REGISTER = "CUSTOMER_REGISTER";
        public const string CUSTOMER_ORDER = "CUSTOMER_ORDER";
        public const string CUSTOMER_STATUS_CHECK = "CUSTOMER_STATUS_CHECK";

        // Server to Client
        public const string ORDER_ASSIGNED = "ORDER_ASSIGNED";
        public const string ORDER_ACCEPTED = "ORDER_ACCEPTED";
        public const string ORDER_STATUS_UPDATE = "ORDER_STATUS_UPDATE";
        public const string LEADERBOARD_UPDATE = "LEADERBOARD_UPDATE"; // BONUS:  Leaderboard
        public const string TRAFFIC_JAM = "TRAFFIC_JAM"; // BONUS: Traffic Jam

        // General
        public const string SUCCESS = "SUCCESS";
        public const string ERROR = "ERROR";
        public const string PING = "PING";
        public const string PONG = "PONG";
    }

    // Utility Class
    public static class Utils
    {
        private static Random _random = new Random();

        // Calculate distance between two coordinates (km)
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth radius in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        // Calculate delivery time (seconds) based on distance
        public static int CalculateDeliveryTime(double distanceKm, bool hasTrafficJam = false)
        {
            // Base:  10 seconds per km (FAST for testing)
            int baseTime = (int)(distanceKm * 10);

            // Ensure minimum 5 seconds, maximum 30 seconds for demo
            if (baseTime < 5) baseTime = 5;
            if (baseTime > 30) baseTime = 30;

            // Random variation (±20%)
            int variation = _random.Next(-baseTime / 5, baseTime / 5);

            // BONUS: Traffic Jam adds 50% delay
            int trafficDelay = 0;
            if (hasTrafficJam)
            {
                trafficDelay = baseTime / 2; // +20% time
            }

            return baseTime + variation + trafficDelay;
        }

        // Calculate customer satisfaction (0-100)
        public static int CalculateSatisfaction(TimeSpan deliveryDuration)
        {
            // For demo purposes, using SECONDS instead of minutes
            double seconds = deliveryDuration.TotalSeconds;

            if (seconds <= 10) return 100;  // Very fast
            if (seconds <= 15) return 95;   // Fast
            if (seconds <= 20) return 90;   // Good
            if (seconds <= 25) return 85;   // Average
            if (seconds <= 30) return 75;   // Slow
            if (seconds <= 40) return 65;   // Very slow
            if (seconds <= 50) return 55;   // Poor
            return 50;                       // Unacceptable
        }

        // Random pizza generator
        public static List<string> GenerateRandomPizzas()
        {
            string[] pizzaTypes = {
                "Margherita", "Pepperoni", "Hawaiian", "Veggie Supreme",
                "BBQ Chicken", "Meat Lovers", "Four Cheese", "Mushroom Deluxe"
            };

            int count = _random.Next(1, 4); // 1-3 pizzas
            var pizzas = new List<string>();

            for (int i = 0; i < count; i++)
            {
                pizzas.Add(pizzaTypes[_random.Next(pizzaTypes.Length)]);
            }

            return pizzas;
        }

        // Random location generator (for simulation)
        public static (double lat, double lon) GenerateRandomLocation()
        {
            // Simulate a city area (40. 7128° N, 74.0060° W - New York-like)
            double baseLat = 40.7128;
            double baseLon = -74.0060;

            // Random offset within ~10km radius
            double latOffset = (_random.NextDouble() - 0.5) * 0.1;
            double lonOffset = (_random.NextDouble() - 0.5) * 0.1;

            return (baseLat + latOffset, baseLon + lonOffset);
        }

        // Check if traffic jam occurs (20% chance)
        public static bool ShouldTrafficJamOccur()
        {
            return _random.Next(0, 100) < 20; // 20% chance
        }
    }
}