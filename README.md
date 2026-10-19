# ILY Photo — Unified Cross-Platform Desktop Web, REST API & Mobile Ecosystem

**Student:** ST10212542  
**Organisation:** ILY Photo Studio (Aysha Allie — Strandfontein, Cape Town, South Africa)  
**Cloud Infrastructure:** Microsoft Azure App Service (`ilyphoto-api-st10212542.azurewebsites.net`), Azure Table Storage (`ilyphotostorage`), and Azure Blob Storage (`ilyphoto-galleries`)

---

## 1. Project Overview & NGO / Community Mission

**ILY Photo** is a Cape Town photography studio and community media partner founded by solo photographer **Aysha Allie** in Strandfontein. Beyond commercial photography (Weddings, Matric Balls, Corporate Events, and Custom Celebrations), ILY Photo operates a dedicated **Community & NGO Impact Programme** across Strandfontein, Mitchells Plain, and the wider Cape Flats:

1. **Subsidized NGO & NPO Donor Storytelling:** Registered charities, feeding schemes, and non-profit organisations receive subsidized photography coverage (`Community / NGO & Youth Outreach` package) to produce high-resolution visual documentation for annual reports, grant applications, and donor accountability.
2. **Youth Visual-Literacy Mentorship:** Practical camera, lighting, and digital editing workshops for aspiring high-school learners in under-resourced Cape Flats schools.
3. **Accessible School & Matric Ball Coverage:** Subsidized school packages (`R800` for 2–3 hours) so every learner can celebrate their graduation and matric ball with studio-grade portraits.

---

## 2. Solution Architecture

The solution (`IlyPhoto.sln`) unifies the Desktop Web Portal, Cross-Platform REST API, React SPA Frontend, and Automated xUnit Test Suite:

| Project / Directory | Role & Responsibilities |
| :--- | :--- |
| **`IlyPhotoWeb/`** | Primary ASP.NET Core MVC + REST API server (`Program.cs`, `Controllers/`, `Services/`, `Models/`, `Views/`). Serves both the Razor Desktop Web Portal (Public, Client Portal, Admin Dashboard, Diagnostics) and the unified `/api/*` REST endpoints consumed by the Android mobile app (`AzureApiClient.kt`) and React SPA. |
| **`IlyPhotoWeb.Tests/`** | Automated xUnit test suite (`ApplicationWorkflowTests.cs`) testing authentication security, password recovery validation, date/time conflict detection, full-field booking rescheduling persistence, auto-invoice generation on booking confirmation, and NGO service defaults. |
| **`frontend/`** | React + Vite SPA (`src/services/azureApi.js`, `src/context/AuthContext.jsx`, `src/pages/client/*`, `src/pages/admin/*`) synchronized with `/api/*` endpoints. |
| **`backend/IlyPhoto.Api/`** | Standalone Minimal API service (`Program.cs`) implementing booking creation, conflict detection, full-field rescheduling, and automated invoicing. |

---

## 3. Cloud Database Architecture: Azure Table Storage Keys, Client Links & Integrity Rules

All structured application data is stored in **Microsoft Azure Table Storage** (`ilyphotostorage`), while high-resolution client albums are stored in **Azure Blob Storage** (`ilyphoto-galleries`) and served via **HMAC-SHA256 time-limited signed URLs**.

### 3.1 Table Schema, PartitionKey, RowKey & Foreign Key Links

| Azure Table | `PartitionKey` | `RowKey` | Foreign Key / Client Link | Key Properties & Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **`users`** | `"user"` | `Id` (`user-...` / `admin-...`) | `ClientId` (`CL-1001` or `ADMIN`) | `FullName`, `Email` (normalized lowercase), `Phone`, `Role` (`Client` / `Admin`), `PasswordHash` (BCrypt), `CreatedAt`. |
| **`bookings`** | `"booking"` | `Id` (`book-...`) | `ClientId` (`CL-1001`), `Uid` (`users.RowKey`), `InvoiceNumber` (`INV-00001`) | `EventType`/`serviceName`, `Date`/`eventDate` (`yyyy-MM-dd`), `StartTime`/`eventTime`, `EndTime`, `Hours`, `Location`/`eventLocation`, `Notes`/`requirements`, `Status`, `EstimatedTotal`/`servicePrice`, `InvoiceStatus`. |
| **`invoices`** | `"invoice"` | `Id` (`inv-...`) | `ClientId` (`CL-1001`), `BookingId` (`bookings.RowKey`) | `InvoiceNo` (`INV-00001`), `BookingLabel`, `IssueDate`, `DueDate`, `Service`, `Quantity`, `Price`, `Total`, `Status` (`Unpaid`, `Partially Paid`, `Paid`, `Overdue`), `EmailSent`. |
| **`galleries`** | `"gallery"` | `Id` (`gal-...`) | `ClientId` (`CL-1001`), `BookingId` (`bookings.RowKey`) | `Title`, `EventDate`, `PhotoPathsJson` (Blob paths under `galleries/{ClientId}/{GalleryId}/`), `PortfolioApproved`. |
| **`services`** | `"service"` | `Id` (`matric-ball`, `wedding`, `ngo-community`, etc.) | Referenced by `bookings.EventType` & `invoices.Service` | `Name`, `Description`, `BasePrice`, `IncludedHours`, `ExtraHourPrice`, `HourlyOnly`, `SortOrder`. |
| **`blockedDates`** | `"blockedDate"` | `Date` (`yyyy-MM-dd`) | Checked against `bookings.Date` | `Date`, `Reason`, `CreatedAt`. Prevents client bookings on blocked dates. |
| **`reviews`** | `"review"` | `Id` (`rev-...`) | `ClientId` (`CL-1001`) | `ClientName`, `Rating` (`1..5`), `Text`, `Approved`, `CreatedAt`. |
| **`enquiries`** | `"enquiry"` | `Id` (`enq-...`) | Public visitor email | `FullName`, `Email`, `Phone`, `Message`, `Status`, `CreatedAt`. |
| **`notifications`** | `"notification"` | `Id` (`notif-...`) | `ClientId` (`CL-1001`), `RecipientEmail`, `RelatedId` | `Title`, `Message`, `Category` (`Booking`, `Invoice`, `Gallery`, `Security`), `IsRead`, `CreatedAt`. |
| **`counters`** | `"counter"` | Sequence name (`"clients"`, `"invoices"`) | Generates `CL-1001` & `INV-00001` | Atomic `Value` counter for sequential human-readable IDs. |

### 3.2 Data Integrity & Security Rules
1. **Strict Credential Verification:** Passwords are hashed using `BCrypt.Net.BCrypt.HashPassword`. Existing accounts strictly verify against `BCrypt.Verify` and never fall back to a cached profile or default password on failed login.
2. **Validated Password Recovery:** Both `AccountController.ForgotPassword` and `POST /api/auth/reset-password` verify that the submitted email is valid and registered in the `users` table before updating the BCrypt hash and dispatching a `Security` notification.
3. **Server-Side Availability & Conflict Prevention:** Creating or rescheduling a booking checks `BookingStore.HasConflictOnDateAsync(dateIso, exceptBookingId)` against both `blockedDates` and existing `Confirmed`/`Rescheduled` bookings, returning `409 Conflict` (API) or a validation error (MVC) to prevent double-booking.
4. **Full-Field Rescheduling Persistence:** Rescheduling via the Client Portal (`ClientController.EditBooking`), Admin Portal (`AdminController.RescheduleBooking`), or REST API (`PUT /api/bookings/{id}` and `PATCH /api/bookings/{id}/status`) persists all amended fields: `Date`, `StartTime`, `EndTime`, `Hours`, `Location` (venue), `EventType` (package), `EstimatedTotal`, `Notes`, and `Status`.
5. **Automated Invoicing & Reliable Notifications:** Approving a booking (`ApproveBooking` or `PUT/PATCH /api/bookings/{id}`) automatically generates a linked `Invoice` (`INV-xxxxx`), updates the booking's `InvoiceNumber`, archives HTML emails under `App_Data/sent_emails/`, and persists a `NotificationItem` to the `notifications` Azure Table.

---

## 4. Running on Google Antigravity IDE

This repository is fully configured for out-of-the-box development, debugging, and testing in **Google Antigravity IDE**.

### 4.1 Quick Start (One-Click / Terminal)

1. **Option A: Antigravity IDE Run & Debug (F5)**
   - Open the **Run and Debug** view (`Ctrl+Shift+D` / `Cmd+Shift+D`).
   - Select **"Launch IlyPhotoWeb (MVC + API)"** and press **F5** (or click the green Play icon).
   - Antigravity IDE will automatically compile the solution and open the web portal at `http://localhost:5239`.

2. **Option B: Antigravity Integrated Terminal**
   - Open an integrated PowerShell terminal in Antigravity IDE (`Ctrl+` `~`).
   - Run:
     ```powershell
     .\run.ps1
     ```
   - This checks for the .NET 9 SDK, sets the environment paths, displays startup banners, and launches the ASP.NET Core server.

3. **Option C: Running Unit Tests**
   - Press `Ctrl+Shift+P` -> **Tasks: Run Task** -> **test**
   - Or in the integrated terminal, run:
     ```powershell
     .\test.ps1
     ```

### 4.2 Local Endpoints & Ports

| Endpoint | URL | Description |
| :--- | :--- | :--- |
| **Main Web Portal** | `http://localhost:5239` | Public landing page, galleries, bookings, contact form |
| **Diagnostics & Health** | `http://localhost:5239/Diagnostics` | Visual database, storage, and health telemetry |
| **REST API Health** | `http://localhost:5239/api/health` | JSON health endpoint verified by mobile & frontend clients |
| **API Swagger / OpenApi** | `http://localhost:5233/openapi/v1.json` | Minimal API microservice OpenAPI specification |

### 4.3 Default Test Credentials

| Role | Email | Password | Access Level |
| :--- | :--- | :--- | :--- |
| **Admin / Photographer** | `admin@ilyphoto.co.za` | `AdminPass123!` | Admin portal, bookings management, invoice generation, diagnostics |
| **Client** | `client@test.com` | `ClientPass123!` | Client portal, booking history, rescheduling, invoices, notifications |
