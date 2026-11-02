import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { KeyRound, CheckCircle, AlertTriangle } from 'lucide-react';
import { AzureApi } from '../../services/azureApi';

export const PasswordResetPage = () => {
  const [email, setEmail] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [sent, setSent] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleReset = async (e) => {
    e.preventDefault();
    setError('');
    const cleanEmail = email.trim();
    if (!cleanEmail || !cleanEmail.includes('@') || !cleanEmail.includes('.')) {
      setError('Please enter a valid registered email address.');
      return;
    }
    if (newPassword && newPassword.length < 6) {
      setError('If setting a new password, it must be at least 6 characters.');
      return;
    }

    setLoading(true);
    try {
      await AzureApi.resetPassword(cleanEmail, newPassword || undefined);
      setSent(true);
    } catch (err) {
      setError(err.message || 'No registered account was found for that email address.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '480px' }}>
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Password Recovery</h1>
        <p style={{ color: '#2C2C2C' }}>Verify your registered account in Azure Table Storage</p>
      </div>

      {sent ? (
        <div className="card" style={{ textAlign: 'center', padding: '2.5rem', backgroundColor: '#E8F8F5', borderColor: '#0E6251' }}>
          <CheckCircle size={48} color="#0E6251" style={{ marginBottom: '1rem' }} />
          <h3 style={{ color: '#0E6251', marginBottom: '0.5rem' }}>Password Recovery Completed</h3>
          <p style={{ color: '#1A1A1A', fontSize: '0.95rem', marginBottom: '1.5rem' }}>
            Your account (<strong>{email}</strong>) has been verified and a recovery notification has been recorded.
          </p>
          <Link to="/client/login" className="btn btn-black btn-full">
            Return to Login
          </Link>
        </div>
      ) : (
        <form onSubmit={handleReset} className="card" style={{ padding: '2.5rem' }}>
          {error && (
            <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.92rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <AlertTriangle size={18} />
              <span>{error}</span>
            </div>
          )}

          <div className="form-group">
            <label className="form-label">Registered Email Address *</label>
            <input
              type="email"
              className="form-control"
              placeholder="name@example.com"
              value={email}
              onChange={e => setEmail(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label className="form-label">New Password (Optional - min. 6 chars)</label>
            <input
              type="password"
              className="form-control"
              placeholder="••••••••"
              minLength={6}
              value={newPassword}
              onChange={e => setNewPassword(e.target.value)}
            />
          </div>

          <button type="submit" className="btn btn-black btn-full" disabled={loading}>
            <KeyRound size={18} /> {loading ? 'Verifying Account...' : 'Verify & Recover Password'}
          </button>

          <div style={{ textAlign: 'center', marginTop: '1.5rem', fontSize: '0.92rem' }}>
            Remembered your password? <Link to="/client/login" style={{ color: '#4A3525', fontWeight: 700 }}>Back to login</Link>
          </div>
        </form>
      )}
    </div>
  );
};
