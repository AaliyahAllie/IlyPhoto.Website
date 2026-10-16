import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Calendar, Users, DollarSign, Image as ImageIcon, CheckCircle, Clock, AlertCircle } from 'lucide-react';

export const AdminDashboardPage = () => {
  const { bookings, invoices, portfolio, reviews } = useAuth();

  const pendingCount = bookings.filter(b => b.status === 'Pending').length;
  const confirmedCount = bookings.filter(b => b.status === 'Confirmed').length;
  const totalClients = new Set(bookings.map(b => b.clientEmail)).size;
  const totalRevenue = invoices.filter(inv => inv.status === 'Paid').reduce((acc, inv) => acc + inv.amount, 0);

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Admin Side (Website)" */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2.5rem' }}>
        <div>
          <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Photographer Admin Dashboard</h1>
          <p style={{ color: '#666', margin: 0 }}>ILY Photo Business Statistics & Management</p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem' }}>
          <Link to="/admin/bookings" className="btn btn-primary">
            <Calendar size={16} /> Manage Bookings
          </Link>
          <Link to="/admin/gallery" className="btn btn-black">
            <ImageIcon size={16} /> Upload Gallery
          </Link>
        </div>
      </div>

      {/* Business Statistics Grid FR23 */}
      <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', marginBottom: '3rem' }}>
        <div className="card" style={{ backgroundColor: '#FAF7F2', borderColor: '#EAE3D2' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: '#666', fontWeight: 600 }}>Total Clients</span>
            <Users size={20} color="#4A3525" />
          </div>
          <div style={{ fontSize: '2.25rem', fontWeight: 800, color: '#1A1A1A' }}>{totalClients}</div>
          <p style={{ fontSize: '0.78rem', color: '#4A3525', margin: 0 }}>Registered & Active</p>
        </div>

        <div className="card" style={{ backgroundColor: '#FEF3C7', borderColor: '#F59E0B' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: '#92400E', fontWeight: 600 }}>Pending Requests</span>
            <Clock size={20} color="#D97706" />
          </div>
          <div style={{ fontSize: '2.25rem', fontWeight: 800, color: '#92400E' }}>{pendingCount}</div>
          <p style={{ fontSize: '0.78rem', color: '#D97706', margin: 0 }}>Requires Action</p>
        </div>

        <div className="card" style={{ backgroundColor: '#E8F5E9', borderColor: '#2D6A4F' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: '#2D6A4F', fontWeight: 600 }}>Upcoming Shoots</span>
            <CheckCircle size={20} color="#2D6A4F" />
          </div>
          <div style={{ fontSize: '2.25rem', fontWeight: 800, color: '#2D6A4F' }}>{confirmedCount}</div>
          <p style={{ fontSize: '0.78rem', color: '#2D6A4F', margin: 0 }}>Confirmed Sessions</p>
        </div>

        <div className="card" style={{ backgroundColor: '#1A1A1A', color: '#FFF' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
            <span style={{ fontSize: '0.85rem', color: '#DDD', fontWeight: 600 }}>Revenue Collected</span>
            <DollarSign size={20} color="#FAF7F2" />
          </div>
          <div style={{ fontSize: '2.25rem', fontWeight: 800, color: '#FFF' }}>R{totalRevenue.toLocaleString()}</div>
          <p style={{ fontSize: '0.78rem', color: '#AAA', margin: 0 }}>Paid Invoices</p>
        </div>
      </div>

      {/* Bookings Overview Table matching Wireframe 2 Admin/Bookings */}
      <section className="card" style={{ padding: '2rem' }}>
        <h3 style={{ marginBottom: '1.25rem' }}>Recent Booking Appointments</h3>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
          <thead>
            <tr style={{ backgroundColor: '#EAE3D2', textAlign: 'left' }}>
              <th style={{ padding: '0.75rem 1rem' }}>ID</th>
              <th style={{ padding: '0.75rem 1rem' }}>Client</th>
              <th style={{ padding: '0.75rem 1rem' }}>Service</th>
              <th style={{ padding: '0.75rem 1rem' }}>Date & Time</th>
              <th style={{ padding: '0.75rem 1rem' }}>Status</th>
            </tr>
          </thead>
          <tbody>
            {bookings.slice(0, 5).map(b => (
              <tr key={b.id} style={{ borderBottom: '1px solid #E2DACB' }}>
                <td style={{ padding: '0.75rem 1rem', fontWeight: 700 }}>{b.id}</td>
                <td style={{ padding: '0.75rem 1rem' }}>
                  <strong>{b.clientName}</strong>
                  <br />
                  <span style={{ fontSize: '0.78rem', color: '#666' }}>{b.clientEmail}</span>
                </td>
                <td style={{ padding: '0.75rem 1rem' }}>{b.serviceName}</td>
                <td style={{ padding: '0.75rem 1rem' }}>{b.eventDate} at {b.eventTime}</td>
                <td style={{ padding: '0.75rem 1rem' }}><StatusBadge status={b.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
};
