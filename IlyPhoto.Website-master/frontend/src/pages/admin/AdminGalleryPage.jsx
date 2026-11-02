import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { Modal } from '../../components/common/Modal';
import { Upload, Plus, Trash2, ShieldCheck, Lock, Image as ImageIcon } from 'lucide-react';

export const AdminGalleryPage = () => {
  const { portfolio, addPublicPortfolioItem, deletePortfolioItem, clientGalleries, addPrivateClientPhoto, deleteClientPhoto, bookings } = useAuth();
  const [isUploadOpen, setIsUploadOpen] = useState(false);
  const [targetType, setTargetType] = useState('private'); // 'public' or 'private'
  
  const [title, setTitle] = useState('');
  const [imageUrl, setImageUrl] = useState('');
  const [category, setCategory] = useState('Wedding');
  const [selectedBookingId, setSelectedBookingId] = useState(bookings[0]?.id || 'BK-1001');

  const handleUpload = (e) => {
    e.preventDefault();
    if (!title || !imageUrl) return;

    if (targetType === 'public') {
      addPublicPortfolioItem({
        title,
        category,
        imageUrl,
        featured: true
      });
    } else {
      const selectedBooking = bookings.find(b => b.id === selectedBookingId);
      addPrivateClientPhoto({
        bookingId: selectedBookingId,
        clientEmail: selectedBooking ? selectedBooking.clientEmail : 'sarah.j@example.com',
        title,
        imageUrl
      });
    }

    setIsUploadOpen(false);
    setTitle('');
    setImageUrl('');
  };

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Admin / Gallery" */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2.5rem' }}>
        <div>
          <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Gallery Management</h1>
          <p style={{ color: '#666', margin: 0 }}>Pick and View / Upload Deliverables</p>
        </div>
        <button onClick={() => setIsUploadOpen(true)} className="btn btn-primary">
          <Upload size={18} /> Upload Gallery
        </button>
      </div>

      {/* Upload Modal matching Wireframe 2 "Upload Gallery" */}
      <Modal isOpen={isUploadOpen} onClose={() => setIsUploadOpen(false)} title="Upload Gallery Photo">
        <form onSubmit={handleUpload}>
          <div className="form-group">
            <label className="form-label">Upload Destination *</label>
            <select
              className="form-control"
              value={targetType}
              onChange={e => setTargetType(e.target.value)}
            >
              <option value="private">🔒 Private Client Gallery (Linked to Client Booking ID - POPIA Secure)</option>
              <option value="public">🌐 Public Portfolio Showcase (Visible to all website visitors)</option>
            </select>
          </div>

          {targetType === 'private' && (
            <div className="form-group">
              <label className="form-label">Link to Client Booking ID *</label>
              <select
                className="form-control"
                value={selectedBookingId}
                onChange={e => setSelectedBookingId(e.target.value)}
              >
                {bookings.map(b => (
                  <option key={b.id} value={b.id}>
                    {b.id} - {b.clientName} ({b.serviceName}) [{b.clientEmail}]
                  </option>
                ))}
              </select>
            </div>
          )}

          <div className="form-group">
            <label className="form-label">Photo Title *</label>
            <input
              type="text"
              className="form-control"
              placeholder="e.g. Sunset Ceremony Kiss"
              value={title}
              onChange={e => setTitle(e.target.value)}
              required
            />
          </div>

          {targetType === 'public' && (
            <div className="form-group">
              <label className="form-label">Portfolio Category</label>
              <select className="form-control" value={category} onChange={e => setCategory(e.target.value)}>
                <option value="Wedding">Wedding</option>
                <option value="Birthday">Birthday</option>
                <option value="Work & Art">Work & Art</option>
                <option value="Family">Family</option>
              </select>
            </div>
          )}

          <div className="form-group">
            <label className="form-label">Image URL / Asset Path *</label>
            <input
              type="url"
              className="form-control"
              placeholder="https://images.unsplash.com/photo-..."
              value={imageUrl}
              onChange={e => setImageUrl(e.target.value)}
              required
            />
          </div>

          <button type="submit" className="btn btn-black btn-full">
            <Upload size={18} /> Confirm Upload
          </button>
        </form>
      </Modal>

      {/* Private Client Galleries Section */}
      <section style={{ marginBottom: '3rem' }}>
        <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <Lock size={20} color="#4A3525" /> Client Private Deliverable Galleries ({clientGalleries.length})
        </h3>
        <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
          {clientGalleries.map(photo => (
            <div key={photo.id} className="card" style={{ padding: 0, overflow: 'hidden' }}>
              <div className="wireframe-box" style={{ height: '220px' }}>
                <img src={photo.imageUrl} alt={photo.title} />
              </div>
              <div style={{ padding: '1rem' }}>
                <h4 style={{ margin: '0 0 0.25rem' }}>{photo.title}</h4>
                <p style={{ margin: 0, fontSize: '0.82rem', color: '#4A3525', fontWeight: 600 }}>Ref: {photo.bookingId}</p>
                <p style={{ margin: '0 0 0.75rem', fontSize: '0.8rem', color: '#666' }}>Client: {photo.clientEmail}</p>

                <button onClick={() => deleteClientPhoto(photo.id)} className="btn btn-outline btn-sm btn-full" style={{ borderColor: '#991B1B', color: '#991B1B' }}>
                  <Trash2 size={14} /> Remove Photo
                </button>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Public Portfolio Grid */}
      <section>
        <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <ImageIcon size={20} color="#4A3525" /> Public Portfolio Photos ({portfolio.length})
        </h3>
        <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
          {portfolio.map(photo => (
            <div key={photo.id} className="card" style={{ padding: 0, overflow: 'hidden' }}>
              <div className="wireframe-box" style={{ height: '220px' }}>
                <img src={photo.imageUrl} alt={photo.title} />
              </div>
              <div style={{ padding: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <h4 style={{ margin: 0, fontSize: '0.95rem' }}>{photo.title}</h4>
                  <span className="badge" style={{ backgroundColor: '#EAE3D2', color: '#4A3525' }}>{photo.category}</span>
                </div>
                <button onClick={() => deletePortfolioItem(photo.id)} className="btn btn-outline btn-sm" style={{ borderColor: '#991B1B', color: '#991B1B' }}>
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};
