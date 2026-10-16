import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { Modal } from '../../components/common/Modal';
import { Eye } from 'lucide-react';

export const PortfolioPage = () => {
  const { portfolio } = useAuth();
  const [selectedCategory, setSelectedCategory] = useState('All');
  const [activePhoto, setActivePhoto] = useState(null);

  const categories = ['All', 'Wedding', 'Birthday', 'Work & Art', 'Family'];

  const publicPortfolio = portfolio.filter(item => item.isPublic || item.isPublic === undefined);

  const filteredItems = selectedCategory === 'All'
    ? publicPortfolio
    : publicPortfolio.filter(item => item.category === selectedCategory);

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Main / Portfolio" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.5rem' }}>Portfolio</h1>
        <p style={{ color: '#666' }}>explore memories</p>
      </div>

      {/* Category Tabs matching Wireframe 1 */}
      <div className="tabs-header" style={{ justifyContent: 'center' }}>
        {categories.map(cat => (
          <button
            key={cat}
            className={`tab-btn ${selectedCategory === cat ? 'active' : ''}`}
            onClick={() => setSelectedCategory(cat)}
          >
            {cat}
          </button>
        ))}
      </div>

      {/* Photo Grid matching Wireframe 1 */}
      <div className="card-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))' }}>
        {filteredItems.map(item => (
          <div
            key={item.id}
            className="card"
            style={{ padding: 0, overflow: 'hidden', cursor: 'pointer' }}
            onClick={() => setActivePhoto(item)}
          >
            <div className="wireframe-box" style={{ height: '260px' }}>
              <img src={item.imageUrl} alt={item.title} />
              <div style={{
                position: 'absolute',
                top: 0, left: 0, right: 0, bottom: 0,
                background: 'rgba(0,0,0,0.2)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                opacity: 0,
                transition: 'opacity 0.25s'
              }}
              onMouseEnter={e => e.currentTarget.style.opacity = 1}
              onMouseLeave={e => e.currentTarget.style.opacity = 0}
              >
                <span className="btn btn-black btn-sm"><Eye size={16} /> View Image</span>
              </div>
            </div>
            <div style={{ padding: '1rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <h4 style={{ margin: 0 }}>{item.title}</h4>
              <span className="badge" style={{ backgroundColor: '#EAE3D2', color: '#4A3525' }}>
                {item.category}
              </span>
            </div>
          </div>
        ))}
      </div>

      {/* Lightbox Modal */}
      <Modal isOpen={!!activePhoto} onClose={() => setActivePhoto(null)} title={activePhoto?.title || 'Portfolio Item'}>
        {activePhoto && (
          <div>
            <div style={{ borderRadius: '8px', overflow: 'hidden', marginBottom: '1rem' }}>
              <img src={activePhoto.imageUrl} alt={activePhoto.title} style={{ width: '100%', maxHeight: '500px', objectFit: 'cover' }} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span className="badge" style={{ backgroundColor: '#4A3525', color: '#FFF' }}>
                {activePhoto.category}
              </span>
              <p style={{ margin: 0, color: '#666', fontSize: '0.9rem' }}>Public Showcase Gallery</p>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
