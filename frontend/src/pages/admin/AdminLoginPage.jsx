import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ShieldCheck, Lock, AlertTriangle } from 'lucide-react';

export const AdminLoginPage = () => {
  const [username, setUsername] = useState('admin@ilyphoto.co.za');
  const [password, setPassword] = useState('Password123!');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { loginAsAdmin } = useAuth();
  const navigate = useNavigate();

  const handleAdminLogin = async (e) => {
    e.preventDefault();
    setError('');
    if (!username || !password) {
      setError('Please enter administrator email and password.');
      return;
    }
    setLoading(true);
    try {
      await loginAsAdmin(username, password);
      navigate('/admin/dashboard');
    } catch (err) {
      setError(err.message || 'Administrator authentication failed.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '440px' }}>
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <ShieldCheck size={48} color="#4A3525" style={{ marginBottom: '0.5rem' }} />
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Admin Login</h1>
        <p style={{ color: '#2C2C2C' }}>Photographer Admin Portal</p>
      </div>

      <form onSubmit={handleAdminLogin} className="card" style={{ padding: '2.5rem' }}>
        {error && (
          <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.92rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
            <AlertTriangle size={18} />
            <span>{error}</span>
          </div>
        )}

        <div className="form-group">
          <label className="form-label">Admin Email</label>
          <input
            type="email"
            className="form-control"
            value={username}
            onChange={e => setUsername(e.target.value)}
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

        <div style={{ marginBottom: '1.25rem', background: '#F5EFE6', padding: '0.85rem', borderRadius: '6px', border: '1px solid #C5B8A5' }}>
          <p style={{ fontSize: '0.82rem', fontWeight: 700, color: '#2C1E14', margin: '0 0 0.4rem 0', textTransform: 'uppercase' }}>Default Administrator Accounts:</p>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <button type="button" onClick={() => { setUsername('admin@ilyphoto.co.za'); setPassword('Password123!'); }} style={{ fontSize: '0.82rem', background: '#FFF', border: '1px solid #4A3525', borderRadius: '4px', padding: '0.35rem 0.65rem', cursor: 'pointer', color: '#2C1E14', fontWeight: 700 }}>
              admin@ilyphoto.co.za
            </button>
            <button type="button" onClick={() => { setUsername('ayshaallie23@gmail.com'); setPassword('Password123!'); }} style={{ fontSize: '0.82rem', background: '#FFF', border: '1px solid #4A3525', borderRadius: '4px', padding: '0.35rem 0.65rem', cursor: 'pointer', color: '#2C1E14', fontWeight: 700 }}>
              ayshaallie23@gmail.com
            </button>
          </div>
        </div>

        <button type="submit" className="btn btn-black btn-full" style={{ marginTop: '0.5rem' }} disabled={loading}>
          <Lock size={16} /> {loading ? 'Verifying...' : 'Enter Photographer Dashboard'}
        </button>
      </form>
    </div>
  );
};
