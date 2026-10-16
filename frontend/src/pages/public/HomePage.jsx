import React from 'react';
import { Link } from 'react-router-dom';
import { Camera, Calendar, Award, User, Star, ArrowRight } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { RatingStars } from '../../components/common/RatingStars';

export const HomePage = () => {
  const { reviews, portfolio } = useAuth();
  const featuredPortfolio = portfolio.filter(p => p.featured || p.isPublic).slice(0, 3);

  return (
    <div className="page-wrapper container">
      {/* Hero Section matching Wireframe 1 "Main / Home Page" */}
      <section className="hero-section">
        <h1 className="hero-title">Home</h1>
        <p className="hero-subtitle">where the team is</p>
        <div style={{ display: 'flex', justifyContent: 'center', gap: '1rem', marginTop: '1.5rem' }}>
          <Link to="/client/booking" className="btn btn-primary">
            <Calendar size={18} /> Book A Session
          </Link>
          <Link to="/portfolio" className="btn btn-outline">
            View Portfolio <ArrowRight size={16} />
          </Link>
        </div>
      </section>

      {/* Photographer About Section matching Wireframe 1 */}
      <section className="about-banner">
        <div className="about-avatar">
          <User size={64} />
        </div>
        <div>
          <h2 style={{ marginBottom: '0.5rem', color: '#4A3525' }}>About Ily Photo</h2>
          <p style={{ color: '#333', fontSize: '1.05rem', lineHeight: '1.7' }}>
            Welcome to Ily Photo! We specialize in capturing life's most genuine, radiant moments. 
            From elegant pre-wedding sessions to vibrant birthday celebrations and corporate events, 
            our goal is to establish a global presence while serving our clients with exceptional artistry.
          </p>
        </div>
      </section>

      {/* Featured Works Showcase */}
      <section style={{ marginBottom: '3rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
          <h2>Featured Portfolio</h2>
          <Link to="/portfolio" style={{ fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
            View All <ArrowRight size={16} />
          </Link>
        </div>

        <div className="card-grid">
          {featuredPortfolio.map(item => (
            <div key={item.id} className="card" style={{ padding: 0, overflow: 'hidden' }}>
              <div className="wireframe-box" style={{ height: '220px' }}>
                <img src={item.imageUrl} alt={item.title} />
              </div>
              <div style={{ padding: '1rem' }}>
                <span className="badge" style={{ backgroundColor: '#EAE3D2', color: '#4A3525', marginBottom: '0.5rem' }}>
                  {item.category}
                </span>
                <h4 style={{ margin: '0.25rem 0' }}>{item.title}</h4>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Reviews Snippets matching Wireframe 1 */}
      <section style={{ marginBottom: '3rem' }}>
        <h2 style={{ textAlign: 'center', marginBottom: '1.5rem' }}>Client Reviews</h2>
        <div className="card-grid">
          {reviews.slice(0, 3).map(rev => (
            <div key={rev.id} className="card" style={{ backgroundColor: '#FFFFFF' }}>
              <RatingStars rating={rev.rating} />
              <h4 style={{ margin: '0.75rem 0 0.25rem' }}>"{rev.clientName}"</h4>
              <p style={{ fontSize: '0.9rem', color: '#666', marginBottom: '0.75rem' }}>{rev.comment}</p>
              <div style={{ fontSize: '0.8rem', fontWeight: 700, color: '#4A3525' }}>
                - {rev.author} ({rev.service})
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
};
