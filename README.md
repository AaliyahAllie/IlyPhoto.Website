# ILY Photo — Photography Studio Web Platform

An ASP.NET Core MVC (.NET 9) web application built for **ILY Photo Studio (Cape Town, South Africa)**. The platform connects prospective clients, registered clients, and the studio photographer/administrator with real-time booking management, interactive availability calendars, automated EFT invoicing, private cloud photo galleries, and community NPO outreach support.

---

## Features

### Public Website
- **Home & Portfolio Showcase:** Displays studio highlights and client-approved portfolio captures served from Azure Blob Storage with a full-screen lightbox viewer.
- **Packages & Pricing:** Dynamic service packages (`Matric Ball`, `Wedding`, `Corporate Event`, `Other Events`) backed by Azure Table Storage, complete with an interactive instant quote estimator.
- **About & Community Outreach:** Highlights studio background, subsidized Cape Flats NPO coverage, pro-bono Matric Ball sponsorships, and POPIA privacy compliance.
- **Contact & Enquiries:** Persists client enquiries directly into Azure Table Storage (`enquiries` table).
- **Responsive Design, Dark Mode & Language Switcher:** Responsive layouts for desktop, tablet, and mobile devices, persistent Light/Dark mode toggle, and English / Afrikaans (`EN` / `AF`) interface translation support.

### Client Portal
- **Strict Per-Client Isolation:** Every registered client receives a unique sequential `ClientId` (`CL-XXXX`). Bookings, invoices, reviews, and private photo galleries are strictly scoped to the signed-in client's `ClientId`.
- **Interactive Booking & Availability Calendar:** Multi-month calendar displaying available, busy, blocked, and personal booking dates via `/api/calendar`, with automatic conflict prevention and live duration/price calculation.
- **Payment-Gated Private Galleries:** Clients can view full-resolution photo albums and download complete `.zip` archives only after their associated booking invoice has been marked `Paid` (or if the session is a subsidized/pro-bono R0 booking). Unpaid albums display a clear payment-pending status with direct access to EFT invoice details.
- **Invoices & EFT Statements:** Printable tax invoices and Standard Bank EFT payment instructions with unique invoice references (`INV-XXXXX`).
- **Client Reviews & Profile Management:** Clients can submit star-rated reviews and update their contact details.

### Photographer / Admin Portal
- **Studio Overview Dashboard:** Live summary of pending requests, upcoming confirmed shoots, outstanding EFT totals, and registered client count.
- **Master Calendar & Booking Workflow:** Approve, deny, or reschedule client bookings, and click calendar dates to block or unblock studio availability. Approving a booking automatically generates and links a tax invoice.
- **Multi-File Gallery Uploads:** Multi-select image uploader (JPG, PNG, WEBP, GIF up to 50 MB per image) storing photos in Azure Blob Storage (`ilyphoto-galleries`) with optional public portfolio consent flag.
- **Invoice Management:** Generate invoices linked to confirmed bookings, preview printable statements, dispatch HTML EFT email notices, and update payment status (`Unpaid`, `Partially Paid`, `Paid`, `Overdue`) with automatic synchronization to the linked booking.
- **Packages & Pricing Administration:** Create and edit photography packages, base prices, included hours, and extra hourly rates.
- **System Diagnostics (`/diagnostics`):** Live health checks for Azure Table Storage, Azure Blob Storage, and configuration status.

### Security & Authentication
- **BCrypt Password Hashing:** All user passwords are salted and hashed with `BCrypt.Net-Next`.
- **Password Complexity Enforcement:** Registration and password resets require at least 8 characters including an uppercase letter, a lowercase letter, a number, and a special character.
- **Secure Cookie Authentication:** `HttpOnly`, `SameSite=Lax`, sliding 8-hour sessions with role-based access control (`Admin` and `Client`) and global antiforgery token validation (`AutoValidateAntiforgeryTokenAttribute`).
- **Hardened Login:** No hardcoded or pre-filled administrator credentials on the login page.

---

## Tech Stack

- **Framework:** ASP.NET Core MVC (.NET 9.0)
- **Database:** Azure Table Storage (`Azure.Data.Tables`)
- **Media Storage:** Azure Blob Storage (`Azure.Storage.Blobs`) with local disk fallback (`App_Data/photos`)
- **Authentication:** ASP.NET Core Cookie Authentication + `BCrypt.Net-Next`
- **Frontend:** Razor Views, CSS3 (custom responsive grid & Light/Dark themes), Vanilla JavaScript
- **Testing:** xUnit, Moq, Microsoft.NET.Test.Sdk (`IlyPhotoWeb.Tests`)

---

## Repository Structure

```text
Ily Photo Website/
├── IlyPhoto.sln                # Visual Studio solution file
├── IlyPhotoWeb/                # Main ASP.NET Core MVC application
│   ├── Controllers/            # Home, Account, Client, Admin, Calendar, Media, Api, Diagnostics
│   ├── Models/                 # Entities, ViewModels, Formatting & Date Normalization helpers
│   ├── Services/               # AzureTableContext, AuthService, Stores, GalleryStore, Blob/Local Storage, EmailService
│   ├── Views/                  # Razor views (Home, Account, Client, Admin, Diagnostics, Shared)
│   ├── wwwroot/                # CSS, JavaScript, and static image assets
│   └── Program.cs              # Application startup and DI configuration
├── IlyPhotoWeb.Tests/          # Automated unit test suite (27 tests)
│   ├── AuthServiceTests.cs
│   ├── BookingSchedulingTests.cs
│   └── ApiControllerTests.cs
└── .gitignore                  # Excludes secrets (appsettings.json), build outputs, and backups
```

---

## Azure Table Storage Schema

The application connects to the following tables in Azure Table Storage (auto-created on startup if absent):

| Table | Purpose |
| :--- | :--- |
| `users` | Client and administrator profiles, unique `ClientId` (`CL-XXXX`), and BCrypt password hashes |
| `bookings` | Client booking requests, normalized ISO event dates (`yyyy-MM-dd`), times, locations, and status |
| `blockedDates` | Photographer-blocked unavailable dates |
| `invoices` | Generated tax invoices (`INV-XXXXX`), linked booking IDs, totals, and EFT payment status |
| `galleries` | Client photo album metadata and Azure Blob Storage image paths |
| `services` | Photography packages and pricing formulas |
| `reviews` | Approved client testimonials and ratings |
| `enquiries` | Public contact form submissions |
| `counters` | Sequential ID trackers for unique `CL-XXXX` client IDs and `INV-XXXXX` invoice numbers |

---

## Local Setup & Configuration

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 (or VS Code / Rider)
- An Azure Storage Account (Table Storage + Blob Storage)

### 1. Clone the Repository
```bash
git clone https://github.com/AaliyahAllie/IlyPhoto.Website.git
cd IlyPhoto.Website
```

### 2. Configure `appsettings.json`
`IlyPhotoWeb/appsettings.json` is excluded via `.gitignore` so connection strings and keys are never committed to source control. Create `IlyPhotoWeb/appsettings.json` locally using the template below:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=YOUR_STORAGE_ACCOUNT;AccountKey=YOUR_ACCOUNT_KEY;EndpointSuffix=core.windows.net",
    "ContainerName": "ilyphoto-galleries"
  },
  "PhotoStorage": {
    "LocalPath": "App_Data/photos",
    "SigningKey": "replace-with-a-strong-random-signing-key"
  },
  "Admin": {
    "Emails": [
      "admin@ilyphoto.co.za"
    ]
  },
  "Business": {
    "Name": "ILY Photo",
    "Email": "info@ilyphoto.co.za",
    "Phone": "+27 82 123 4567",
    "BankDetails": "Bank: Standard Bank\nAccount Holder: Ily Photo Studio\nAccount Number: 0000000000\nAccount Type: Cheque / Current\nBranch Code: 051001\nReference: Use your Invoice Number (e.g. INV-00001)"
  }
}
```

### 3. Run the Application
Using the CLI:
```bash
dotnet run --project IlyPhotoWeb/IlyPhotoWeb.csproj
```
Or open `IlyPhoto.sln` in Visual Studio and press **F5**.

---

## Running Unit Tests

Run the full xUnit test suite (`AuthServiceTests`, `BookingSchedulingTests`, `ApiControllerTests`):

```bash
dotnet test IlyPhotoWeb.Tests/IlyPhotoWeb.Tests.csproj
```

---

## Privacy & Compliance
- Client data is isolated by unique `ClientId` and protected with role-based authorization checks on all portal routes and media downloads.
- Designed with South African **POPIA** principles in mind, requiring explicit consent before any private client gallery photos appear on the public portfolio.
