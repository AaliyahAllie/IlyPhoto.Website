import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { Navbar } from './components/common/Navbar';
import { Footer } from './components/common/Footer';

// Public Pages
import { HomePage } from './pages/public/HomePage';
import { PortfolioPage } from './pages/public/PortfolioPage';
import { PricingPage } from './pages/public/PricingPage';
import { ContactPage } from './pages/public/ContactPage';

// Client Portal Pages
import { ClientLoginPage } from './pages/client/ClientLoginPage';
import { ClientRegisterPage } from './pages/client/ClientRegisterPage';
import { PasswordResetPage } from './pages/client/PasswordResetPage';
import { ClientDashboardPage } from './pages/client/ClientDashboardPage';
import { ClientBookingPage } from './pages/client/ClientBookingPage';
import { ClientGalleryPage } from './pages/client/ClientGalleryPage';
import { ClientReviewsPage } from './pages/client/ClientReviewsPage';
import { ClientInvoicePage } from './pages/client/ClientInvoicePage';

// Admin Dashboard Pages
import { AdminLoginPage } from './pages/admin/AdminLoginPage';
import { AdminDashboardPage } from './pages/admin/AdminDashboardPage';
import { AdminBookingsPage } from './pages/admin/AdminBookingsPage';
import { AdminGalleryPage } from './pages/admin/AdminGalleryPage';
import { AdminReviewsPage } from './pages/admin/AdminReviewsPage';
import { AdminServicesPage } from './pages/admin/AdminServicesPage';
import { AdminInvoicePage } from './pages/admin/AdminInvoicePage';

import './assets/theme.css';

function App() {
  return (
    <AuthProvider>
      <Router>
        <div style={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
          <Navbar />
          <main style={{ flex: 1 }}>
            <Routes>
              {/* Public Routes */}
              <Route path="/" element={<HomePage />} />
              <Route path="/portfolio" element={<PortfolioPage />} />
              <Route path="/pricing" element={<PricingPage />} />
              <Route path="/contact" element={<ContactPage />} />

              {/* Client Portal Routes */}
              <Route path="/client/login" element={<ClientLoginPage />} />
              <Route path="/client/register" element={<ClientRegisterPage />} />
              <Route path="/client/reset-password" element={<PasswordResetPage />} />
              <Route path="/client/dashboard" element={<ClientDashboardPage />} />
              <Route path="/client/booking" element={<ClientBookingPage />} />
              <Route path="/client/gallery" element={<ClientGalleryPage />} />
              <Route path="/client/reviews" element={<ClientReviewsPage />} />
              <Route path="/client/invoices" element={<ClientInvoicePage />} />

              {/* Admin Dashboard Routes */}
              <Route path="/admin/login" element={<AdminLoginPage />} />
              <Route path="/admin/dashboard" element={<AdminDashboardPage />} />
              <Route path="/admin/bookings" element={<AdminBookingsPage />} />
              <Route path="/admin/gallery" element={<AdminGalleryPage />} />
              <Route path="/admin/reviews" element={<AdminReviewsPage />} />
              <Route path="/admin/services" element={<AdminServicesPage />} />
              <Route path="/admin/invoices" element={<AdminInvoicePage />} />
            </Routes>
          </main>
          <Footer />
        </div>
      </Router>
    </AuthProvider>
  );
}

export default App;
