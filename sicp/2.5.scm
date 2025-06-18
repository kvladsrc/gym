(define (new-cons a b)
  (* (fast_expt 2 a) (fast_expt 3 b)))

(define (new-car p)
  (define (car-iter p)
    (if (= 0 (remainder p 3))
	(car-iter (/ p 3))
	(log2 p)))
  (car-iter p))

(define (log2 n)
  (define (car-iter n a)
    (if (= 1 (/ n 2))
	a
	(car-iter (/ n 2) (+ 1 a))))
  (car-iter n 1))

(define (new-cdr p)
  (define (cdr-iter p n)
    (if (= 0 (remainder p 3))
	(cdr-iter (/ p 3) (+ n 1))
	n))
  (cdr-iter p 0))

(define (even? x)
  (= (remainder x 2) 0))

(define (square x) (* x x))

;; Fast expt iteration proccess

(define (fast_expt b n)
  (define (iter a b n)
    (cond ((= n 0) a)
	  ((even? n) (iter a (square b) (/ n 2)))
	  (else (iter (* a b) b (- n 1)))))
  (iter 1 b n))
