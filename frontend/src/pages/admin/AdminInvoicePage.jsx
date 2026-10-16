import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Modal } from '../../components/common/Modal';
import { Plus, FileText, CheckCircle, DollarSign } from 'lucide-react';

export const AdminInvoicePage = () => {
  const { invoices, addInvoice, updateInvoiceStatus, bookings } = useAuth();
  const [isOpen, setIsOpen] = useState(false);
  const [bookingId, setBookingId] = useState(bookings[0]?.id || 'BK-1001');
  const [amount, setAmount] = useState(2500);
  const [dueDate, setDueDate] = useState('2026-10-01');

  const handleGenerateInvoice = (e) => {
    e.preventDefault();
    const b = bookings.find(item => item.id === bookingId);
    if (!b) return;

    addInvoice({
      bookingId: b.id,
      clientName: b.clientName,
      clientEmail: b.clientEmail,
      serviceName: b.serviceName,
      amount: Number(amount),
      dueDate
    });

    setIsOpen(false);
  };

  return (
    <div className="page-wrapper container">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2.5rem' }}>
        <div>
          <h1 style={{ fontSize: '2.25rem', marginBottom: '0.35rem' }}>Invoice & Financial Management</h1>
          <p style={{ color: '#666', margin: 0 }}>FR14, FR16, FR17 - Track invoices & payment statuses</p>
        </div>
        <button onClick={() => setIsOpen(true)} className="btn btn-primary">
          <Plus size={18} /> Generate New Invoice
        </button>
      </div>

      <div className="card-grid">
        {invoices.map(inv => (
          <div key={inv.id} className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                <span style={{ fontWeight: 800, color: '#4A3525' }}>{inv.id}</span>
                <StatusBadge status={inv.status} />
              </div>

              <h4 style={{ margin: '0 0 0.2rem' }}>{inv.clientName}</h4>
              <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '0.5rem' }}>{inv.serviceName} ({inv.bookingId})</p>

              <div style={{ fontSize: '1.75rem', fontWeight: 800, color: '#1A1A1A', marginBottom: '1rem' }}>
                R{inv.amount.toLocaleString()}
              </div>
            </div>

            <div style={{ borderTop: '1px solid #E2DACB', paddingTop: '0.75rem' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 700, display: 'block', marginBottom: '0.35rem' }}>Update Status:</label>
              <select
                className="form-control"
                style={{ padding: '0.4rem', fontSize: '0.85rem' }}
                value={inv.status}
                onChange={e => updateInvoiceStatus(inv.id, e.target.value)}
              >
                <option value="Unpaid">Unpaid</option>
                <option value="Partially Paid">Partially Paid</option>
                <option value="Paid">Paid</option>
                <option value="Overdue">Overdue</option>
              </select>
            </div>
          </div>
        ))}
      </div>

      {/* Invoice Generator Modal */}
      <Modal isOpen={isOpen} onClose={() => setIsOpen(false)} title="Generate Client Invoice">
        <form onSubmit={handleGenerateInvoice}>
          <div className="form-group">
            <label className="form-label">Select Client Booking *</label>
            <select className="form-control" value={bookingId} onChange={e => setBookingId(e.target.value)}>
              {bookings.map(b => (
                <option key={b.id} value={b.id}>
                  {b.id} - {b.clientName} ({b.serviceName})
                </option>
              ))}
            </select>
          </div>

          <div className="form-group">
            <label className="form-label">Invoice Amount (ZAR) *</label>
            <input type="number" className="form-control" value={amount} onChange={e => setAmount(e.target.value)} required />
          </div>

          <div className="form-group">
            <label className="form-label">Due Date *</label>
            <input type="date" className="form-control" value={dueDate} onChange={e => setDueDate(e.target.value)} required />
          </div>

          <button type="submit" className="btn btn-black btn-full">
            <FileText size={16} /> Generate Invoice
          </button>
        </form>
      </Modal>
    </div>
  );
};
