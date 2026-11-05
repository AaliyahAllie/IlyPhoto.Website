import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Modal } from '../../components/common/Modal';
import { Calendar, Image as ImageIcon, FileText, Star, PlusCircle, ArrowRight, RefreshCw, AlertTriangle } from 'lucide-react';

export const ClientDashboardPage = () => {
  const { currentUser, bookings, clientGalleries, invoices, rescheduleBooking } = useAuth();
  const [editingBooking, setEditingBooking] = useState(null);
  const [editDate, setEditDate] = useState('');
  const [editTime, setEditTime] = useState('');
  const [editLocation, setEditLocation] = useState('');
  const [editNotes, setEditNotes] = useState('');
  const [editError, setEditError] = useState('');

  const userEmail = currentUser?.email || 'sarah.j@example.com';
  const userName = currentUser?.name || 'Sarah';

  // Filter client's specific bookings and photos
  const myBookings = bookings.filter(b => b.clientEmail === userEmail);
  const myPhotos = clientGalleries.filter(g => g.clientEmail === userEmail);
  const myInvoices = invoices.filter(inv => inv.clientEmail === userEmail);

  const openRescheduleModal = (b) => {
    setEditingBooking(b);
    setEditDate(b.eventDate || '');
    setEditTime(b.eventTime || '10:00');
    setEditLocation(b.location || '');
    setEditNotes(b.requirements || '');
    setEditError('');
  };

  const handleSaveReschedule = async (e) => {
    e.preventDefault();
    if (!editingBooking) return;
    setEditError('');
    try {
      await rescheduleBooking(editingBooking.id, {
        eventDate: editDate || editingBooking.eventDate,
        eventTime: editTime || editingBooking.eventTime,
        location: editLocation || editingBooking.location,
        requirements: editNotes,
        serviceName: editingBooking.serviceName
      });
      setEditingBooking(null);
    } catch (err) {
      setEditError(err.message || 'Could not reschedule booking. The selected date may already be booked.');
    }
  };

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Client/Landing" */}
      <section className="hero-section" style={{ padding: '3rem 2rem', textAlign: 'left', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1.5rem' }}>
        <div>
          <h1 style={{ fontSize: '2.5rem', marginBottom: '0.35rem' }}>Welcome</h1>
          <p style={{ color: '#666', fontSize: '1.1rem', margin: 0 }}>Welcome back! {userName}</p>
        </div>
        <div style={{ display: 'flex', gap: '0.75rem' }}>
          <Link to="/client/booking" className="btn btn-primary">
            <PlusCircle size={18} /> New Booking
          </Link>
          <Link to="/client/gallery" className="btn btn-outline">
            <ImageIcon size={18} /> My Private Gallery
          </Link>
        </div>
      </section>

      {/* Gallery Wireframe Preview Row */}
      <section style={{ marginBottom: '2.5rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h3 style={{ margin: 0 }}>My Private Gallery Preview</h3>
          <Link to="/client/gallery" style={{ fontSize: '0.9rem', fontWeight: 600 }}>
            View Full Gallery ({myPhotos.length}) <ArrowRight size={14} />
          </Link>
        </div>

        {myPhotos.length > 0 ? (
          <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))' }}>
            {myPhotos.map(photo => (
              <div key={photo.id} className="card" style={{ padding: 0, overflow: 'hidden' }}>
                <div className="wireframe-box" style={{ height: '160px' }}>
                  <img src={photo.imageUrl} alt={photo.title} />
                </div>
                <div style={{ padding: '0.75rem' }}>
                  <h5 style={{ margin: 0, fontSize: '0.9rem' }}>{photo.title}</h5>
                  <span style={{ fontSize: '0.75rem', color: '#666' }}>Booking: {photo.bookingId}</span>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="card" style={{ padding: '2rem', textAlign: 'center', color: '#666' }}>
            <ImageIcon size={36} color="#4A3525" style={{ marginBottom: '0.5rem' }} />
            <p>No photos uploaded to your private gallery yet. Once your photographer completes your session, your private gallery will appear here!</p>
          </div>
        )}
      </section>

      {/* Bookings Section matching Wireframe 3 */}
      <section>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h3 style={{ margin: 0 }}>My Event Bookings</h3>
          <span style={{ fontSize: '0.88rem', color: '#666' }}>Total: {myBookings.length} Bookings</span>
        </div>

        <div className="card-grid">
          {myBookings.map(b => (
            <div key={b.id} className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                  <span style={{ fontWeight: 700, fontSize: '0.9rem', color: '#4A3525' }}>{b.id}</span>
                  <StatusBadge status={b.status} />
                </div>
                <h4 style={{ fontSize: '1.15rem', marginBottom: '0.35rem' }}>{b.serviceName}</h4>
                <p style={{ fontSize: '0.88rem', color: '#666', marginBottom: '0.25rem' }}>📅 {b.eventDate} at {b.eventTime}</p>
                <p style={{ fontSize: '0.88rem', color: '#666', marginBottom: '0.75rem' }}>📍 {b.location}</p>
              </div>

              <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem', borderTop: '1px solid #E2DACB', paddingTop: '0.75rem', flexWrap: 'wrap' }}>
                <Link to="/client/invoices" className="btn btn-secondary btn-sm" style={{ flex: 1 }}>
                  <FileText size={14} /> Invoices
                </Link>
                {(b.status === 'Pending' || b.status === 'Confirmed' || b.status === 'Rescheduled') && (
                  <button type="button" onClick={() => openRescheduleModal(b)} className="btn btn-outline btn-sm" style={{ flex: 1 }}>
                    <RefreshCw size={14} /> Reschedule
                  </button>
                )}
                {b.status === 'Completed' && (
                  <Link to="/client/reviews" className="btn btn-primary btn-sm" style={{ flex: 1 }}>
                    <Star size={14} /> Review
                  </Link>
                )}
              </div>
            </div>
          ))}
        </div>
      </section>

      <Modal isOpen={!!editingBooking} onClose={() => setEditingBooking(null)} title={`Edit / Reschedule Booking: ${editingBooking?.id}`}>
        {editingBooking && (
          <form onSubmit={handleSaveReschedule}>
            {editError && (
              <div style={{ padding: '0.75rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.9rem', fontWeight: 700, marginBottom: '1rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <AlertTriangle size={18} />
                <span>{editError}</span>
              </div>
            )}
            <div className="form-group">
              <label className="form-label">New Event Date *</label>
              <input type="date" className="form-control" value={editDate} onChange={e => setEditDate(e.target.value)} required />
            </div>
            <div className="form-group">
              <label className="form-label">New Event Time *</label>
              <input type="time" className="form-control" value={editTime} onChange={e => setEditTime(e.target.value)} required />
            </div>
            <div className="form-group">
              <label className="form-label">Venue / Location *</label>
              <input type="text" className="form-control" value={editLocation} onChange={e => setEditLocation(e.target.value)} required />
            </div>
            <div className="form-group">
              <label className="form-label">Notes / Requirements</label>
              <textarea className="form-control" value={editNotes} onChange={e => setEditNotes(e.target.value)} />
            </div>
            <button type="submit" className="btn btn-primary btn-full">
              <RefreshCw size={16} /> Save Rescheduled Date, Time &amp; Venue
            </button>
          </form>
        )}
      </Modal>
    </div>
  );
};
