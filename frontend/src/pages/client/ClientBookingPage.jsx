import React, { useState, useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { Calendar, Clock, MapPin, CheckCircle, AlertTriangle } from 'lucide-react';

export const ClientBookingPage = () => {
  const { services, bookings, addBooking, currentUser } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const initialService = location.state?.selectedService || services[0]?.name || 'Pre-Wedding';

  const [serviceName, setServiceName] = useState(initialService);
  const [eventDate, setEventDate] = useState('2026-10-15');
  const [eventTime, setEventTime] = useState('14:00');
  const [locationAddress, setLocationAddress] = useState('Kirstenbosch Gardens, Cape Town');
  const [requirements, setRequirements] = useState('');
  
  const [isAvailable, setIsAvailable] = useState(true);
  const [conflictMessage, setConflictMessage] = useState('');
  const [submitted, setSubmitted] = useState(false);

  // Check Availability FR08
  useEffect(() => {
    const conflict = bookings.find(b => 
      b.eventDate === eventDate && 
      b.eventTime === eventTime && 
      b.status !== 'Cancelled'
    );

    if (conflict) {
      setIsAvailable(false);
      setConflictMessage(`The photographer already has a scheduled appointment (${conflict.serviceName}) on ${eventDate} at ${eventTime}. Please pick another time slot.`);
    } else {
      setIsAvailable(true);
      setConflictMessage('');
    }
  }, [eventDate, eventTime, bookings]);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!isAvailable) return;

    addBooking({
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
    }, 2500);
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '580px' }}>
      {/* Wireframe Header "Client / Booking" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Booking</h1>
        <p style={{ color: '#666' }}>Choose a service</p>
      </div>

      {submitted ? (
        <div className="card" style={{ textAlign: 'center', padding: '3rem 2rem', backgroundColor: '#E8F5E9', borderColor: '#2D6A4F' }}>
          <CheckCircle size={54} color="#2D6A4F" style={{ marginBottom: '1rem' }} />
          <h2 style={{ color: '#2D6A4F', marginBottom: '0.5rem' }}>Booking Requested!</h2>
          <p style={{ color: '#333', fontSize: '0.95rem' }}>
            Your booking request for <strong>{serviceName}</strong> on <strong>{eventDate}</strong> has been submitted.
            The photographer will review and notify you shortly.
          </p>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="card" style={{ padding: '2.5rem' }}>
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

          {/* Real-time FR08 Availability Status Alert */}
          {!isAvailable ? (
            <div style={{ padding: '0.85rem', backgroundColor: '#FEE2E2', border: '1px solid #991B1B', borderRadius: '6px', color: '#991B1B', fontSize: '0.88rem', marginBottom: '1.25rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <AlertTriangle size={20} flexShrink={0} />
              <span>{conflictMessage}</span>
            </div>
          ) : (
            <div style={{ padding: '0.65rem 0.85rem', backgroundColor: '#E8F5E9', border: '1px solid #2D6A4F', borderRadius: '6px', color: '#2D6A4F', fontSize: '0.85rem', marginBottom: '1.25rem', display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
              <CheckCircle size={16} />
              <span>Photographer is available on this date & time!</span>
            </div>
          )}

          <div className="form-group">
            <label className="form-label">Event Location *</label>
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
            disabled={!isAvailable}
            style={{ opacity: !isAvailable ? 0.5 : 1 }}
          >
            <Calendar size={18} /> Submit Appointment Request
          </button>
        </form>
      )}
    </div>
  );
};
