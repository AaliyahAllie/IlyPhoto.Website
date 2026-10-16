import React, { useState } from 'react';
import { Mail, Phone, MapPin, Send, CheckCircle } from 'lucide-react';

export const ContactPage = () => {
  const [formData, setFormData] = useState({ name: '', email: '', phone: '', message: '' });
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!formData.name || !formData.email || !formData.message) return;
    setSubmitted(true);
    setTimeout(() => {
      setSubmitted(false);
      setFormData({ name: '', email: '', phone: '', message: '' });
    }, 4000);
  };

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Main / Contact Us" */}
      <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.5rem' }}>Contact Us</h1>
        <p style={{ color: '#666' }}>Can I get your number?</p>
      </div>

      <div style={{ maxWidth: '650px', margin: '0 auto' }}>
        {submitted ? (
          <div className="card" style={{ textAlign: 'center', padding: '3rem 2rem', backgroundColor: '#E8F5E9', borderColor: '#2D6A4F' }}>
            <CheckCircle size={48} color="#2D6A4F" style={{ marginBottom: '1rem' }} />
            <h3 style={{ color: '#2D6A4F', marginBottom: '0.5rem' }}>Enquiry Received!</h3>
            <p style={{ color: '#333' }}>
              Thank you for reaching out to Ily Photo. We have received your inquiry and will respond within 24 hours.
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="card" style={{ padding: '2.5rem' }}>
            <div className="form-group">
              <label className="form-label">Full Name *</label>
              <input
                type="text"
                className="form-control"
                placeholder="Enter your name"
                value={formData.name}
                onChange={e => setFormData({ ...formData, name: e.target.value })}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Email Address *</label>
              <input
                type="email"
                className="form-control"
                placeholder="name@example.com"
                value={formData.email}
                onChange={e => setFormData({ ...formData, email: e.target.value })}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Phone Number</label>
              <input
                type="tel"
                className="form-control"
                placeholder="+27 82 000 0000"
                value={formData.phone}
                onChange={e => setFormData({ ...formData, phone: e.target.value })}
              />
            </div>

            <div className="form-group">
              <label className="form-label">Enquiry Message *</label>
              <textarea
                className="form-control"
                placeholder="Tell us about your event, preferred dates, or photography questions..."
                value={formData.message}
                onChange={e => setFormData({ ...formData, message: e.target.value })}
                required
              ></textarea>
            </div>

            <button type="submit" className="btn btn-black btn-full">
              <Send size={18} /> Submit Request
            </button>
          </form>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginTop: '2.5rem', textAlign: 'center' }}>
          <div className="card" style={{ padding: '1.25rem' }}>
            <Mail size={24} color="#4A3525" style={{ marginBottom: '0.5rem' }} />
            <h4 style={{ fontSize: '0.9rem', marginBottom: '0.2rem' }}>Email</h4>
            <p style={{ fontSize: '0.8rem', color: '#666', margin: 0 }}>info@ilyphoto.co.za</p>
          </div>
          <div className="card" style={{ padding: '1.25rem' }}>
            <Phone size={24} color="#4A3525" style={{ marginBottom: '0.5rem' }} />
            <h4 style={{ fontSize: '0.9rem', marginBottom: '0.2rem' }}>Phone</h4>
            <p style={{ fontSize: '0.8rem', color: '#666', margin: 0 }}>+27 82 123 4567</p>
          </div>
          <div className="card" style={{ padding: '1.25rem' }}>
            <MapPin size={24} color="#4A3525" style={{ marginBottom: '0.5rem' }} />
            <h4 style={{ fontSize: '0.9rem', marginBottom: '0.2rem' }}>Location</h4>
            <p style={{ fontSize: '0.8rem', color: '#666', margin: 0 }}>Studio A, Cape Town</p>
          </div>
        </div>
      </div>
    </div>
  );
};
