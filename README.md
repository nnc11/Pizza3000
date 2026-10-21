# 🍕 Pizza Delivery Simulator 3000: A Multi-Client Socket Programming Application Using TCP and UDP

A real-time pizza delivery management system demonstrating TCP/UDP socket programming with multiple concurrent clients.

---

## Overview

This project implements a **multi-client pizza delivery system** using C# socket programming with: 

- **Server (Restaurant HQ):** Manages orders, assigns deliveries, coordinates drivers
- **Driver Clients:** Accept orders, simulate deliveries, track performance  
- **Customer Clients:** Place orders, receive real-time status updates

**Key Technologies:**  
✅ TCP (TcpListener/TcpClient) - Reliable order communication  
✅ UDP (UdpClient) - Real-time driver location broadcasts  
✅ Async/await - Concurrent client handling  
✅ Thread-safe collections - ConcurrentDictionary, ConcurrentQueue  

**Bonus Features:**  
🏆 Traffic Jam Simulation - Random delivery delays  
🏆 Order Stealing - Drivers can steal orders if closer  
🏆 Rush Order Priority - Separate queue system  
🏆 Driver Leaderboard - Real-time performance ranking  

---

## Technical Requirements

### Frameworks & Libraries
- **. NET Framework 4.7.2+**
- **System.Net. Sockets** - TcpListener, TcpClient, UdpClient
- **System.Threading.Tasks** - Async/await pattern
- **System.Collections. Concurrent** - Thread-safe collections
- **System. Configuration** - App.config management

### Development Environment
- **Visual Studio 2019/2022**
- **C# 7.0+**
- **Windows OS**

### Network Configuration
- **TCP Port 5000** (configurable via App.config)
- **UDP Port 6000** (configurable via App. config)
- **Localhost** or LAN access

---

## How to Build the Project

### 1. Extract Files
```bash
unzip PizzaDeliverySystem.zip
cd PizzaDeliverySystem
```

### 2. Open in Visual Studio
- Double-click `PizzaDeliverySystem. sln`

### 3. Restore Dependencies
- Right-click Solution → **Restore NuGet Packages**

### 4. Build Solution
- **Build → Rebuild Solution** (Ctrl+Shift+B)
- Verify:  All projects build without errors

**Project Structure:**
```
PizzaDeliverySystem/
├── Server/              # TCP/UDP server
├── DriverClient/        # Driver application
├── CustomerClient/      # Customer application
├── Common/              # Shared models & protocol
└── README.md
```

---

## How to Run

### Step 1: Start the Server

**From Visual Studio:**
1. Right-click **Server** project → Set as Startup Project
2. Press **F5** or **Ctrl+F5**

**From Command Line:**
```bash
cd Server/bin/Debug
Server.exe
```

**Expected Output:**
```
╔════════════════════════════════════════════╗
║   🍕 PIZZA DELIVERY SERVER STARTED 🍕    ║
╚════════════════════════════════════════════╝
[TCP] Listening on port 5000
[UDP] Broadcasting on port 6000
```

⚠️ **Server must start FIRST**

---

### Step 2: Start Driver Clients

**From Visual Studio:**
1. Right-click **DriverClient** → Debug → Start New Instance
2. Enter driver name (e.g., "John")
3. **Repeat for 2-3 drivers**

**Menu:**
```
╔════════════ DRIVER MENU ════════════╗
║ 1. View Status                      ║
║ 2. Attempt Order Steal (BONUS)      ║
║ 3. View Stats                       ║
║ 4. Exit                             ║
╚═════════════════════════════════════╝
```

Drivers receive orders automatically in the background.

---

### Step 3: Start Customer Clients

**From Visual Studio:**
1. Right-click **CustomerClient** → Debug → Start New Instance
2. Enter customer name (e.g., "Alice")

**Menu:**
```
╔════════════ CUSTOMER MENU ════════════╗
║ 1. Order Pizza                        ║
║ 2. Order RUSH Pizza (Priority)        ║
║ 3. View My Orders                     ║
║ 4. Exit                               ║
╚═══════════════════════════════════════╝
```

---

## How to Test Main Features

### Test 1: Basic Order Flow
**Setup:** 1 Server + 1 Driver + 1 Customer  
**Steps:**
1. Customer → Menu Option 1 (Order Pizza)
2. Enter address:  `123 Main Street`
3. Wait ~15-30 seconds

**Verify:**
- ✅ Customer receives order confirmation
- ✅ Driver receives assignment with progress bar
- ✅ Customer receives status updates (Preparing → Delivered)
- ✅ Satisfaction score shown (50-100%)

---

### Test 2: Multiple Drivers
**Setup:** 1 Server + 3 Drivers + 1 Customer  
**Steps:**
1. Customer → Place 6 orders quickly
2. Observe server console

**Verify:**
- ✅ Orders distributed among drivers
- ✅ Nearest driver selected (server shows distances)
- ✅ All deliveries complete

**Server Output Example:**
```
[SEARCH] Finding best driver for order #1001: 
   - John: 4.52km
   - Sarah: 2.18km  ← Selected
   - Mike: 3.91km
```

---

### Test 3: Rush Order Priority (BONUS)
**Setup:** 1 Server + 3 Busy Drivers + 2 Customers  
**Steps:**
1. Make all drivers busy with orders
2. Customer A → Place normal order (queued)
3. Customer B → Place RUSH order (Menu Option 2)
4. Wait for driver to become available

**Verify:**
- ✅ Server shows `[PRIORITY ⚡] Processing rush order`
- ✅ Rush order assigned BEFORE normal order
- ✅ Driver sees `[⚡ RUSH ORDER]` tag

---

### Test 4: Traffic Jam (BONUS)
**Setup:** Place 10-20 orders  
**Verify:**
- ✅ ~20% show `[TRAFFIC JAM! ]` on server
- ✅ Driver shows ⚠️ warning and 🚦 on progress bar
- ✅ Delivery takes longer

---

### Test 5: Client Disconnection
**Setup:** 1 Server + 2 Drivers  
**Steps:**
1. Close one driver window
2. Place new order

**Verify:**
- ✅ Server shows `[DRIVER DISCONNECTED]`
- ✅ Server continues running
- ✅ Order goes to remaining driver

---

### Test 6: Multiple Customers (UDP)
**Setup:** 1 Server + 1 Driver + 2 Customers  
**Verify:**
- ✅ First customer:  `[UDP] Driver location tracking enabled ✓`
- ✅ Second customer: `[UDP] Location tracking unavailable`
- ✅ Both customers can order successfully (TCP fallback works)

---

## Known Issues & Limitations

1. **UDP Port Limitation**  
   - Only first customer gets UDP driver tracking
   - Others fallback to TCP (full functionality preserved)
   - Reason: UDP broadcast single-port architecture

2. **Localhost-Only Testing**  
   - Tested on 127.0.0.1
   - For LAN:  Edit App.config ServerIP setting

3. **No Data Persistence**  
   - All data in-memory (no database)
   - Orders/stats lost on server restart

4. **ETA Display Format**  
   - May show "0 minutes X seconds" for short distances
   - Actual timing works correctly

5. **Satisfaction Scores**  
   - Most deliveries score 95-100%
   - Reason: Fast simulation (5-30 seconds)

6. **Order Stealing Success Rate**  
   - Often fails due to random driver locations
   - Try multiple attempts to test success case (50% distance rule)

7. **Console Emoji Support**  
   - May show `?? ` on some Windows configurations
   - Visual only, functionality unaffected
---

### AI Assistance

**Tool:** GitHub Copilot Chat  

**Usage Examples:**

**1. Multi-Client Handling**  
- **Problem:** TcpListener blocking on single client
- **AI Suggestion:** Async accept loop with Task.Run()
- **Implementation:** `_ = Task.Run(() => HandleNewClient(client));`

**2. Priority Queue System**  
- **Problem:** Need rush order priority
- **AI Suggestion:** Two ConcurrentQueue<Order> with priority check
- **Implementation:** Separate `_rushOrderQueue` and `_normalOrderQueue`

**3. Network Exception Handling**  
- **Problem:** Crashes on client disconnect
- **AI Suggestion:** Try-catch with IOException
- **Implementation:** Graceful disconnect detection

**4. Console Formatting**  
- **AI Suggestion:** ANSI colors and box-drawing characters
- **Result:** Enhanced user experience

**5. Haversine Formula**  
- **AI Suggestion:** Geographic distance calculation
- **Implementation:** `CalculateDistance()` method

**6. Background Task Patterns**  
- **AI Suggestion:** Async loops with cancellation
- **Implementation:** `UdpBroadcastLoop()`, `LeaderboardUpdateLoop()`

---

## Academic Integrity

All code written by student with AI assistance as documented.  No unauthorized code copying.

**Project Statistics:**  
- Lines of Code: ~1500+
- Bonus Features: 4/4 (+20 points)
- Testing: 6+ comprehensive scenarios
- Build Status: ✅ All features working

---

**Version:** 1.0  
**Last Updated:** December 31, 2025
