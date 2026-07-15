;;Half-interval method

(define (search f neg-point pos-point)
  (let ((midpoint (average neg-point pos-point)))
    (if (close-enough? neg-point pos-point)
	midpoint
	(let ((test-value (f midpoint)))
	  (cond ((positive? test-value)
		 (search f neg-point midpoint))
		((negative? test-value)
		 (search f midpoint pos-point))
		(else midpoint))))))

(define (close-enough? x y)
  (< (abs (- x y)) 0.00001))

(define (half-interval-method f a b)
  (let ((a-value (f a))
	(b-value (f b)))
    (cond ((and (negative? a-value) (positive? b-value))
	   (search f a b))
	  ((and (negative? b-value) (positive? a-value))
	   (search f b a))
	  (else
	   (error "TEH ERROR" a b)))))

(define (average x y)
  (/ (+ x y) 2))

;;Fixed point

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

(define (sqrt-fp x arg)
  (cond ((= arg 1)
	 (fixed-point (lambda (y) (average (/ x y) y))
	       1.0))
	((= arg 2)
	 (fixed-point-v (lambda (y) (average (/ x y) y))
               1.0))
	(else (display "TEH ERROR"))))

(define (golden-ratio)
  (fixed-point (lambda (x)
		 (+ 1 (/ 1 x)))
	       1.0))

;;Fixed point verbose

(define (fixed-point-v f first-guess)
  (define (close-enough? v1 v2)
    (< (abs (- v1 v2)) tolerance))
  (define (try guess)
    (let ((next (f guess)))
      (display "guess: ")
      (display guess)
      (newline)
      (if (close-enough? guess next)
          next
          (try next))))
  (try first-guess))
