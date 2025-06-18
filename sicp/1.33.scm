(define (accumulate filter combiner null-value term a next b)
  (define (iter result a)
    (if (> a b)
	result
	(iter (combiner result
			(if (filter a)
			    (term a)
			    null-value))
			(next a))))
  (iter null-value a))

(define (accumulate-rec filter combiner null-value term a next b)
  (if (> a b)
      null-value
      (combiner (if (filter a)
		    (term a)
		    null-value)
		(accumulate-rec filter combiner null-value term (next a) next b))))

(define (sum-of-even-squad a b)
  (define (inc x) (+ 1 x))
  (accumulate even? + 0 squad a inc b))

(define (squad x) (* x x))

(define (expt-iter n x)
  (define (iter result state)
    (if (= state x)
	result
	(iter (* result n) (+ state 1))))
  (iter 1 0))

(define (expt-rec n x)
  (if (= x 0)
      1
      (* n (expt-rec n (- x 1)))))

(define (fast-expt n x)
  (define (iter result num exp)
    (if (= exp 1)
	result
	(if (= (remainder exp 2) 0)
	    (iter (* result num) (* num num) (/ exp 2))
	    (iter (* result num) num (- exp 1)))))
  (cond ((= x 0) 1)
	((< x 0) -1)
	(else (iter n n x))))
