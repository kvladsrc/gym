(define (make-rat n d)
  (let ((g (gcd n d)))
    (if (< d 0)
	(cons (/ n g) (/ (- 0 d) g))
	(cons (/ n g) (/ d g)))))

(define (numer x)
  (car x))

(define (denum x)
  (cdr x))

(define (gcd n d)
  (define (delived? x y)
    (= 0 (remainder x y)))
  (if (delived? n d)
      d
      (gcd d (remainder n d))))

(define (mul-rat x y)
  (make-rat
   (* (numer x) (numer y))
   (* (denum x) (denum y))))

(define (add-rat x y)
  (make-rat
   (+ (* (numer x) (denum y))
      (* (numer y) (denum x)))
   (* (denum x) (denum y))))
