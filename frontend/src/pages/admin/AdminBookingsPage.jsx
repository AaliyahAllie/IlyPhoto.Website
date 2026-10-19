import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Modal } from '../../components/common/Modal';
import { Check, X, RefreshCw, CheckCircle2, AlertTriangle } from 'lucide-react';

export const AdminBookingsPage = () => {
  const { bookings, updateBookingStatus, rescheduleBooking } = useAuth();
  const [selectedBooking, setSelectedBooking] = useState(null);
  const [rescheduleDate, setRescheduleDate] = useState('');
  const [rescheduleTime, setRescheduleTime] = useState('');
  const [rescheduleLocation, setRescheduleLocation] = useState('');
  const [rescheduleNotes, setRescheduleNotes] = useState('');
  const [actionError, setActionError] = useState('');

  const openManageModal = (b) => {
    setSelectedBooking(b);
    setRescheduleDate(b.eventDate || '');
    setRescheduleTime(b.eventTime || '10:00');
    setRescheduleLocation(b.location || '');
    setRescheduleNotes(b.requirements || '');
    setActionError('');
  };

  const handleAction = async (bookingId, status) => {
    setActionError('');
    try {
      await updateBookingStatus(bookingId, status);
      if (selectedBooking && selectedBooking.id === bookingId) {
        setSelectedBooking(prev => ({ ...prev, status }));
      }
    } catch (err) {
      setActionError(err.message || 'Could not update booking status.');
    }
  };

  const handleRescheduleSave = async () => {
    if (!selectedBooking) return;
    setActionError('');
    try {
      await rescheduleBooking(selectedBooking.id, {
        eventDate: rescheduleDate || selectedBooking.eventDate,
        eventTime: rescheduleTime || selectedBooking.eventTime,
        location: rescheduleLocation || selectedBooking.location,
        requirements: rescheduleNotes,
        serviceName: selectedBooking.serviceName
      });
      setSelectedBooking(null);
    } catch (err) {
      setActionError(err.message || 'Could not reschedule booking.');
    }
  };

  return (
    <div className="page-wrapper container">
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Booking Management</h1>
        <p style={{ color: '#2C2C2C' }}>Review, approve, complete, and reschedule client appointments</p>
      </div>

      {actionError && (
        <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.92rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
          <AlertTriangle size={18} />
          <span>{actionError}</span>
        </div>
      )}

      <div className="card-grid">
        {bookings.map(b => (
          <div key={b.id} className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                <span style={{ fontWeight: 800, color: '#4A3525' }}>{b.id}</span>
                <StatusBadge status={b.status} />
              </div>

              <h3 style={{ fontSize: '1.2rem', marginBottom: '0.35rem' }}>{b.serviceName}</h3>
              <p style={{ fontWeight: 600, margin: '0.2rem 0' }}>Client: {b.clientName}</p>
              <p style={{ fontSize: '0.9rem', color: '#2C2C2C', marginBottom: '0.5rem' }}>Email: {b.clientEmail}</p>

              <div style={{ backgroundColor: '#FAF7F2', padding: '0.75rem', borderRadius: '6px', marginBottom: '1rem', fontSize: '0.9rem' }}>
                <p style={{ margin: '0 0 0.2rem' }}>📅 <strong>Date:</strong> {b.eventDate} at {b.eventTime}</p>
                <p style={{ margin: '0 0 0.2rem' }}>📍 <strong>Venue:</strong> {b.location}</p>
                {b.requirements && <p style={{ margin: 0, fontStyle: 'italic', color: '#3D3D3D' }}>"{b.requirements}"</p>}
              </div>
            </div>

            <div style={{ borderTop: '1px solid #E2DACB', paddingTop: '1rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              {b.status === 'Pending' && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button onClick={() => handleAction(b.id, 'Confirmed')} className="btn btn-primary btn-sm" style={{ flex: 1 }}>
                    <Check size={14} /> Accept &amp; Invoice
                  </button>
                  <button onClick={() => handleAction(b.id, 'Cancelled')} className="btn btn-outline btn-sm" style={{ flex: 1, borderColor: '#922B21', color: '#922B21' }}>
                    <X size={14} /> Deny
                  </button>
                </div>
              )}

              {(b.status === 'Confirmed' || b.status === 'Rescheduled') && (
                <button onClick={() => handleAction(b.id, 'Completed')} className="btn btn-black btn-sm btn-full">
                  <CheckCircle2 size={14} /> Mark Completed
                </button>
              )}

              <button onClick={() => openManageModal(b)} className="btn btn-secondary btn-sm btn-full">
                <RefreshCw size={14} /> Reschedule / Edit Details
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Reschedule / Manage Detail Modal */}
      <Modal isOpen={!!selectedBooking} onClose={() => setSelectedBooking(null)} title={`Reschedule Booking: ${selectedBooking?.id}`}>
        {selectedBooking && (
          <div>
            <div style={{ marginBottom: '1.25rem' }}>
              <h4>{selectedBooking.serviceName}</h4>
              <p>Client: <strong>{selectedBooking.clientName}</strong> ({selectedBooking.clientEmail})</p>
              <p>Current Status: <StatusBadge status={selectedBooking.status} /></p>
            </div>

            <div style={{ backgroundColor: '#FAF7F2', padding: '1.25rem', borderRadius: '8px', marginBottom: '1rem' }}>
              <h5 style={{ marginBottom: '0.75rem' }}>Update Date, Time &amp; Venue</h5>
              <div className="form-group">
                <label className="form-label">New Event Date</label>
                <input
                  type="date"
                  className="form-control"
                  value={rescheduleDate}
                  onChange={e => setRescheduleDate(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label className="form-label">New Event Time</label>
                <input
                  type="time"
                  className="form-control"
                  value={rescheduleTime}
                  onChange={e => setRescheduleTime(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Venue / Location</label>
                <input
                  type="text"
                  className="form-control"
                  value={rescheduleLocation}
                  onChange={e => setRescheduleLocation(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Notes / Requirements</label>
                <textarea
                  className="form-control"
                  value={rescheduleNotes}
                  onChange={e => setRescheduleNotes(e.target.value)}
                />
              </div>

              <button
                onClick={handleRescheduleSave}
                className="btn btn-primary btn-full"
              >
                <RefreshCw size={16} /> Save Rescheduled Date, Time &amp; Venue
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
