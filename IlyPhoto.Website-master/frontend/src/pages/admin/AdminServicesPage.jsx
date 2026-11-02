import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { Modal } from '../../components/common/Modal';
import { Plus, Edit2, CheckCircle2 } from 'lucide-react';

export const AdminServicesPage = () => {
  const { services, setServices } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const [name, setName] = useState('');
  const [category, setCategory] = useState('Wedding');
  const [price, setPrice] = useState(1500);
  const [duration, setDuration] = useState('2 Hours');
  const [description, setDescription] = useState('');

  const handleAddService = (e) => {
    e.preventDefault();
    if (!name || !price) return;

    const newSrv = {
      id: Date.now(),
      name,
      category,
      price: Number(price),
      duration,
      description,
      features: [`${duration} Session`, "High-Res Edited Deliverables", "Private Client Gallery"]
    };

    setServices(prev => [...prev, newSrv]);
    setIsOpen(false);
    setName('');
    setDescription('');
  };

  return (
    <div className="page-wrapper container">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2.5rem' }}>
        <div>
          <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Photography Service Management</h1>
          <p style={{ color: '#666', margin: 0 }}>FR21 - Manage packages and pricing</p>
        </div>
        <button onClick={() => setIsOpen(true)} className="btn btn-primary">
          <Plus size={18} /> Add New Package
        </button>
      </div>

      <div className="card-grid">
        {services.map(srv => (
          <div key={srv.id} className="card">
            <span className="badge" style={{ backgroundColor: '#EAE3D2', color: '#4A3525', marginBottom: '0.75rem' }}>
              {srv.duration}
            </span>
            <h3 style={{ fontSize: '1.25rem', marginBottom: '0.25rem' }}>{srv.name}</h3>
            <p style={{ color: '#666', fontSize: '0.88rem', marginBottom: '1rem' }}>{srv.description}</p>
            <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#4A3525', marginBottom: '1rem' }}>
              R{srv.price.toLocaleString()}
            </div>
          </div>
        ))}
      </div>

      <Modal isOpen={isOpen} onClose={() => setIsOpen(false)} title="Create New Package / Service">
        <form onSubmit={handleAddService}>
          <div className="form-group">
            <label className="form-label">Package Name *</label>
            <input type="text" className="form-control" placeholder="e.g. VIP Wedding Deluxe" value={name} onChange={e => setName(e.target.value)} required />
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
            <div className="form-group">
              <label className="form-label">Price (ZAR) *</label>
              <input type="number" className="form-control" value={price} onChange={e => setPrice(e.target.value)} required />
            </div>

            <div className="form-group">
              <label className="form-label">Duration</label>
              <input type="text" className="form-control" placeholder="3 Hours" value={duration} onChange={e => setDuration(e.target.value)} required />
            </div>
          </div>

          <div className="form-group">
            <label className="form-label">Description</label>
            <textarea className="form-control" value={description} onChange={e => setDescription(e.target.value)}></textarea>
          </div>

          <button type="submit" className="btn btn-black btn-full">
            <Plus size={16} /> Save Package
          </button>
        </form>
      </Modal>
    </div>
  );
};
