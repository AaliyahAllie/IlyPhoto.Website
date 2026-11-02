/**
 * IlyPhoto - Unified Azure API Client for React Frontend
 *
 * Connects to the same Azure REST API and Azure Table Storage (ilyphotostorage)
 * database used by the IlyPhoto Desktop Web Portal and Android App.
 */

const API_BASE_URL = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'
  ? '/api'
  : 'https://ilyphoto-api-st10212542.azurewebsites.net/api';

export const AzureApi = {
  baseUrl: API_BASE_URL,

  async getHealth() {
    const res = await fetch(`${API_BASE_URL}/health`);
    if (!res.ok) throw new Error('Health check failed');
    return res.json();
  },

  async login(email, password) {
    if (!email || !password) {
      throw new Error('Email and password are required.');
    }
    const res = await fetch(`${API_BASE_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: email.trim(), password })
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Incorrect email or password.');
    }
    return res.json();
  },

  async register(userData) {
    const res = await fetch(`${API_BASE_URL}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(userData)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Registration failed');
    }
    return res.json();
  },

  async resetPassword(email, newPassword) {
    if (!email || !email.includes('@')) {
      throw new Error('Please enter a valid registered email address.');
    }
    const res = await fetch(`${API_BASE_URL}/auth/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: email.trim(), newPassword })
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok || data.ok === false) {
      throw new Error(data.error || 'No registered account was found for that email address.');
    }
    return data;
  },

  async getBookings(token) {
    const headers = token ? { Authorization: `Bearer ${token}` } : {};
    const res = await fetch(`${API_BASE_URL}/bookings`, { headers });
    if (!res.ok) throw new Error('Failed to load cloud bookings');
    return res.json();
  },

  async createBooking(booking, token) {
    const res = await fetch(`${API_BASE_URL}/bookings`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      },
      body: JSON.stringify(booking)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Booking creation failed');
    }
    return res.json();
  },

  async updateBooking(id, patch, token) {
    const headers = {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {})
    };
    let res = await fetch(`${API_BASE_URL}/bookings/${encodeURIComponent(id)}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(patch)
    });
    if (res.status === 404 || res.status === 405) {
      res = await fetch(`${API_BASE_URL}/bookings/${encodeURIComponent(id)}/status`, {
        method: 'PATCH',
        headers,
        body: JSON.stringify(patch)
      });
    }
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Failed to update booking in Azure Table Storage');
    }
    return res.json();
  },

  async getNotifications(token) {
    const headers = token ? { Authorization: `Bearer ${token}` } : {};
    const res = await fetch(`${API_BASE_URL}/notifications`, { headers });
    if (!res.ok) return [];
    return res.json();
  },

  async getPhotos() {
    const res = await fetch(`${API_BASE_URL}/photos`);
    if (!res.ok) throw new Error('Failed to load photos');
    return res.json();
  },

  async getReviews() {
    const res = await fetch(`${API_BASE_URL}/reviews`);
    if (!res.ok) throw new Error('Failed to load reviews');
    return res.json();
  },

  async createReview(review, token) {
    const res = await fetch(`${API_BASE_URL}/reviews`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      },
      body: JSON.stringify(review)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Failed to submit review');
    }
    return res.json();
  },

  async createEnquiry(enquiry) {
    const res = await fetch(`${API_BASE_URL}/enquiries`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(enquiry)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Failed to submit enquiry');
    }
    return res.json();
  }
};
