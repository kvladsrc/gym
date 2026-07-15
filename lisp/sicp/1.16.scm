;; Fast expt iteration proccess

(define (fast_expt b n)
  (define (iter a b n)
    (cond ((= n 0) a)
	  ((even? n) (iter a (square b) (/ n 2)))
	  (else (iter (* a b) b (- n 1)))))
  (iter 1 b n))

(define (even? x)
  (= (remainder x 2) 0))

(define (square x) (* x x))
