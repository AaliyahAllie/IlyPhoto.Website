// Mock seed data for Ily Photo Website
// Ready to be replaced by C# ASP.NET Core Web API endpoints

export const initialServices = [
  {
    id: 1,
    name: "Matric Ball Shoot",
    category: "Celebration",
    price: 800,
    duration: "2-3 Hours",
    description: "Elegant matric farewell portraits and pre-ball group photos.",
    features: ["2-3 Hours Coverage", "Full High-Res Edited Gallery", "Outfit & Partner Portraits", "Private Client Album"]
  },
  {
    id: 2,
    name: "Wedding Photography",
    category: "Wedding",
    price: 1500,
    duration: "Starts at 4 Hours",
    description: "Starting at R1,500 for 4 hours coverage. (+R200 per additional hour).",
    features: ["4 Hours Initial Coverage", "R200 per extra hour", "Bridal Glow & Ceremony Shots", "High-Res Edited Deliverables", "Print Rights Included"]
  },
  {
    id: 3,
    name: "Corporate Event",
    category: "Corporate",
    price: 2000,
    duration: "Starts at 4 Hours",
    description: "Starting at R2,000 for 4 hours coverage. (+R200 per additional hour).",
    features: ["4 Hours Event Coverage", "R200 per extra hour", "Keynote Speeches & Gala Shots", "Dedicated Photographer", "Social Media Highlights"]
  },
  {
    id: 4,
    name: "Other Events",
    category: "Custom Event",
    price: 300,
    duration: "Selectable Hours",
    description: "Flexible hourly rate of R300 per hour. Choose your exact event duration.",
    features: ["R300 per hour rate", "Selectable Shoot Hours", "Family Reunions & Parties", "Online Deliverable Album"]
  }
];

export const initialPortfolio = [
  {
    id: 1,
    title: "Golden Hour Couple",
    category: "Wedding",
    imageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&q=80&w=800",
    isPublic: true,
    featured: true
  },
  {
    id: 2,
    title: "Minimalist Studio Portrait",
    category: "Work & Art",
    imageUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&q=80&w=800",
    isPublic: true,
    featured: true
  }
];

export const initialClientGalleries = [
  {
    id: 101,
    bookingId: "BK-1001",
    clientEmail: "sarah.j@example.com",
    title: "Pre-Wedding Shoot Sunset",
    imageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&q=80&w=800",
    uploadDate: "2026-09-11"
  }
];

export const initialBookings = [
  {
    id: "BK-1001",
    clientName: "Sarah Jenkins",
    clientEmail: "sarah.j@example.com",
    serviceName: "Wedding Photography",
    eventDate: "2026-10-15",
    eventTime: "14:00",
    location: "Botanical Gardens, Sunset View",
    requirements: "Golden hour lighting preferred. Focus on natural unposed candid shots.",
    status: "Pending",
    createdAt: "2026-09-10"
  }
];

export const initialInvoices = [
  {
    id: "INV-2026-001",
    bookingId: "BK-1001",
    clientName: "Sarah Jenkins",
    clientEmail: "sarah.j@example.com",
    serviceName: "Wedding Photography (4 Hours)",
    amount: 1500,
    status: "Unpaid",
    issuedDate: "2026-09-10",
    dueDate: "2026-09-24"
  }
];

export const initialReviews = [
  {
    id: 1,
    clientName: "Amazing work!",
    author: "Elena Rostova",
    service: "Wedding Photography",
    rating: 5,
    comment: "Ily Photo was punctual, professional, and delivered photos far beyond our expectations!",
    adminResponse: "Thank you so much Elena!",
    date: "2026-08-28"
  }
];
