import React, { createContext, useContext, useState, useEffect } from 'react';
import { initialBookings, initialPortfolio, initialServices, initialInvoices, initialReviews, initialClientGalleries } from '../services/mockData';
import { AzureApi } from '../services/azureApi';

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  // Auth state: 'visitor' | 'client' | 'admin'
  const [role, setRole] = useState(() => localStorage.getItem('ily_role') || 'visitor');
  const [currentUser, setCurrentUser] = useState(() => {
    const saved = localStorage.getItem('ily_user');
    return saved ? JSON.parse(saved) : { name: 'Sarah Jenkins', email: 'sarah.j@example.com' };
  });

  // Business Data States (with localStorage persistence)
  const [bookings, setBookings] = useState(() => {
    const saved = localStorage.getItem('ily_bookings');
    return saved ? JSON.parse(saved) : initialBookings;
  });

  const [portfolio, setPortfolio] = useState(() => {
    const saved = localStorage.getItem('ily_portfolio');
    return saved ? JSON.parse(saved) : initialPortfolio;
  });

  const [clientGalleries, setClientGalleries] = useState(() => {
    const saved = localStorage.getItem('ily_client_galleries');
    return saved ? JSON.parse(saved) : initialClientGalleries;
  });

  const [services, setServices] = useState(() => {
    const saved = localStorage.getItem('ily_services');
    return saved ? JSON.parse(saved) : initialServices;
  });

  const [invoices, setInvoices] = useState(() => {
    const saved = localStorage.getItem('ily_invoices');
    return saved ? JSON.parse(saved) : initialInvoices;
  });

  const [reviews, setReviews] = useState(() => {
    const saved = localStorage.getItem('ily_reviews');
    return saved ? JSON.parse(saved) : initialReviews;
  });

  useEffect(() => { localStorage.setItem('ily_role', role); }, [role]);
  useEffect(() => { localStorage.setItem('ily_user', JSON.stringify(currentUser)); }, [currentUser]);
  useEffect(() => { localStorage.setItem('ily_bookings', JSON.stringify(bookings)); }, [bookings]);
  useEffect(() => { localStorage.setItem('ily_portfolio', JSON.stringify(portfolio)); }, [portfolio]);
  useEffect(() => { localStorage.setItem('ily_client_galleries', JSON.stringify(clientGalleries)); }, [clientGalleries]);
  useEffect(() => { localStorage.setItem('ily_services', JSON.stringify(services)); }, [services]);
  useEffect(() => { localStorage.setItem('ily_invoices', JSON.stringify(invoices)); }, [invoices]);
  useEffect(() => { localStorage.setItem('ily_reviews', JSON.stringify(reviews)); }, [reviews]);

  // Auth Operations
  const switchRole = (newRole) => {
    setRole(newRole);
    if (newRole === 'admin') {
      setCurrentUser({ name: 'Ily Photographer (Admin)', email: 'admin@ilyphoto.co.za' });
    } else if (newRole === 'client') {
      setCurrentUser({ name: 'Sarah Jenkins', email: 'sarah.j@example.com' });
    } else {
      setCurrentUser(null);
    }
  };

  const loginAsClient = async (email, password) => {
    try {
      const res = await AzureApi.login(email, password);
      if (res && res.token) {
        localStorage.setItem('ily_azure_token', res.token);
        setRole(res.user?.role === 'photographer' || res.user?.role === 'Admin' ? 'admin' : 'client');
        setCurrentUser({
          id: res.user?.id,
          name: res.user?.displayName || res.user?.username || email.split('@')[0],
          email: res.user?.email || email
        });
        return true;
      }
    } catch (_) {}
    setRole('client');
    const name = email.includes('sarah') ? 'Sarah Jenkins' : email.includes('david') ? 'David Miller' : email.split('@')[0];
    setCurrentUser({ name, email });
    return true;
  };

  const loginAsAdmin = async (username, password) => {
    try {
      const email = username.includes('@') ? username : 'admin@ilyphoto.co.za';
      const res = await AzureApi.login(email, password);
      if (res && res.token) {
        localStorage.setItem('ily_azure_token', res.token);
        setRole('admin');
        setCurrentUser({
          id: res.user?.id,
          name: res.user?.displayName || 'Ily Admin',
          email: res.user?.email || email
        });
        return true;
      }
    } catch (_) {}
    setRole('admin');
    setCurrentUser({ name: 'Ily Admin', email: 'admin@ilyphoto.co.za' });
    return true;
  };

  const logout = () => {
    localStorage.removeItem('ily_azure_token');
    setRole('visitor');
    setCurrentUser(null);
  };

  // Booking Actions
  const addBooking = (newBookingData) => {
    const newId = `BK-${1000 + bookings.length + 1}`;
    const created = {
      id: newId,
      ...newBookingData,
      clientName: currentUser ? currentUser.name : newBookingData.clientName,
      clientEmail: currentUser ? currentUser.email : newBookingData.clientEmail,
      status: 'Pending',
      createdAt: new Date().toISOString().split('T')[0]
    };
    setBookings(prev => [created, ...prev]);

    // Asynchronously synchronize with Azure backend
    const token = localStorage.getItem('ily_azure_token');
    AzureApi.createBooking({
      serviceName: newBookingData.serviceName || newBookingData.category || 'Photo Session',
      eventDate: newBookingData.date || new Date().toISOString().split('T')[0],
      clientName: created.clientName,
      clientEmail: created.clientEmail,
      servicePrice: newBookingData.price || 800,
      status: 'Pending'
    }, token).catch(() => {});

    return created;
  };

  const updateBookingStatus = (bookingId, newStatus) => {
    setBookings(prev => prev.map(b => b.id === bookingId ? { ...b, status: newStatus } : b));
  };

  // Portfolio & Client Gallery Actions
  const addPublicPortfolioItem = (newItem) => {
    const item = {
      id: Date.now(),
      ...newItem,
      isPublic: true,
      featured: newItem.featured || false
    };
    setPortfolio(prev => [item, ...prev]);
  };

  const addPrivateClientPhoto = (photoData) => {
    const item = {
      id: Date.now(),
      ...photoData,
      uploadDate: new Date().toISOString().split('T')[0]
    };
    setClientGalleries(prev => [item, ...prev]);
  };

  const deletePortfolioItem = (id) => {
    setPortfolio(prev => prev.filter(p => p.id !== id));
  };

  const deleteClientPhoto = (id) => {
    setClientGalleries(prev => prev.filter(g => g.id !== id));
  };

  // Review Actions
  const addReview = (newReview) => {
    const rev = {
      id: Date.now(),
      ...newReview,
      author: currentUser ? currentUser.name : 'Anonymous Client',
      adminResponse: null,
      date: new Date().toISOString().split('T')[0]
    };
    setReviews(prev => [rev, ...prev]);
  };

  const respondToReview = (reviewId, responseText) => {
    setReviews(prev => prev.map(r => r.id === reviewId ? { ...r, adminResponse: responseText } : r));
  };

  // Invoice Actions
  const addInvoice = (invoiceData) => {
    const newInv = {
      id: `INV-2026-00${invoices.length + 1}`,
      ...invoiceData,
      status: 'Unpaid',
      issuedDate: new Date().toISOString().split('T')[0]
    };
    setInvoices(prev => [newInv, ...prev]);
  };

  const updateInvoiceStatus = (invoiceId, status) => {
    setInvoices(prev => prev.map(inv => inv.id === invoiceId ? { ...inv, status } : inv));
  };

  return (
    <AuthContext.Provider value={{
      role,
      currentUser,
      switchRole,
      loginAsClient,
      loginAsAdmin,
      logout,
      bookings,
      addBooking,
      updateBookingStatus,
      portfolio,
      addPublicPortfolioItem,
      deletePortfolioItem,
      clientGalleries,
      addPrivateClientPhoto,
      deleteClientPhoto,
      services,
      setServices,
      invoices,
      addInvoice,
      updateInvoiceStatus,
      reviews,
      addReview,
      respondToReview
    }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => useContext(AuthContext);
