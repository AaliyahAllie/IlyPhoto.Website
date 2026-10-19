import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { initialBookings, initialPortfolio, initialServices, initialInvoices, initialReviews, initialClientGalleries } from '../services/mockData';
import { AzureApi } from '../services/azureApi';

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [role, setRole] = useState(() => localStorage.getItem('ily_role') || 'visitor');
  const [currentUser, setCurrentUser] = useState(() => {
    const saved = localStorage.getItem('ily_user');
    return saved ? JSON.parse(saved) : null;
  });
  const [cloudLoaded, setCloudLoaded] = useState(false);

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
  useEffect(() => {
    if (currentUser) localStorage.setItem('ily_user', JSON.stringify(currentUser));
    else localStorage.removeItem('ily_user');
  }, [currentUser]);
  useEffect(() => { localStorage.setItem('ily_bookings', JSON.stringify(bookings)); }, [bookings]);
  useEffect(() => { localStorage.setItem('ily_portfolio', JSON.stringify(portfolio)); }, [portfolio]);
  useEffect(() => { localStorage.setItem('ily_client_galleries', JSON.stringify(clientGalleries)); }, [clientGalleries]);
  useEffect(() => { localStorage.setItem('ily_services', JSON.stringify(services)); }, [services]);
  useEffect(() => { localStorage.setItem('ily_invoices', JSON.stringify(invoices)); }, [invoices]);
  useEffect(() => { localStorage.setItem('ily_reviews', JSON.stringify(reviews)); }, [reviews]);

  const refreshBookingsFromCloud = useCallback(async () => {
    try {
      const token = localStorage.getItem('ily_azure_token');
      const cloudList = await AzureApi.getBookings(token);
      if (Array.isArray(cloudList) && cloudList.length > 0) {
        const mapped = cloudList.map(b => ({
          id: b.id,
          clientId: b.clientId,
          clientName: b.clientName,
          clientEmail: b.clientEmail,
          clientPhone: b.clientPhone,
          serviceName: b.serviceName,
          price: b.servicePrice,
          durationHours: b.durationHours || 2,
          eventDate: b.eventDate,
          eventTime: b.eventTime || '10:00',
          endTime: b.endTime || '12:00',
          location: b.eventLocation || 'Cape Town',
          requirements: b.requirements || '',
          status: b.status || 'Pending',
          invoiceNumber: b.invoiceNumber,
          invoiceStatus: b.invoiceStatus
        }));
        setBookings(mapped);
      }
    } catch (_) {
      // Retain local state if offline, but mark load attempt completed
    } finally {
      setCloudLoaded(true);
    }
  }, []);

  useEffect(() => {
    refreshBookingsFromCloud();
  }, [refreshBookingsFromCloud]);

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

  // Strict Authentication: Rejects invalid credentials and never signs in with a cached profile on failure
  const loginAsClient = async (email, password) => {
    const res = await AzureApi.login(email, password);
    if (!res || !res.token) {
      throw new Error('Authentication failed. Please check your email and password.');
    }
    localStorage.setItem('ily_azure_token', res.token);
    const resolvedRole = res.user?.role === 'photographer' || res.user?.role === 'admin' ? 'admin' : 'client';
    setRole(resolvedRole);
    setCurrentUser({
      id: res.user?.uid || res.user?.id,
      clientId: res.user?.clientId,
      name: res.user?.displayName || res.user?.fullName || email.split('@')[0],
      email: res.user?.email || email
    });
    await refreshBookingsFromCloud();
    return true;
  };

  const loginAsAdmin = async (username, password) => {
    const email = username.includes('@') ? username.trim() : `${username.trim()}@ilyphoto.co.za`;
    const res = await AzureApi.login(email, password);
    if (!res || !res.token) {
      throw new Error('Administrator authentication failed.');
    }
    if (res.user?.role !== 'admin' && res.user?.role !== 'photographer') {
      throw new Error('This account does not have Administrator privileges.');
    }
    localStorage.setItem('ily_azure_token', res.token);
    setRole('admin');
    setCurrentUser({
      id: res.user?.uid || res.user?.id,
      clientId: res.user?.clientId || 'ADMIN',
      name: res.user?.displayName || res.user?.fullName || 'Ily Admin',
      email: res.user?.email || email
    });
    await refreshBookingsFromCloud();
    return true;
  };

  const logout = () => {
    localStorage.removeItem('ily_azure_token');
    localStorage.removeItem('ily_user');
    setRole('visitor');
    setCurrentUser(null);
  };

  // Booking Actions with Azure Cloud Persistence
  const addBooking = async (newBookingData) => {
    const token = localStorage.getItem('ily_azure_token');
    const payload = {
      serviceName: newBookingData.serviceName || newBookingData.category || 'Photo Session',
      eventDate: newBookingData.eventDate || newBookingData.date,
      eventTime: newBookingData.eventTime || '10:00',
      eventLocation: newBookingData.location || 'Cape Town',
      requirements: newBookingData.requirements || '',
      clientName: currentUser ? currentUser.name : newBookingData.clientName,
      clientEmail: currentUser ? currentUser.email : newBookingData.clientEmail,
      servicePrice: newBookingData.price || 800,
      status: 'Pending'
    };

    const res = await AzureApi.createBooking(payload, token);
    const b = res?.booking;
    const created = {
      id: b?.id || `BK-${1000 + bookings.length + 1}`,
      ...newBookingData,
      eventDate: b?.eventDate || payload.eventDate,
      eventTime: b?.eventTime || payload.eventTime,
      location: b?.eventLocation || payload.eventLocation,
      requirements: b?.requirements || payload.requirements,
      clientName: b?.clientName || payload.clientName,
      clientEmail: b?.clientEmail || payload.clientEmail,
      status: b?.status || 'Pending',
      createdAt: new Date().toISOString().split('T')[0]
    };
    setBookings(prev => [created, ...prev]);
    return created;
  };

  const updateBookingStatus = async (bookingId, newStatus, extraFields = {}) => {
    const token = localStorage.getItem('ily_azure_token');
    const patch = {
      status: newStatus,
      ...(extraFields.eventDate ? { eventDate: extraFields.eventDate } : {}),
      ...(extraFields.eventTime ? { eventTime: extraFields.eventTime } : {}),
      ...(extraFields.endTime ? { endTime: extraFields.endTime } : {}),
      ...(extraFields.location ? { eventLocation: extraFields.location } : {}),
      ...(extraFields.serviceName ? { serviceName: extraFields.serviceName } : {}),
      ...(extraFields.requirements !== undefined ? { requirements: extraFields.requirements } : {})
    };

    await AzureApi.updateBooking(bookingId, patch, token);

    setBookings(prev => prev.map(b => b.id === bookingId ? {
      ...b,
      status: newStatus,
      ...(extraFields.eventDate ? { eventDate: extraFields.eventDate } : {}),
      ...(extraFields.eventTime ? { eventTime: extraFields.eventTime } : {}),
      ...(extraFields.endTime ? { endTime: extraFields.endTime } : {}),
      ...(extraFields.location ? { location: extraFields.location } : {}),
      ...(extraFields.serviceName ? { serviceName: extraFields.serviceName } : {}),
      ...(extraFields.requirements !== undefined ? { requirements: extraFields.requirements } : {})
    } : b));
  };

  const rescheduleBooking = async (bookingId, { eventDate, eventTime, endTime, location, requirements, serviceName }) => {
    return updateBookingStatus(bookingId, 'Rescheduled', {
      eventDate,
      eventTime,
      endTime,
      location,
      requirements,
      serviceName
    });
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
      cloudLoaded,
      refreshBookingsFromCloud,
      switchRole,
      loginAsClient,
      loginAsAdmin,
      logout,
      bookings,
      addBooking,
      updateBookingStatus,
      rescheduleBooking,
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
