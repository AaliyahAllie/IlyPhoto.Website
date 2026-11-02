import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { RatingStars } from '../../components/common/RatingStars';
import { Modal } from '../../components/common/Modal';
import { MessageSquare, Send } from 'lucide-react';

export const AdminReviewsPage = () => {
  const { reviews, respondToReview } = useAuth();
  const [activeReview, setActiveReview] = useState(null);
  const [responseText, setResponseText] = useState('');

  const handleRespond = (e) => {
    e.preventDefault();
    if (activeReview && responseText) {
      respondToReview(activeReview.id, responseText);
      setActiveReview(null);
      setResponseText('');
    }
  };

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Admin / Reviews" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Reviews</h1>
        <p style={{ color: '#666' }}>Under border track view</p>
      </div>

      <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))' }}>
        {reviews.map(rev => (
          <div key={rev.id} className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                <RatingStars rating={rev.rating} />
                <span style={{ fontSize: '0.78rem', color: '#666' }}>{rev.date}</span>
              </div>

              <h4 style={{ fontSize: '1.1rem', marginBottom: '0.25rem' }}>"{rev.clientName}"</h4>
              <p style={{ fontSize: '0.85rem', fontWeight: 600, color: '#4A3525', marginBottom: '0.75rem' }}>
                Author: {rev.author} ({rev.service})
              </p>

              <p style={{ fontSize: '0.9rem', color: '#333', marginBottom: '1rem', fontStyle: 'italic' }}>
                "{rev.comment}"
              </p>

              {rev.adminResponse ? (
                <div style={{ backgroundColor: '#FAF7F2', padding: '0.75rem 1rem', borderRadius: '6px', borderLeft: '3px solid #4A3525', fontSize: '0.85rem' }}>
                  <strong>Admin Response:</strong>
                  <p style={{ margin: 0, color: '#333' }}>{rev.adminResponse}</p>
                </div>
              ) : (
                <span className="badge badge-pending">Awaiting Admin Response</span>
              )}
            </div>

            <button
              onClick={() => {
                setActiveReview(rev);
                setResponseText(rev.adminResponse || '');
              }}
              className="btn btn-black btn-sm btn-full"
              style={{ marginTop: '1.25rem' }}
            >
              <MessageSquare size={14} /> {rev.adminResponse ? 'Edit Response' : 'Reply to Review'}
            </button>
          </div>
        ))}
      </div>

      {/* Response Modal */}
      <Modal isOpen={!!activeReview} onClose={() => setActiveReview(null)} title="Respond to Client Review">
        {activeReview && (
          <form onSubmit={handleRespond}>
            <div style={{ marginBottom: '1rem' }}>
              <RatingStars rating={activeReview.rating} />
              <h4 style={{ margin: '0.5rem 0 0.25rem' }}>{activeReview.clientName}</h4>
              <p style={{ fontSize: '0.85rem', color: '#666' }}>By {activeReview.author}</p>
              <p style={{ fontStyle: 'italic', fontSize: '0.9rem', backgroundColor: '#F4F1EA', padding: '0.75rem', borderRadius: '6px' }}>
                "{activeReview.comment}"
              </p>
            </div>

            <div className="form-group">
              <label className="form-label">Official Photographer Response *</label>
              <textarea
                className="form-control"
                placeholder="Write a polite response to this client review..."
                value={responseText}
                onChange={e => setResponseText(e.target.value)}
                required
              ></textarea>
            </div>

            <button type="submit" className="btn btn-primary btn-full">
              <Send size={16} /> Publish Response
            </button>
          </form>
        )}
      </Modal>
    </div>
  );
};
