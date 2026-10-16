import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { RatingStars } from '../../components/common/RatingStars';
import { Star, Send, CheckCircle } from 'lucide-react';

export const ClientReviewsPage = () => {
  const { reviews, addReview, bookings, currentUser } = useAuth();
  const [headline, setHeadline] = useState('');
  const [service, setService] = useState('Pre-Wedding');
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState('');
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!comment) return;

    addReview({
      clientName: headline || 'Outstanding Experience',
      service,
      rating,
      comment
    });

    setSubmitted(true);
    setHeadline('');
    setComment('');
    setTimeout(() => setSubmitted(false), 3000);
  };

  return (
    <div className="page-wrapper container" style={{ maxWidth: '650px' }}>
      {/* Wireframe Header "Client / Reviews" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.5rem' }}>Reviews</h1>
        <p style={{ color: '#666' }}>Feedback*</p>
      </div>

      {submitted && (
        <div className="card" style={{ textAlign: 'center', padding: '1.5rem', backgroundColor: '#E8F5E9', marginBottom: '2rem' }}>
          <CheckCircle size={32} color="#2D6A4F" style={{ marginBottom: '0.5rem' }} />
          <h4 style={{ color: '#2D6A4F', margin: 0 }}>Review Submitted Successfully!</h4>
          <p style={{ fontSize: '0.85rem', color: '#333', margin: 0 }}>Thank you for helping Ily Photo improve our services.</p>
        </div>
      )}

      <form onSubmit={handleSubmit} className="card" style={{ padding: '2.5rem', marginBottom: '3rem' }}>
        <h3 style={{ marginBottom: '1.25rem' }}>Leave a Review</h3>

        <div className="form-group">
          <label className="form-label">Rating</label>
          <RatingStars rating={rating} interactive={true} onRatingChange={setRating} />
        </div>

        <div className="form-group">
          <label className="form-label">Review Title / Headline</label>
          <input
            type="text"
            className="form-control"
            placeholder="e.g. Amazing photography experience!"
            value={headline}
            onChange={e => setHeadline(e.target.value)}
          />
        </div>

        <div className="form-group">
          <label className="form-label">Photography Service</label>
          <select className="form-control" value={service} onChange={e => setService(e.target.value)}>
            <option value="Pre-Wedding">Pre-Wedding Shoot</option>
            <option value="Standard Shoot">Standard Shoot</option>
            <option value="Full Event">Full Event Photography</option>
          </select>
        </div>

        <div className="form-group">
          <label className="form-label">Feedback & Experience *</label>
          <textarea
            className="form-control"
            placeholder="Describe your session experience, photographer's professionalism, and photo quality..."
            value={comment}
            onChange={e => setComment(e.target.value)}
            required
          ></textarea>
        </div>

        <button type="submit" className="btn btn-black btn-full">
          <Send size={18} /> Submit Review
        </button>
      </form>
    </div>
  );
};
