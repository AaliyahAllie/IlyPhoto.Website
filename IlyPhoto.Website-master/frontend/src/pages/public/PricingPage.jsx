import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { Check, Calendar } from 'lucide-react';

export const PricingPage = () => {
  const { services } = useAuth();
  const navigate = useNavigate();

  const handleSelectPackage = (serviceName) => {
    navigate('/client/booking', { state: { selectedService: serviceName } });
  };

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Main / Pricing" */}
      <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.5rem' }}>Packages</h1>
        <p style={{ color: '#666' }}>Let's work business</p>
      </div>

      {/* Package Cards matching Wireframe 1 dark/light card blocks */}
      <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))' }}>
        {services.map((srv, index) => {
          const isFeatured = index === 1; // Middle card dark accent matching wireframe
          return (
            <div
              key={srv.id}
              className={`card ${isFeatured ? 'card-dark' : ''}`}
              style={{
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                padding: '2rem',
                border: isFeatured ? 'none' : '1px solid #E2DACB'
              }}
            >
              <div>
                <span
                  className="badge"
                  style={{
                    backgroundColor: isFeatured ? '#4A3525' : '#EAE3D2',
                    color: isFeatured ? '#FFF' : '#4A3525',
                    marginBottom: '1rem'
                  }}
                >
                  {srv.duration}
                </span>

                <h2 style={{ fontSize: '1.75rem', marginBottom: '0.5rem', color: isFeatured ? '#FFF' : '#1A1A1A' }}>
                  {srv.name}
                </h2>
                <p style={{ color: isFeatured ? '#DDD' : '#666', fontSize: '0.95rem', marginBottom: '1.5rem' }}>
                  {srv.description}
                </p>

                <div style={{ fontSize: '2.25rem', fontWeight: 800, marginBottom: '1.5rem', color: isFeatured ? '#FFF' : '#4A3525' }}>
                  R{srv.price.toLocaleString()}
                  <span style={{ fontSize: '0.9rem', fontWeight: 400, color: isFeatured ? '#BBB' : '#666' }}> / session</span>
                </div>

                <ul style={{ listStyle: 'none', marginBottom: '2rem' }}>
                  {srv.features?.map((ft, i) => (
                    <li key={i} style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.65rem', fontSize: '0.9rem', color: isFeatured ? '#EEE' : '#333' }}>
                      <Check size={16} color={isFeatured ? '#FAF7F2' : '#4A3525'} />
                      <span>{ft}</span>
                    </li>
                  ))}
                </ul>
              </div>

              <button
                onClick={() => handleSelectPackage(srv.name)}
                className={`btn ${isFeatured ? 'btn-secondary' : 'btn-black'} btn-full`}
              >
                <Calendar size={18} /> Book This Package
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
};
