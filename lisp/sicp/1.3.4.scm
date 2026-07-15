(define (average-damp f)
  (lambda (x) (average x (f x))))

(define (average x y)
  (/ (+ x y) 2))

(define tolerance 0.00001)

(define (fixed-point f first-guess)
  (define (close-enough? v1 v2)
    (< (abs (- v1 v2)) tolerance))
  (define (try guess)
    (let ((next (f guess)))
      (if (close-enough? guess next)
	  next
	  (try next))))
  (try first-guess))

(define (square x) (* x x))

(define (cube-root x)
  (fixed-point (average-damp (lambda (y) (/ x (square y))))
               1.0))

(define (deriv g)
  (lambda (x)
    (/ (- (g (+ x dx)) (g x))
       dx)))

(define dx 0.00001)

(define (cube x) (* x x x))

(define (newton-transform g)
  (lambda (x)
    (- x (/ (g x) ((deriv g) x)))))

(define (newton-method g guess)
  (fixed-point (newton-transform g) guess))

(define (fixed-point-of-transform g transform guess)
  (fixed-point (transform g) guess))

(define (sqrt x)
  (fixed-point-of-transform (lambda (y) (/ x y))
                            average-damp
                            1.0))

;;Упражнение 1.40
(define (cubic a b c)
  (lambda (x)
    (+ (cube x)
       (* a (square x))
       (* b x)
       c)))

;;Упражнение 1.41
(define (double f)
  (lambda (x)
    (f (f x))))

;;Упражнение 1.42
(define (compose f g)
  (lambda (x)
    (f (g x))))

;;Упражнение 1.43
(define (repeated f n)
  (define (rf x n)
    (if (> n 1)
	(f (rf x (- n 1)))
	(f x)))
  (lambda (x) (rf x n)))

;;Упражнение 1.44
(define (smooth f)
  (lambda (x)
    (/ (+ (f (- x dx))
	  (f x)
	  (f (+ x dx)))
       3)))

(define (smooth-n f dx n)
  ((repeated (lambda (g) (smooth g dx)) n) f))

;;Упражнение 1.45
