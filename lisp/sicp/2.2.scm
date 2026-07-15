(define (make-segment x1 y1 x2 y2)
  (cons (make-point x1 y1)
	(make-point x2 y2)))

(define (make-point x y)
  (cons x y))

(define (start-segment x)
  (car x))

(define (end-segment x)
  (cdr x))

(define (x-point x)
  (car x))

(define (y-point x)
  (cdr x))

(define (midpoint x)
  (make-point
   (/ (+ (x-point (start-segment x))
	 (x-point (end-segment x)))
      2)
   (/ (+ (y-point (start-segment x))
	 (y-point (end-segment x)))
      2)))
