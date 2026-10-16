import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { LogIn } from 'lucide-react';

export const ClientLoginPage = () => {
  const [email, setEmail] = useState('sarah.j@example.com');
  const [password, setPassword] = useState('password123');
  const { loginAsClient } = useAuth();
  const navigate = useNavigate();

  const handleLogin = (e) => {
    e.preventDefault();
    if (email && password) {
      loginAsClient(email, password);
      navigate('/client/dashboard');
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '480px' }}>
      {/* Wireframe Header "Client / Login" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Login</h1>
        <p style={{ color: '#666' }}>Welcome back</p>
      </div>

      <form onSubmit={handleLogin} className="card" style={{ padding: '2.5rem' }}>
        <div className="form-group">
          <label className="form-label">Username or Email</label>
          <input
            type="text"
            className="form-control"
            value={email}
            onChange={e => setEmail(e.target.value)}
            required
          />
        </div>

        <div className="form-group">
          <label className="form-label">Password</label>
          <input
            type="password"
            className="form-control"
            value={password}
            onChange={e => setPassword(e.target.value)}
            required
          />
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem', fontSize: '0.85rem' }}>
          <Link to="/client/register" style={{ color: '#4A3525', fontWeight: 600 }}>Create new account</Link>
          <Link to="/client/reset-password" style={{ color: '#666' }}>Forgot password?</Link>
        </div>

        <button type="submit" className="btn btn-black btn-full">
          <LogIn size={18} /> Sign In
        </button>
      </form>
    </div>
  );
};
