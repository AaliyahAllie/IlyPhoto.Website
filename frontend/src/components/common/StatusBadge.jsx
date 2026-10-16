import React from 'react';
import { Clock, CheckCircle, AlertCircle, XCircle, RefreshCw, DollarSign } from 'lucide-react';

export const StatusBadge = ({ status }) => {
  const normalized = (status || '').toLowerCase().replace(/\s+/g, '');

  switch (normalized) {
    case 'pending':
      return <span className="badge badge-pending"><Clock size={12} /> Pending</span>;
    case 'confirmed':
      return <span className="badge badge-confirmed"><CheckCircle size={12} /> Confirmed</span>;
    case 'completed':
      return <span className="badge badge-completed"><CheckCircle size={12} /> Completed</span>;
    case 'cancelled':
      return <span className="badge badge-cancelled"><XCircle size={12} /> Cancelled</span>;
    case 'rescheduled':
      return <span className="badge badge-rescheduled"><RefreshCw size={12} /> Rescheduled</span>;
    case 'unpaid':
      return <span className="badge badge-unpaid"><AlertCircle size={12} /> Unpaid</span>;
    case 'partiallypaid':
      return <span className="badge badge-partiallypaid"><DollarSign size={12} /> Partially Paid</span>;
    case 'paid':
      return <span className="badge badge-paid"><CheckCircle size={12} /> Paid</span>;
    case 'overdue':
      return <span className="badge badge-overdue"><AlertCircle size={12} /> Overdue</span>;
    default:
      return <span className="badge">{status}</span>;
  }
};
