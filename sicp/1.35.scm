(define golden-ratio
  (fixed-point
   (lambda (x)
     (+ 1 (/ 1 x)))
   1))
