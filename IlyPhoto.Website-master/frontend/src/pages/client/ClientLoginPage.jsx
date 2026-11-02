import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { LogIn, AlertTriangle } from 'lucide-react';

export const ClientLoginPage = () => {
  const [email, setEmail] = useState('sarah@example.com');
  const [password, setPassword] = useState('Password123!');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { loginAsClient } = useAuth();
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    setError('');
    if (!email || !password) {
      setError('Please enter both your email address and password.');
      return;
    }
    setLoading(true);
    try {
      await loginAsClient(email, password);
      navigate('/client/dashboard');
    } catch (err) {
      setError(err.message || 'Authentication failed. Please check your credentials.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '480px' }}>
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Login</h1>
        <p style={{ color: '#2C2C2C' }}>Sign in to your Client Portal</p>
      </div>

      <form onSubmit={handleLogin} className="card" style={{ padding: '2.5rem' }}>
        {error && (
          <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.92rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <AlertTriangle size={18} />
            <span>{error}</span>
          </div>
        )}

        <div className="form-group">
          <label className="form-label">Registered Email Address</label>
          <input
            type="email"
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

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem', fontSize: '0.92rem' }}>
          <Link to="/client/register" style={{ color: '#4A3525', fontWeight: 700 }}>Create new account</Link>
          <Link to="/client/reset-password" style={{ color: '#4A3525', fontWeight: 700, textDecoration: 'underline' }}>Forgot password?</Link>
        </div>

        <button type="submit" className="btn btn-black btn-full" disabled={loading}>
          <LogIn size={18} /> {loading ? 'Verifying Credentials...' : 'Sign In'}
        </button>
      </form>
    </div>
  );
};
