import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Modal } from '../../components/common/Modal';
import { Check, X, RefreshCw, Calendar as CalendarIcon, CheckCircle2, Clock } from 'lucide-react';

export const AdminBookingsPage = () => {
  const { bookings, updateBookingStatus } = useAuth();
  const [selectedBooking, setSelectedBooking] = useState(null);
  const [rescheduleDate, setRescheduleDate] = useState('');
  const [rescheduleTime, setRescheduleTime] = useState('');

  const handleAction = (bookingId, status) => {
    updateBookingStatus(bookingId, status);
    if (selectedBooking && selectedBooking.id === bookingId) {
      setSelectedBooking(prev => ({ ...prev, status }));
    }
  };

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Admin / Bookings Page" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Booking Management</h1>
        <p style={{ color: '#666' }}>Review and manage client appointments</p>
      </div>

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
              <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '0.5rem' }}>Email: {b.clientEmail}</p>

              <div style={{ backgroundColor: '#FAF7F2', padding: '0.75rem', borderRadius: '6px', marginBottom: '1rem', fontSize: '0.88rem' }}>
                <p style={{ margin: '0 0 0.2rem' }}>📅 <strong>Date:</strong> {b.eventDate} at {b.eventTime}</p>
                <p style={{ margin: '0 0 0.2rem' }}>📍 <strong>Location:</strong> {b.location}</p>
                {b.requirements && <p style={{ margin: 0, fontStyle: 'italic', color: '#666' }}>" {b.requirements} "</p>}
              </div>
            </div>

            {/* Quick Action Drawer matching Wireframe 2 */}
            <div style={{ borderTop: '1px solid #E2DACB', paddingTop: '1rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
              {b.status === 'Pending' && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <button onClick={() => handleAction(b.id, 'Confirmed')} className="btn btn-primary btn-sm" style={{ flex: 1 }}>
                    <Check size={14} /> Accept
                  </button>
                  <button onClick={() => handleAction(b.id, 'Cancelled')} className="btn btn-outline btn-sm" style={{ flex: 1, borderColor: '#991B1B', color: '#991B1B' }}>
                    <X size={14} /> Deny
                  </button>
                </div>
              )}

              {b.status === 'Confirmed' && (
                <button onClick={() => handleAction(b.id, 'Completed')} className="btn btn-black btn-sm btn-full">
                  <CheckCircle2 size={14} /> Mark Completed
                </button>
              )}

              <button onClick={() => setSelectedBooking(b)} className="btn btn-secondary btn-sm btn-full">
                <RefreshCw size={14} /> Reschedule / Manage Detail
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* Reschedule / Manage Detail Modal */}
      <Modal isOpen={!!selectedBooking} onClose={() => setSelectedBooking(null)} title={`Booking Detail: ${selectedBooking?.id}`}>
        {selectedBooking && (
          <div>
            <div style={{ marginBottom: '1.5rem' }}>
              <h4>{selectedBooking.serviceName}</h4>
              <p>Client: <strong>{selectedBooking.clientName}</strong> ({selectedBooking.clientEmail})</p>
              <p>Current Status: <StatusBadge status={selectedBooking.status} /></p>
            </div>

            <div style={{ backgroundColor: '#FAF7F2', padding: '1.25rem', borderRadius: '8px', marginBottom: '1.5rem' }}>
              <h5 style={{ marginBottom: '0.75rem' }}>Reschedule Appointment</h5>
              <div className="form-group">
                <label className="form-label">New Event Date</label>
                <input
                  type="date"
                  className="form-control"
                  defaultValue={selectedBooking.eventDate}
                  onChange={e => setRescheduleDate(e.target.value)}
                />
              </div>

              <div className="form-group">
                <label className="form-label">New Event Time</label>
                <input
                  type="time"
                  className="form-control"
                  defaultValue={selectedBooking.eventTime}
                  onChange={e => setRescheduleTime(e.target.value)}
                />
              </div>

              <button
                onClick={() => {
                  handleAction(selectedBooking.id, 'Rescheduled');
                  setSelectedBooking(null);
                }}
                className="btn btn-primary btn-full"
              >
                <RefreshCw size={16} /> Confirm Reschedule Status
              </button>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
