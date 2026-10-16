import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Camera, User, LogOut, ShieldAlert } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';

export const Navbar = () => {
  const { role, currentUser, switchRole, logout } = useAuth();
  const location = useLocation();

  const isActive = (path) => location.pathname === path ? 'nav-link active' : 'nav-link';

  return (
    <>
      {/* Role Switcher Toolbar for Testing Wireframe Interfaces */}
      <div className="role-bar">
        <div className="container role-bar-inner">
          <div>
            <span><strong>Role View:</strong> Currently viewing as {role.toUpperCase()} ({currentUser ? currentUser.name : 'Guest'})</span>
          </div>
          <div className="role-bar-btns">
            <button className={`role-btn ${role === 'visitor' ? 'active' : ''}`} onClick={() => switchRole('visitor')}>
              Public Visitor
            </button>
            <button className={`role-btn ${role === 'client' ? 'active' : ''}`} onClick={() => switchRole('client')}>
              Client Portal
            </button>
            <button className={`role-btn ${role === 'admin' ? 'active' : ''}`} onClick={() => switchRole('admin')}>
              Photographer Admin
            </button>
          </div>
        </div>
      </div>

      <header className="site-navbar">
        <div className="container navbar-inner">
          <Link to="/" className="brand-logo">
            <Camera size={26} color="#4A3525" />
            <span>ILY PHOTO</span>
          </Link>

          <nav>
            <ul className="nav-links">
              {/* Public Links */}
              <li><Link to="/" className={isActive('/')}>Home</Link></li>
              <li><Link to="/portfolio" className={isActive('/portfolio')}>Portfolio</Link></li>
              <li><Link to="/pricing" className={isActive('/pricing')}>Pricing</Link></li>
              <li><Link to="/contact" className={isActive('/contact')}>Contact Us</Link></li>

              {/* Client Portal Links */}
              {role === 'client' && (
                <>
                  <li><Link to="/client/dashboard" className={isActive('/client/dashboard')}>Dashboard</Link></li>
                  <li><Link to="/client/booking" className={isActive('/client/booking')}>Make Booking</Link></li>
                  <li><Link to="/client/gallery" className={isActive('/client/gallery')}>My Gallery</Link></li>
                  <li><Link to="/client/invoices" className={isActive('/client/invoices')}>Invoices</Link></li>
                  <li><Link to="/client/reviews" className={isActive('/client/reviews')}>Reviews</Link></li>
                </>
              )}

              {/* Admin Dashboard Links */}
              {role === 'admin' && (
                <>
                  <li><Link to="/admin/dashboard" className={isActive('/admin/dashboard')}>Admin Stats</Link></li>
                  <li><Link to="/admin/bookings" className={isActive('/admin/bookings')}>Bookings</Link></li>
                  <li><Link to="/admin/gallery" className={isActive('/admin/gallery')}>Upload Gallery</Link></li>
                  <li><Link to="/admin/invoices" className={isActive('/admin/invoices')}>Invoices</Link></li>
                  <li><Link to="/admin/reviews" className={isActive('/admin/reviews')}>Reviews Admin</Link></li>
                </>
              )}
            </ul>
          </nav>

          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center' }}>
            {role === 'visitor' ? (
              <>
                <Link to="/client/login" className="btn btn-outline btn-sm">Client Login</Link>
                <Link to="/admin/login" className="btn btn-black btn-sm">Admin</Link>
              </>
            ) : (
              <button onClick={logout} className="btn btn-secondary btn-sm" style={{ gap: '0.35rem' }}>
                <LogOut size={16} /> Logout
              </button>
            )}
          </div>
        </div>
      </header>
    </>
  );
};
