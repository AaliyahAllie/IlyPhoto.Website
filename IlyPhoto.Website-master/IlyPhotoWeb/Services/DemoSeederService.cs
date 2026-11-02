using System.Text.Json;
using Azure.Data.Tables;
using BCrypt.Net;
using IlyPhotoWeb.Models;

namespace IlyPhotoWeb.Services;

/// <summary>
/// Pre-populates realistic, high-impact demonstration data for WIL (XADAD7112) examinations,
/// client presentations, and cloud staging.
/// </summary>
public class DemoSeederService
{
    private readonly AzureTableContext _context;
    private readonly UserStore _users;
    private readonly BookingStore _bookings;
    private readonly InvoiceStore _invoices;
    private readonly ReviewStore _reviews;
    private readonly ServiceStore _services;
    private readonly NotificationStore _notifications;
    private readonly ILogger<DemoSeederService> _logger;

    public DemoSeederService(
        AzureTableContext context,
        UserStore users,
        BookingStore bookings,
        InvoiceStore invoices,
        ReviewStore reviews,
        ServiceStore services,
        NotificationStore notifications,
        ILogger<DemoSeederService> logger)
    {
        _context = context;
        _users = users;
        _bookings = bookings;
        _invoices = invoices;
        _reviews = reviews;
        _services = services;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<int> SeedShowcaseAsync()
    {
        int itemsSeeded = 0;
        var today = Format.TodaySA();

        // 1. Ensure Standard Services
        await _services.ListAsync();

        // 2. Seed Admin & Client Users
        var adminUser = new UserProfile
        {
            Id = "admin-aysha-allie",
            Email = "admin@ilyphoto.co.za",
            FullName = "Aysha Allie",
            Role = Roles.Admin,
            ClientId = "ADMIN",
            Phone = "+27 82 123 4567",
            Address = "Strandfontein, Cape Town",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPass123!"),
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _users.SaveAsync(adminUser);
        itemsSeeded++;

        var clientAmina = new UserProfile
        {
            Id = "user-amina-davids",
            Email = "client@test.com",
            FullName = "Amina Davids",
            Role = Roles.Client,
            ClientId = "CL-1001",
            Phone = "+27 82 555 0199",
            Address = "Strandfontein Village, Cape Town",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ClientPass123!"),
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _users.SaveAsync(clientAmina);
        itemsSeeded++;

        var clientPastor = new UserProfile
        {
            Id = "user-pastor-isaacs",
            Email = "pastor.isaacs@mitchellsplainyouth.org.za",
            FullName = "Pastor Jerome Isaacs (Mitchells Plain Youth NPO)",
            Role = Roles.Client,
            ClientId = "CL-1002",
            Phone = "+27 83 444 8822",
            Address = "Tafelsig Community Center, Mitchells Plain",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ClientPass123!"),
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _users.SaveAsync(clientPastor);
        itemsSeeded++;

        var clientFatima = new UserProfile
        {
            Id = "user-fatima-hendricks",
            Email = "fatima.hendricks@gmail.com",
            FullName = "Fatima & Zayd Hendricks",
            Role = Roles.Client,
            ClientId = "CL-1003",
            Phone = "+27 71 222 3344",
            Address = "Muizenberg, Cape Town",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ClientPass123!"),
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _users.SaveAsync(clientFatima);
        itemsSeeded++;

        // 3. Seed Realistic Bookings
        var bNgo = new Booking
        {
            Id = "book-demo-ngo",
            ClientId = "CL-1002",
            Uid = clientPastor.Id,
            ClientName = clientPastor.FullName,
            ClientEmail = clientPastor.Email,
            ClientPhone = clientPastor.Phone,
            EventType = "Community / NGO & Youth Outreach",
            Date = Format.Iso(today.AddDays(5)),
            StartTime = "10:00",
            EndTime = "13:00",
            Hours = 3,
            Location = "Mitchells Plain Community Hall, Cape Flats",
            Notes = "Subsidized visual documentation for feeding scheme donor reporting and community impact presentation.",
            Status = BookingStatus.Confirmed,
            EstimatedTotal = 0,
            InvoiceNumber = "INV-00101",
            InvoiceStatus = InvoiceStatus.Paid,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _bookings.CreateAsync(bNgo);
        itemsSeeded++;

        var bWedding = new Booking
        {
            Id = "book-demo-wedding",
            ClientId = "CL-1003",
            Uid = clientFatima.Id,
            ClientName = clientFatima.FullName,
            ClientEmail = clientFatima.Email,
            ClientPhone = clientFatima.Phone,
            EventType = "Wedding",
            Date = Format.Iso(today.AddDays(12)),
            StartTime = "13:00",
            EndTime = "18:00",
            Hours = 5,
            Location = "Strandfontein Pavilion & Beachfront",
            Notes = "Full bridal preparations, beach sunset golden hour ceremony, and family reception.",
            Status = BookingStatus.Confirmed,
            EstimatedTotal = 3500,
            InvoiceNumber = "INV-00102",
            InvoiceStatus = InvoiceStatus.PartiallyPaid,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _bookings.CreateAsync(bWedding);
        itemsSeeded++;

        var bMatric = new Booking
        {
            Id = "book-demo-matric",
            ClientId = "CL-1001",
            Uid = clientAmina.Id,
            ClientName = clientAmina.FullName,
            ClientEmail = clientAmina.Email,
            ClientPhone = clientAmina.Phone,
            EventType = "Matric Ball",
            Date = Format.Iso(today.AddDays(18)),
            StartTime = "16:00",
            EndTime = "19:00",
            Hours = 3,
            Location = "Strandfontein High Hall, Cape Flats",
            Notes = "Matric farewell arrival, classic car departure, and formal studio portraits.",
            Status = BookingStatus.Pending,
            EstimatedTotal = 800,
            InvoiceNumber = "",
            InvoiceStatus = InvoiceStatus.Unpaid,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _bookings.CreateAsync(bMatric);
        itemsSeeded++;

        var bStudio = new Booking
        {
            Id = "book-demo-studio",
            ClientId = "CL-1001",
            Uid = clientAmina.Id,
            ClientName = clientAmina.FullName,
            ClientEmail = clientAmina.Email,
            ClientPhone = clientAmina.Phone,
            EventType = "Other Events / Birthday",
            Date = Format.Iso(today.AddDays(-14)),
            StartTime = "11:00",
            EndTime = "12:30",
            Hours = 2,
            Location = "ILY Photo Studio, Strandfontein",
            Notes = "Solo editorial fashion headshots and digital portfolio.",
            Status = BookingStatus.Completed,
            EstimatedTotal = 650,
            InvoiceNumber = "INV-00103",
            InvoiceStatus = InvoiceStatus.Paid,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _bookings.CreateAsync(bStudio);
        itemsSeeded++;

        // 4. Seed Linked Invoices
        var inv1 = new Invoice
        {
            Id = "inv-demo-00101",
            InvoiceNo = "INV-00101",
            ClientId = "CL-1002",
            BookingId = bNgo.Id,
            BookingLabel = bNgo.Label,
            IssueDate = Format.Iso(today.AddDays(-2)),
            DueDate = Format.Iso(today.AddDays(5)),
            Service = "Community / NGO & Youth Outreach (Feeding Scheme Donor Documentation - 100% Subsidized)",
            Quantity = 1,
            Price = 0,
            Total = 0,
            Status = InvoiceStatus.Paid,
            EmailSent = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _invoices.CreateAsync(inv1);
        itemsSeeded++;

        var inv2 = new Invoice
        {
            Id = "inv-demo-00102",
            InvoiceNo = "INV-00102",
            ClientId = "CL-1003",
            BookingId = bWedding.Id,
            BookingLabel = bWedding.Label,
            IssueDate = Format.Iso(today.AddDays(-3)),
            DueDate = Format.Iso(today.AddDays(12)),
            Service = "Wedding Photography Package (5h Strandfontein Pavilion Beachfront)",
            Quantity = 1,
            Price = 3500,
            Total = 3500,
            Status = InvoiceStatus.PartiallyPaid,
            EmailSent = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _invoices.CreateAsync(inv2);
        itemsSeeded++;

        var inv3 = new Invoice
        {
            Id = "inv-demo-00103",
            InvoiceNo = "INV-00103",
            ClientId = "CL-1001",
            BookingId = bStudio.Id,
            BookingLabel = bStudio.Label,
            IssueDate = Format.Iso(today.AddDays(-14)),
            DueDate = Format.Iso(today.AddDays(-10)),
            Service = "Solo Editorial Fashion Studio Session (High-Resolution Digital Retouching)",
            Quantity = 1,
            Price = 650,
            Total = 650,
            Status = InvoiceStatus.Paid,
            EmailSent = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        };
        await _invoices.CreateAsync(inv3);
        itemsSeeded++;

        // 5. Seed Authentic Community & Client Reviews
        await _reviews.CreateAsync(new Review
        {
            Id = "rev-demo-01",
            ClientId = "CL-1002",
            ClientName = "Pastor Jerome Isaacs (Mitchells Plain Youth NPO)",
            Rating = 5,
            Text = "Aysha Allie and ILY Photo provided incredible photography for our youth feeding scheme. The photos went straight into our annual report and helped us secure corporate sponsorship for 2027!",
            Approved = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });
        itemsSeeded++;

        await _reviews.CreateAsync(new Review
        {
            Id = "rev-demo-02",
            ClientId = "CL-1003",
            ClientName = "Fatima & Zayd Hendricks",
            Rating = 5,
            Text = "Our beachfront sunset wedding photos in Strandfontein are breathtaking! Aysha made us feel completely relaxed and captured genuine moments we'll cherish forever.",
            Approved = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });
        itemsSeeded++;

        await _reviews.CreateAsync(new Review
        {
            Id = "rev-demo-03",
            ClientId = "CL-1001",
            ClientName = "Amina Davids",
            Rating = 5,
            Text = "The studio portraits are high-fashion magazine standard! Fast turnaround on the private gallery and very reasonable rates for our community.",
            Approved = true,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });
        itemsSeeded++;

        // 6. Seed Sample Gallery in Azure Table
        var galEntity = new TableEntity("gallery", "gal-amina-01")
        {
            { "Id", "gal-amina-01" },
            { "ClientId", "CL-1001" },
            { "BookingId", bStudio.Id },
            { "Title", "Amina Davids — Studio Fashion Portraiture" },
            { "EventDate", bStudio.Date },
            { "PhotoPathsJson", JsonSerializer.Serialize(new List<string>
                {
                    "galleries/CL-1001/gal-amina-01/001-portrait-studio.jpg",
                    "galleries/CL-1001/gal-amina-01/002-editorial-look.jpg",
                    "galleries/CL-1001/gal-amina-01/003-creative-lighting.jpg"
                })
            },
            { "PortfolioApproved", true },
            { "CreatedAt", Timestamp.GetCurrentTimestamp().ToString() }
        };
        await _context.UpsertAsync("galleries", galEntity, TableUpdateMode.Replace);
        itemsSeeded++;

        // 7. Seed Notifications
        await _notifications.CreateAsync(new NotificationItem
        {
            Id = "notif-demo-01",
            ClientId = "CL-1001",
            Title = "Invoice INV-00103 Marked as Paid",
            Message = "Your payment of R650 for the Studio Session has been confirmed. Thank you!",
            Category = "Invoice",
            IsRead = false,
            CreatedAt = Timestamp.GetCurrentTimestamp()
        });
        itemsSeeded++;

        _logger.LogInformation("Successfully seeded {Count} showcase items into Azure database.", itemsSeeded);
        return itemsSeeded;
    }
}
