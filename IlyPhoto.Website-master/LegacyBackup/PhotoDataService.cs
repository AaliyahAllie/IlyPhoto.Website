using IlyPhotoWeb.Models;

namespace IlyPhotoWeb.Services
{
    public class PhotoDataService
    {
        public List<ServicePackage> Services { get; private set; } = new List<ServicePackage>();
        public List<PhotoAlbum> Albums { get; private set; } = new List<PhotoAlbum>();
        public List<Booking> Bookings { get; private set; } = new List<Booking>();
        public List<Invoice> Invoices { get; private set; } = new List<Invoice>();
        public List<Review> Reviews { get; private set; } = new List<Review>();

        public PhotoDataService()
        {
            SeedData();
        }

        private void SeedData()
        {
            // Photography Services / Packages matching User Requirements
            Services = new List<ServicePackage>
            {
                new ServicePackage
                {
                    Id = 1,
                    Name = "Matric Ball Shoot",
                    Category = "Celebration",
                    Price = 800,
                    Duration = "2-3 Hours",
                    Description = "Elegant matric farewell portraits and pre-ball group photos.",
                    Features = new List<string> { "2-3 Hours Coverage", "Full High-Res Edited Gallery", "Outfit & Partner Portraits", "Private Client Album" }
                },
                new ServicePackage
                {
                    Id = 2,
                    Name = "Wedding Photography",
                    Category = "Wedding",
                    Price = 1500,
                    Duration = "Starts at 4 Hours",
                    Description = "Starting at R1,500 for 4 hours coverage. (+R200 per additional hour).",
                    Features = new List<string> { "4 Hours Initial Coverage", "R200 per extra hour", "Bridal Glow & Ceremony Shots", "High-Res Edited Deliverables", "Print Rights Included" }
                },
                new ServicePackage
                {
                    Id = 3,
                    Name = "Corporate Event",
                    Category = "Corporate",
                    Price = 2000,
                    Duration = "Starts at 4 Hours",
                    Description = "Starting at R2,000 for 4 hours coverage. (+R200 per additional hour).",
                    Features = new List<string> { "4 Hours Event Coverage", "R200 per extra hour", "Keynote Speeches & Gala Shots", "Dedicated Photographer", "Social Media Highlights" }
                },
                new ServicePackage
                {
                    Id = 4,
                    Name = "Other Events",
                    Category = "Custom Event",
                    Price = 300,
                    Duration = "Selectable Hours",
                    Description = "Flexible hourly rate of R300 per hour. Choose your exact event duration.",
                    Features = new List<string> { "R300 per hour rate", "Selectable Shoot Hours", "Family Reunions & Parties", "Online Deliverable Album" }
                }
            };

            // Initial Albums list starts empty as requested (No placeholders, uploaded by user/admin)
            Albums = new List<PhotoAlbum>();

            // Public Client Reviews
            Reviews = new List<Review>
            {
                new Review { Id = 1, ClientName = "Amazing work!", Author = "Elena Rostova", Service = "Wedding Photography", Rating = 5, Comment = "Ily Photo was punctual, professional, and delivered photos far beyond our expectations!", AdminResponse = "Thank you so much Elena!", Date = "2026-08-28" },
                new Review { Id = 2, ClientName = "Would hire again!", Author = "Michael Vance", Service = "Matric Ball Shoot", Rating = 5, Comment = "The matric farewell photos turned out stunning.", AdminResponse = "Appreciate the recommendation Michael!", Date = "2026-08-15" }
            };
        }

        // Availability checking FR08
        public bool IsDateAvailable(string eventDate, string eventTime)
        {
            return !Bookings.Any(b => b.EventDate == eventDate && b.EventTime == eventTime && b.Status != "Cancelled");
        }

        public Booking AddBooking(Booking booking)
        {
            booking.Id = $"BK-{1000 + Bookings.Count + 1}";
            booking.Status = "Pending";
            booking.CreatedAt = DateTime.Now.ToString("yyyy-MM-dd");
            Bookings.Insert(0, booking);
            return booking;
        }

        public void UpdateBookingStatus(string bookingId, string newStatus)
        {
            var b = Bookings.FirstOrDefault(x => x.Id == bookingId);
            if (b != null) b.Status = newStatus;
        }

        public void AddPhotoAlbum(PhotoAlbum album)
        {
            album.Id = Albums.Count + 1;
            Albums.Insert(0, album);
        }

        public void AddReview(Review review)
        {
            review.Id = Reviews.Count + 1;
            review.Date = DateTime.Now.ToString("yyyy-MM-dd");
            Reviews.Insert(0, review);
        }

        public void RespondToReview(int reviewId, string response)
        {
            var r = Reviews.FirstOrDefault(x => x.Id == reviewId);
            if (r != null) r.AdminResponse = response;
        }

        public Invoice GenerateInvoice(string bookingId, decimal amount, string dueDate)
        {
            var b = Bookings.FirstOrDefault(x => x.Id == bookingId);
            var inv = new Invoice
            {
                Id = $"INV-2026-00{Invoices.Count + 1}",
                BookingId = bookingId,
                ClientName = b?.ClientName ?? "Client",
                ClientEmail = b?.ClientEmail ?? "client@example.com",
                ServiceName = b?.ServiceName ?? "Photography Session",
                Amount = amount,
                Status = "Unpaid",
                IssuedDate = DateTime.Now.ToString("yyyy-MM-dd"),
                DueDate = dueDate
            };
            Invoices.Insert(0, inv);
            return inv;
        }

        public void UpdateInvoiceStatus(string invoiceId, string status)
        {
            var inv = Invoices.FirstOrDefault(x => x.Id == invoiceId);
            if (inv != null) inv.Status = status;
        }
    }
}
