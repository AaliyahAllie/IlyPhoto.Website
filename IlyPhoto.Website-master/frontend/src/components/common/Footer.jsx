import React from 'react';
import { Link } from 'react-router-dom';
import { Camera, ShieldCheck, Mail, Phone, MapPin } from 'lucide-react';

export const Footer = () => {
  return (
    <footer className="site-footer">
      <div className="container">
        <div className="footer-grid">
          <div className="footer-column">
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
              <Camera size={24} color="#4A3525" />
              <h4 style={{ margin: 0, fontSize: '1.2rem', fontWeight: 800 }}>ILY PHOTO</h4>
            </div>
            <p style={{ fontSize: '0.88rem', color: '#666', marginBottom: '1rem' }}>
              Creating a global audience & capturing timeless photography experiences with elegance.
            </p>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.8rem', color: '#4A3525' }}>
              <ShieldCheck size={16} />
              <span>POPIA Compliant Personal Information Protection</span>
            </div>
          </div>

          <div className="footer-column">
            <h4>Quick Links</h4>
            <ul>
              <li><Link to="/">Home Page</Link></li>
              <li><Link to="/portfolio">Public Portfolio</Link></li>
              <li><Link to="/pricing">Pricing & Packages</Link></li>
              <li><Link to="/contact">Contact Us</Link></li>
            </ul>
          </div>

          <div className="footer-column">
            <h4>Client & Admin</h4>
            <ul>
              <li><Link to="/client/login">Client Sign In</Link></li>
              <li><Link to="/client/register">Create Client Account</Link></li>
              <li><Link to="/client/booking">Book Photography Session</Link></li>
              <li><Link to="/admin/login">Photographer Admin Portal</Link></li>
            </ul>
          </div>

          <div className="footer-column">
            <h4>Get In Touch</h4>
            <ul style={{ fontSize: '0.88rem', color: '#666' }}>
              <li style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Mail size={14} /> info@ilyphoto.co.za
              </li>
              <li style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <Phone size={14} /> +27 (0) 82 123 4567
              </li>
              <li style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                <MapPin size={14} /> Ily Photo Studio, Cape Town / JHB
              </li>
            </ul>
          </div>
        </div>

        <div className="footer-bottom">
          <p>© {new Date().getFullYear()} Ily Photo Platform. All rights reserved. Designed for WIL Portfolio & Website Automation.</p>
        </div>
      </div>
    </footer>
  );
};
