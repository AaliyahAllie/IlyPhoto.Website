/**
 * IlyPhoto - Unified Azure API Client for React Frontend
 *
 * Connects to the same Azure REST API and database used by the Android App.
 */

const API_BASE_URL = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1'
  ? 'https://ilyphoto-api-st10212542.azurewebsites.net/api'
  : 'https://ilyphoto-api-st10212542.azurewebsites.net/api';

export const AzureApi = {
  baseUrl: API_BASE_URL,

  async getHealth() {
    const res = await fetch(`${API_BASE_URL}/health`);
    return res.json();
  },

  async login(email, password) {
    const res = await fetch(`${API_BASE_URL}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Login failed');
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

  async getBookings(token) {
    const headers = token ? { Authorization: `Bearer ${token}` } : {};
    const res = await fetch(`${API_BASE_URL}/bookings`, { headers });
    return res.json();
  },

  async createBooking(booking, token) {
    const res = await fetch(`${API_BASE_URL}/bookings`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify(booking)
    });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.error || 'Booking creation failed');
    }
    return res.json();
  },

  async getPhotos() {
    const res = await fetch(`${API_BASE_URL}/photos`);
    return res.json();
  },

  async getReviews() {
    const res = await fetch(`${API_BASE_URL}/reviews`);
    return res.json();
  },

  async createReview(review, token) {
    const res = await fetch(`${API_BASE_URL}/reviews`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
      },
      body: JSON.stringify(review)
    });
    return res.json();
  },

  async createEnquiry(enquiry) {
    const res = await fetch(`${API_BASE_URL}/enquiries`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(enquiry)
    });
    return res.json();
  }
};
