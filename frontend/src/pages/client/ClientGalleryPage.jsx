import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { Modal } from '../../components/common/Modal';
import { Download, ShieldCheck, Lock, Image as ImageIcon } from 'lucide-react';

export const ClientGalleryPage = () => {
  const { currentUser, clientGalleries } = useAuth();
  const [selectedPhoto, setSelectedPhoto] = useState(null);

  const clientEmail = currentUser?.email || 'sarah.j@example.com';
  const clientName = currentUser?.name || 'Sarah Jenkins';

  // Strict user isolation (POPIA compliance)
  const myPrivatePhotos = clientGalleries.filter(g => g.clientEmail === clientEmail);

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Client / Gallery" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.35rem' }}>Gallery</h1>
        <p style={{ color: '#666' }}>Pick and View</p>
        <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', marginTop: '0.5rem', padding: '0.35rem 0.85rem', backgroundColor: '#EAE3D2', borderRadius: '999px', fontSize: '0.82rem', color: '#4A3525', fontWeight: 600 }}>
          <Lock size={14} /> Private Client Album ({clientName})
        </div>
      </div>

      {myPrivatePhotos.length > 0 ? (
        <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
          {myPrivatePhotos.map(photo => (
            <div key={photo.id} className="card" style={{ padding: 0, overflow: 'hidden' }}>
              <div className="wireframe-box" style={{ height: '240px', cursor: 'pointer' }} onClick={() => setSelectedPhoto(photo)}>
                <img src={photo.imageUrl} alt={photo.title} />
              </div>
              <div style={{ padding: '1rem' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                  <h4 style={{ margin: 0, fontSize: '1rem' }}>{photo.title}</h4>
                  <span className="badge" style={{ backgroundColor: '#EAE3D2', color: '#4A3525' }}>
                    {photo.bookingId}
                  </span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '0.75rem', fontSize: '0.8rem', color: '#666' }}>
                  <span>Uploaded: {photo.uploadDate}</span>
                  <a href={photo.imageUrl} target="_blank" rel="noreferrer" download className="btn btn-outline btn-sm">
                    <Download size={14} /> Download
                  </a>
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="card" style={{ padding: '3.5rem', textAlign: 'center', maxWidth: '600px', margin: '0 auto' }}>
          <ShieldCheck size={48} color="#4A3525" style={{ marginBottom: '1rem' }} />
          <h3 style={{ marginBottom: '0.5rem' }}>Private Gallery Secure & Protected</h3>
          <p style={{ color: '#666', lineHeight: '1.6', fontSize: '0.95rem' }}>
            In accordance with POPIA regulations, client photos are strictly restricted to your registered account (<strong>{clientEmail}</strong>).
          </p>
          <p style={{ color: '#8C6D53', marginTop: '1rem', fontSize: '0.9rem', fontWeight: 600 }}>
            No gallery photos uploaded yet for your bookings. Your photographer will upload your photos here upon completing your shoot session.
          </p>
        </div>
      )}

      {/* Lightbox Download Modal */}
      <Modal isOpen={!!selectedPhoto} onClose={() => setSelectedPhoto(null)} title={selectedPhoto?.title || 'Photo Detail'}>
        {selectedPhoto && (
          <div>
            <img src={selectedPhoto.imageUrl} alt={selectedPhoto.title} style={{ width: '100%', borderRadius: '8px', marginBottom: '1rem', maxHeight: '500px', objectFit: 'cover' }} />
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <p style={{ margin: 0, fontWeight: 700 }}>Booking Ref: {selectedPhoto.bookingId}</p>
                <p style={{ margin: 0, fontSize: '0.82rem', color: '#666' }}>Uploaded to account {selectedPhoto.clientEmail}</p>
              </div>
              <a href={selectedPhoto.imageUrl} target="_blank" rel="noreferrer" download className="btn btn-primary">
                <Download size={16} /> Download High-Res Original
              </a>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
