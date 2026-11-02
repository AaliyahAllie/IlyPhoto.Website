import React, { useState, useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { Calendar, CheckCircle, AlertTriangle } from 'lucide-react';

export const ClientBookingPage = () => {
  const { services, bookings, cloudLoaded, refreshBookingsFromCloud, addBooking, currentUser } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const initialService = location.state?.selectedService || services[0]?.name || 'Matric Ball';

  const [serviceName, setServiceName] = useState(initialService);
  const [eventDate, setEventDate] = useState('2026-11-15');
  const [eventTime, setEventTime] = useState('14:00');
  const [locationAddress, setLocationAddress] = useState('Strandfontein Pavilion, Cape Town');
  const [requirements, setRequirements] = useState('');

  const [isAvailable, setIsAvailable] = useState(true);
  const [conflictMessage, setConflictMessage] = useState('');
  const [submitError, setSubmitError] = useState('');
  const [submitted, setSubmitted] = useState(false);

  // Ensure cloud bookings are loaded before running availability checks
  useEffect(() => {
    refreshBookingsFromCloud();
  }, [refreshBookingsFromCloud]);

  // Check Availability FR08 only after cloud data has loaded
  useEffect(() => {
    if (!cloudLoaded) return;

    const conflict = bookings.find(b =>
      b.eventDate === eventDate &&
      b.status !== 'Cancelled' &&
      b.status !== 'Denied'
    );

    if (conflict) {
      setIsAvailable(false);
      setConflictMessage(`The photographer already has a scheduled appointment (${conflict.serviceName}) on ${eventDate}. Please pick another date.`);
    } else {
      setIsAvailable(true);
      setConflictMessage('');
    }
  }, [eventDate, eventTime, bookings, cloudLoaded]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSubmitError('');
    if (!cloudLoaded || !isAvailable) return;

    try {
      await addBooking({
        clientName: currentUser?.name || 'Sarah Jenkins',
        clientEmail: currentUser?.email || 'sarah.j@example.com',
        serviceName,
        eventDate,
        eventTime,
        location: locationAddress,
        requirements
      });

      setSubmitted(true);
      setTimeout(() => {
        navigate('/client/dashboard');
      }, 2000);
    } catch (err) {
      setSubmitError(err.message || 'Could not create booking. The date may already be booked.');
    }
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '580px' }}>
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Book A Session</h1>
        <p style={{ color: '#2C2C2C' }}>Real-time Azure cloud availability check</p>
      </div>

      {submitted ? (
        <div className="card" style={{ textAlign: 'center', padding: '3rem 2rem', backgroundColor: '#E8F8F5', borderColor: '#0E6251' }}>
          <CheckCircle size={54} color="#0E6251" style={{ marginBottom: '1rem' }} />
          <h2 style={{ color: '#0E6251', marginBottom: '0.5rem' }}>Booking Requested!</h2>
          <p style={{ color: '#1A1A1A', fontSize: '0.95rem' }}>
            Your booking request for <strong>{serviceName}</strong> on <strong>{eventDate}</strong> at <strong>{locationAddress}</strong> has been saved to Azure Table Storage.
          </p>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="card" style={{ padding: '2.5rem' }}>
          {submitError && (
            <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.92rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <AlertTriangle size={20} />
              <span>{submitError}</span>
            </div>
          )}

          <div className="form-group">
            <label className="form-label">Select Service / Package *</label>
            <select
              className="form-control"
              value={serviceName}
              onChange={e => setServiceName(e.target.value)}
              required
            >
              {services.map(s => (
                <option key={s.id} value={s.name}>
                  {s.name} - R{s.price} ({s.duration})
                </option>
              ))}
            </select>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
            <div className="form-group">
              <label className="form-label">Preferred Date *</label>
              <input
                type="date"
                className="form-control"
                value={eventDate}
                onChange={e => setEventDate(e.target.value)}
                required
              />
            </div>

            <div className="form-group">
              <label className="form-label">Preferred Time *</label>
              <input
                type="time"
                className="form-control"
                value={eventTime}
                onChange={e => setEventTime(e.target.value)}
                required
              />
            </div>
          </div>

          {!cloudLoaded ? (
            <div style={{ padding: '0.75rem', backgroundColor: '#FEF9E7', border: '1px solid #F9E79F', borderRadius: '6px', color: '#7D6608', fontSize: '0.9rem', fontWeight: 600, marginBottom: '1.25rem' }}>
              Syncing live calendar availability from Azure Table Storage...
            </div>
          ) : !isAvailable ? (
            <div style={{ padding: '0.85rem', backgroundColor: '#FDEDEC', border: '1px solid #922B21', borderRadius: '6px', color: '#922B21', fontSize: '0.9rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <AlertTriangle size={20} />
              <span>{conflictMessage}</span>
            </div>
          ) : (
            <div style={{ padding: '0.65rem 0.85rem', backgroundColor: '#E8F8F5', border: '1px solid #0E6251', borderRadius: '6px', color: '#0E6251', fontSize: '0.9rem', fontWeight: 600, marginBottom: '1.25rem', display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
              <CheckCircle size={16} />
              <span>Photographer is available on this date &amp; time!</span>
            </div>
          )}

          <div className="form-group">
            <label className="form-label">Event Location / Venue *</label>
            <input
              type="text"
              className="form-control"
              placeholder="Address or venue name"
              value={locationAddress}
              onChange={e => setLocationAddress(e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label className="form-label">Special Requirements / Notes</label>
            <textarea
              className="form-control"
              placeholder="Lighting preferences, outfit counts, family members included..."
              value={requirements}
              onChange={e => setRequirements(e.target.value)}
            ></textarea>
          </div>

          <button
            type="submit"
            className="btn btn-black btn-full"
            disabled={!cloudLoaded || !isAvailable}
            style={{ opacity: (!cloudLoaded || !isAvailable) ? 0.5 : 1 }}
          >
            <Calendar size={18} /> Submit Appointment Request
          </button>
        </form>
      )}
    </div>
  );
};
