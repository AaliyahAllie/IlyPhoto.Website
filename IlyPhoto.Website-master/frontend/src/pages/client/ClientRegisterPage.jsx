import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { UserPlus, AlertTriangle } from 'lucide-react';

export const ClientRegisterPage = () => {
  const [formData, setFormData] = useState({ fullName: '', username: '', email: '', phone: '', password: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { registerClient } = useAuth();
  const navigate = useNavigate();

  const handleRegister = async (e) => {
    e.preventDefault();
    setError('');
    if (!formData.fullName.trim() || !formData.email.trim() || !formData.password) {
      setError('Please complete all required registration fields.');
      return;
    }
    if (formData.password.length < 6) {
      setError('Password must be at least 6 characters.');
      return;
    }

    setLoading(true);
    try {
      await registerClient({
        fullName: formData.fullName,
        email: formData.email,
        password: formData.password,
        phone: formData.phone
      });
      navigate('/client/dashboard');
    } catch (err) {
      setError(err.message || 'Account registration failed. Please check your details and try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '480px' }}>
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Register</h1>
        <p style={{ color: '#2C2C2C' }}>Create your Client Portal account in Azure Table Storage</p>
      </div>

      <form onSubmit={handleRegister} className="card" style={{ padding: '2.5rem' }}>
        {error && (
          <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1.5px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.92rem', fontWeight: 700, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <AlertTriangle size={18} />
            <span>{error}</span>
          </div>
        )}

        <div className="form-group">
          <label className="form-label">Full Name *</label>
          <input
            type="text"
            className="form-control"
            placeholder="John Doe"
            value={formData.fullName}
            onChange={e => setFormData({ ...formData, fullName: e.target.value })}
            required
          />
        </div>

        <div className="form-group">
          <label className="form-label">Phone / WhatsApp</label>
          <input
            type="tel"
            className="form-control"
            placeholder="+27 82 000 0000"
            value={formData.phone}
            onChange={e => setFormData({ ...formData, phone: e.target.value })}
          />
        </div>

        <div className="form-group">
          <label className="form-label">Email Address *</label>
          <input
            type="email"
            className="form-control"
            placeholder="john@example.com"
            value={formData.email}
            onChange={e => setFormData({ ...formData, email: e.target.value })}
            required
          />
        </div>

        <div className="form-group">
          <label className="form-label">Password (min. 6 characters) *</label>
          <input
            type="password"
            className="form-control"
            placeholder="••••••••"
            minLength={6}
            value={formData.password}
            onChange={e => setFormData({ ...formData, password: e.target.value })}
            required
          />
        </div>

        <button type="submit" className="btn btn-black btn-full" style={{ marginTop: '1rem' }} disabled={loading}>
          <UserPlus size={18} /> {loading ? 'Creating Account...' : 'Register Account'}
        </button>

        <div style={{ textAlign: 'center', marginTop: '1.5rem', fontSize: '0.92rem', color: '#2C2C2C' }}>
          Already have an account? <Link to="/client/login" style={{ color: '#4A3525', fontWeight: 700, textDecoration: 'underline' }}>Sign in</Link>
        </div>
      </form>
    </div>
  );
};
