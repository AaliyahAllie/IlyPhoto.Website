import React, { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { StatusBadge } from '../../components/common/StatusBadge';
import { Modal } from '../../components/common/Modal';
import { FileText, Download, Printer, DollarSign } from 'lucide-react';

export const ClientInvoicePage = () => {
  const { invoices, currentUser } = useAuth();
  const [selectedInvoice, setSelectedInvoice] = useState(null);

  const clientEmail = currentUser?.email || 'sarah.j@example.com';
  const myInvoices = invoices.filter(inv => inv.clientEmail === clientEmail);

  return (
    <div className="page-wrapper container">
      {/* Wireframe Header "Client / Gallery - Pick and View Invoice" */}
      <div style={{ textAlign: 'center', marginBottom: '2.5rem' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.5rem' }}>Invoices</h1>
        <p style={{ color: '#666' }}>Pick and View</p>
      </div>

      <div className="card-grid">
        {myInvoices.map(inv => (
          <div key={inv.id} className="card" style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
            <div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
                <span style={{ fontWeight: 800, color: '#4A3525' }}>{inv.id}</span>
                <StatusBadge status={inv.status} />
              </div>

              <h3 style={{ fontSize: '1.2rem', marginBottom: '0.35rem' }}>{inv.serviceName}</h3>
              <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '0.2rem' }}>Booking Ref: {inv.bookingId}</p>
              <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '0.75rem' }}>Due Date: {inv.dueDate}</p>

              <div style={{ fontSize: '1.5rem', fontWeight: 800, color: '#1A1A1A', marginTop: '0.5rem' }}>
                R{inv.amount.toLocaleString()}
              </div>
            </div>

            <button
              onClick={() => setSelectedInvoice(inv)}
              className="btn btn-outline btn-full"
              style={{ marginTop: '1.25rem' }}
            >
              <FileText size={16} /> View & Download Statement
            </button>
          </div>
        ))}
      </div>

      {/* Invoice Detail Printable Modal matching Wireframe 3 */}
      <Modal isOpen={!!selectedInvoice} onClose={() => setSelectedInvoice(null)} title={`Tax Invoice: ${selectedInvoice?.id}`}>
        {selectedInvoice && (
          <div style={{ padding: '1rem 0' }}>
            <div style={{ borderBottom: '2px solid #4A3525', paddingBottom: '1rem', marginBottom: '1.5rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <h2 style={{ color: '#4A3525', margin: 0 }}>ILY PHOTO</h2>
                <p style={{ margin: 0, fontSize: '0.82rem', color: '#666' }}>VAT Reg: 4920192019 | Cape Town, South Africa</p>
              </div>
              <div style={{ textAlign: 'right' }}>
                <StatusBadge status={selectedInvoice.status} />
                <p style={{ margin: '0.35rem 0 0', fontWeight: 700 }}>{selectedInvoice.id}</p>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1.5rem', fontSize: '0.9rem' }}>
              <div>
                <strong>Billed To:</strong>
                <p style={{ margin: 0 }}>{selectedInvoice.clientName}</p>
                <p style={{ margin: 0, color: '#666' }}>{selectedInvoice.clientEmail}</p>
              </div>
              <div>
                <strong>Invoice Dates:</strong>
                <p style={{ margin: 0 }}>Issued: {selectedInvoice.issuedDate}</p>
                <p style={{ margin: 0 }}>Due: {selectedInvoice.dueDate}</p>
              </div>
            </div>

            <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '1.5rem', fontSize: '0.9rem' }}>
              <thead>
                <tr style={{ backgroundColor: '#EAE3D2', borderBottom: '1px solid #DFD7C6', textAlign: 'left' }}>
                  <th style={{ padding: '0.6rem 0.8rem' }}>Description</th>
                  <th style={{ padding: '0.6rem 0.8rem', textAlign: 'right' }}>Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td style={{ padding: '0.8rem', borderBottom: '1px solid #E2DACB' }}>
                    {selectedInvoice.serviceName} ({selectedInvoice.bookingId})
                  </td>
                  <td style={{ padding: '0.8rem', borderBottom: '1px solid #E2DACB', textAlign: 'right', fontWeight: 700 }}>
                    R{selectedInvoice.amount.toLocaleString()}
                  </td>
                </tr>
              </tbody>
            </table>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1.5rem' }}>
              <button onClick={() => window.print()} className="btn btn-secondary btn-sm">
                <Printer size={16} /> Print Statement
              </button>
              <a href={`data:text/plain;charset=utf-8,Invoice ${selectedInvoice.id} for ${selectedInvoice.clientName}`} download={`${selectedInvoice.id}.txt`} className="btn btn-primary btn-sm">
                <Download size={16} /> Download Copy
              </a>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
};
