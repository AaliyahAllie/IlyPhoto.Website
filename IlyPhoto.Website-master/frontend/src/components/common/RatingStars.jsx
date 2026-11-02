import React from 'react';
import { Star } from 'lucide-react';

export const RatingStars = ({ rating = 5, interactive = false, onRatingChange }) => {
  return (
    <div style={{ display: 'flex', gap: '0.2rem' }}>
      {[1, 2, 3, 4, 5].map((star) => (
        <Star
          key={star}
          size={18}
          color="#D97706"
          fill={star <= rating ? "#D97706" : "transparent"}
          style={{ cursor: interactive ? 'pointer' : 'default' }}
          onClick={() => interactive && onRatingChange && onRatingChange(star)}
        />
      ))}
    </div>
  );
};
