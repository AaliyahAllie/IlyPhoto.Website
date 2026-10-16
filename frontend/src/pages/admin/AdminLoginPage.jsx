import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { ShieldCheck, Lock } from 'lucide-react';

export const AdminLoginPage = () => {
  const [username, setUsername] = useState('admin@ilyphoto.co.za');
  const [password, setPassword] = useState('Password123!');
  const { loginAsAdmin } = useAuth();
  const navigate = useNavigate();

  const handleAdminLogin = (e) => {
    e.preventDefault();
    if (username && password) {
      loginAsAdmin(username, password);
      navigate('/admin/dashboard');
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '440px' }}>
      {/* Wireframe Header "Admin / Login" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <ShieldCheck size={48} color="#4A3525" style={{ marginBottom: '0.5rem' }} />
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Login</h1>
        <p style={{ color: '#666' }}>Photographer Admin Side</p>
      </div>

      <form onSubmit={handleAdminLogin} className="card" style={{ padding: '2.5rem' }}>
        <div className="form-group">
          <label className="form-label">Admin Username</label>
          <input
            type="text"
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

        <div style={{ marginBottom: '1.25rem', background: '#F8F6F0', padding: '0.75rem', borderRadius: '6px', border: '1px dashed #D1C7B7' }}>
          <p style={{ fontSize: '0.75rem', fontWeight: 700, color: '#7A6B5D', margin: '0 0 0.4rem 0', textTransform: 'uppercase' }}>Default Administrator Accounts:</p>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <button type="button" onClick={() => { setUsername('admin@ilyphoto.co.za'); setPassword('Password123!'); }} style={{ fontSize: '0.75rem', background: '#EFE7DA', border: '1px solid #D1C7B7', borderRadius: '4px', padding: '0.3rem 0.6rem', cursor: 'pointer', color: '#4A3525', fontWeight: 600 }}>
              admin@ilyphoto.co.za
            </button>
            <button type="button" onClick={() => { setUsername('ayshaallie23@gmail.com'); setPassword('Password123!'); }} style={{ fontSize: '0.75rem', background: '#EFE7DA', border: '1px solid #D1C7B7', borderRadius: '4px', padding: '0.3rem 0.6rem', cursor: 'pointer', color: '#4A3525', fontWeight: 600 }}>
              ayshaallie23@gmail.com
            </button>
          </div>
        </div>

        <button type="submit" className="btn btn-black btn-full" style={{ marginTop: '0.5rem' }}>
          <Lock size={16} /> Enter Photographer Dashboard
        </button>
      </form>
    </div>
  );
};
