(define (interval-radius i)
  (/ (- (lower-bound i) (upper-bound i))
     2))
