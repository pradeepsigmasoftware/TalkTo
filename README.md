# TalkTo - Tawk.to-Style Live Chat Application

A modern, responsive, enterprise-grade web-based Live Chat Application inspired by **Tawk.to** and **WhatsApp**. Engineered with **ASP.NET Core 8 MVC**, **Microsoft SQL Server**, **ADO.NET** (no Entity Framework), and **SignalR** for real-time two-way communication.

---

## Architecture Overview

- **Presentation Layer**: ASP.NET Core 8 MVC with Razor Views, Bootstrap 5, Font Awesome, and clean Vanilla JavaScript / jQuery.
- **Real-Time Communication**: SignalR Hub (`/chatHub`) with auto-reconnection, presence sync, typing indicators, and message broadcasts.
- **Business Logic Layer (BLL)**:
  - `ChatAssignmentService`: Intelligent load-balanced agent assignment matching skills/services and concurrent limits.
  - `AuthService`: Secure password hashing with SHA-256 for Agent/Admin accounts.
  - `ExportService`: Generates Excel-compatible CSV and printable reports.
- **Data Access Layer (DAL)**: Pure **ADO.NET** (`Microsoft.Data.SqlClient`) calling 30 optimized SQL Server stored procedures with robust parameter and null handling.
- **Database Engine**: Microsoft SQL Server (`TawkToLiveChatDb`).

---

## Customer Chat Workflow (4-Step Sequential Sequence)

1. **Step 1: Floating Chat Widget**
   - Floating chat button at the bottom-right corner of the website with pulse animation and unread badge.
   - Popup window with company branding, header controls (minimize, maximize/expand, close).
   - Welcome message: *"Welcome! Thank you for contacting us. Please provide your details so our team can assist you."*

2. **Step 2: Mobile Number Capture (NO OTP)**
   - Country code dropdown with **+91 India** selected by default.
   - 10-digit Indian mobile number input with live JavaScript validation (`^[6-9]\d{9}$`).
   - Server-side validation and storage in `Chat_Visitors` table via ADO.NET.
   - **No OTP verification, no SMS, no customer password/registration required**.
   - Directly proceeds to Step 3 upon validation.

3. **Step 3: Service Selection**
   - Dropdown Label: **Select Your Service**.
   - Populated dynamically from SQL Server with 9 pre-configured services:
     1. Technical Support
     2. Website Development
     3. Software Development
     4. Payment Related Query
     5. Product Enquiry
     6. Sales & Marketing
     7. Account Related Query
     8. General Enquiry
     9. Other
   - Admins can add, edit, reorder, and toggle services in real time.

4. **Step 4: Initiate Live Chat Session**
   - Generates unique Chat Session ID (e.g. `CHAT-20261117-XXXX`).
   - Automatically queries online agents assigned to the selected service with lowest concurrent chat load.
   - If available: Sets session to `Active`, assigns agent, and triggers agent welcome greeting.
   - If unavailable: Maintains session in `Pending` queue with notice: *"Thank you for contacting us. Our support team will connect with you shortly."*
   - Real-time two-way messaging begins over SignalR.
   - Existing session restored from `localStorage` on page refresh or browser reload.

---

## Agent Dashboard

- **KPI Statistics**:
  - Total Assigned Chats
  - Active Chats
  - Pending Chats (with pulsing alert indicator)
  - Completed Chats
  - Unread Messages
- **Availability Toggle**:
  - `Online` (receives chats)
  - `Busy` (held from auto-assignment)
  - `Offline`
- **3-Column Workspace**:
  - **Left**: Active Chats list & Pending Queue with unread badges, service tags, last message snippet.
  - **Middle**: Full conversation stream, typing indicators, image preview, emoji picker, attachment uploading, message composer.
  - **Right**: Customer info card (Mobile number, country, service, session ID, time), Transfer Chat modal (to online agents), and Internal Notes tab.

---

## Admin Panel

- **Dashboard & Live Monitoring**:
  - Overall KPIs: Total Visitors, Total Chats, Active, Pending, Completed, Online/Offline Agents.
  - Doughnut chart for Service chat volume distribution.
  - Real-time Live Chat Monitor table with search by mobile, filters by service, status, date range, and transcript viewer.
- **Agent Management**:
  - Add / edit agents, set login credentials, role (Admin/Agent), max concurrent chats, activate/deactivate, and assign services via multi-select checkboxes.
- **Service Management**:
  - Add / edit services, set display order, and activate/deactivate.
- **Visitor Directory**:
  - View visitor mobile numbers, country codes, total sessions, first/last contact dates, and full session history.
- **Reports & Analytics**:
  - Daily Chat Report
  - Monthly Chat Report
  - Service-Wise Chat Report
  - Agent-Wise Chat Report
  - Pending Chat Report
  - Completed Chat Report
  - **1-Click Export to Excel (CSV)** and Print / PDF support.

---

## Pre-Configured Test Credentials

| Role | Email | Password | Assigned Skills / Services |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@tawkto.local` | `Admin@123` | Super Admin (All 9 Services) |
| **Agent (Rahul)** | `rahul.tech@tawkto.local` | `Agent@123` | Technical Support, Website Dev, Software Dev |
| **Agent (Priya)** | `priya.sales@tawkto.local` | `Agent@123` | Payment Query, Product Enquiry, Sales & Marketing |
| **Agent (Amit)** | `amit.support@tawkto.local` | `Agent@123` | Account Query, General Enquiry, Other |

*Note: The Login screen includes 1-click quick-fill buttons for each test user.*

---

## Database Setup & Execution

The SQL script `Database/CreateDatabaseAndObjects.sql` contains the complete schema, tables, foreign keys, indexes, seed data, and 30 stored procedures:

```powershell
sqlcmd -S localhost -E -C -i "d:\_AntiGravity\TalkTo\Database\CreateDatabaseAndObjects.sql"
```

To run the web application:
```powershell
cd "d:\_AntiGravity\TalkTo\TalkToLiveChat"
dotnet run --launch-profile http
```
The application will listen on `http://localhost:5010/`.
