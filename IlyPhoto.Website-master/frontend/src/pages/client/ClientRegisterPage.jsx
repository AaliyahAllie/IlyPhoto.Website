import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { UserPlus } from 'lucide-react';

export const ClientRegisterPage = () => {
  const [formData, setFormData] = useState({ fullName: '', username: '', email: '', password: '' });
  const { loginAsClient } = useAuth();
  const navigate = useNavigate();

  const handleRegister = (e) => {
    e.preventDefault();
    if (formData.email && formData.password) {
      loginAsClient(formData.email, formData.password);
      navigate('/client/dashboard');
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '480px' }}>
      {/* Wireframe Header "Client / Register" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Register</h1>
        <p style={{ color: '#666' }}>Create your account</p>
      </div>

      <form onSubmit={handleRegister} className="card" style={{ padding: '2.5rem' }}>
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
          <label className="form-label">Username *</label>
          <input
            type="text"
            className="form-control"
            placeholder="johndoe"
            value={formData.username}
            onChange={e => setFormData({ ...formData, username: e.target.value })}
            required
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
          <label className="form-label">Password *</label>
          <input
            type="password"
            className="form-control"
            placeholder="••••••••"
            value={formData.password}
            onChange={e => setFormData({ ...formData, password: e.target.value })}
            required
          />
        </div>

        <button type="submit" className="btn btn-black btn-full" style={{ marginTop: '1rem' }}>
          <UserPlus size={18} /> Register Account
        </button>

        <div style={{ textAlign: 'center', marginTop: '1.5rem', fontSize: '0.85rem' }}>
          Already have an account? <Link to="/client/login" style={{ color: '#4A3525', fontWeight: 600 }}>Sign in</Link>
        </div>
      </form>
    </div>
  );
};
