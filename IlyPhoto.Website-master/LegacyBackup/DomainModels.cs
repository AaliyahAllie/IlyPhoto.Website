namespace IlyPhotoWeb.Models
{
    public class Booking
    {
        public string Id { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string EventDate { get; set; } = string.Empty; // Format: YYYY-MM-DD
        public string EventTime { get; set; } = string.Empty; // Format: HH:mm
        public string Location { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // "Pending", "Confirmed", "Completed", "Cancelled", "Rescheduled"
        public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }

    public class PhotoAlbum
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string Category { get; set; } = "Wedding";
        public bool IsPublic { get; set; } = false;
        public bool Featured { get; set; } = false;
        public List<string> ImageUrls { get; set; } = new List<string>();
        public string CoverImageUrl => ImageUrls.FirstOrDefault() ?? "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&q=80&w=800";
        public string UploadDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }

    public class ServicePackage
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Duration { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Features { get; set; } = new List<string>();
    }

    public class Invoice
    {
        public string Id { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Unpaid"; // "Unpaid", "Partially Paid", "Paid", "Overdue"
        public string IssuedDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public string DueDate { get; set; } = DateTime.Now.AddDays(14).ToString("yyyy-MM-dd");
        
        // EFT & Email Dispatch fields
        public bool EmailSent { get; set; } = true;
        public string BankName { get; set; } = "Standard Bank South Africa";
        public string AccountName { get; set; } = "Ily Photo Studio (Pty) Ltd";
        public string AccountNumber { get; set; } = "10982736451";
        public string BranchCode { get; set; } = "051001";
        public string PaymentReference => Id;
    }

    public class Review
    {
        public int Id { get; set; }
        public string ClientName { get; set; } = string.Empty; // Review Headline
        public string Author { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
        public string? AdminResponse { get; set; }
        public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }
}
