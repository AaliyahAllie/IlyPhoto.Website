import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { KeyRound, CheckCircle } from 'lucide-react';

export const PasswordResetPage = () => {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState(false);

  const handleReset = (e) => {
    e.preventDefault();
    if (email) {
      setSent(true);
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '480px' }}>
      {/* Wireframe Header "Authentica... Password reset" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Password Reset</h1>
        <p style={{ color: '#666' }}>Recover your account access</p>
      </div>

      {sent ? (
        <div className="card" style={{ textAlign: 'center', padding: '2.5rem', backgroundColor: '#E8F5E9' }}>
          <CheckCircle size={48} color="#2D6A4F" style={{ marginBottom: '1rem' }} />
          <h3 style={{ color: '#2D6A4F', marginBottom: '0.5rem' }}>Reset Link Sent</h3>
          <p style={{ color: '#333', fontSize: '0.9rem', marginBottom: '1.5rem' }}>
            We've emailed instructions to <strong>{email}</strong>. Check your inbox to reset your password.
          </p>
          <Link to="/client/login" className="btn btn-black btn-full">
            Return to Login
          </Link>
        </div>
      ) : (
        <form onSubmit={handleReset} className="card" style={{ padding: '2.5rem' }}>
          <div className="form-group">
            <label className="form-label">Registered Email Address</label>
            <input
              type="email"
              className="form-control"
              placeholder="name@example.com"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
            />
          </div>

          <button type="submit" className="btn btn-black btn-full">
            <KeyRound size={18} /> Send Reset Link
          </button>

          <div style={{ textAlign: 'center', marginTop: '1.5rem', fontSize: '0.85rem' }}>
            Remembered your password? <Link to="/client/login" style={{ color: '#4A3525', fontWeight: 600 }}>Back to login</Link>
          </div>
        </form>
      )}
    </div>
  );
};
