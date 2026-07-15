(define (fib n)
  (fib_iter 1 0 0 1 n))

(define (fib_iter a b p q count)
  (cond ((= count 0) b)
	((even? count)
	 (fib_iter a
		   b
		   (+ (square p) (square q))
		   (+ (* 2 p q) (square q))
		   (/ count 2)))
	(else (fib_iter (+ (* b q) (* a q) (* a p))
			(+ (* b p) (* a q))
			p
			q
			(- count 1)))))

(define (square x)
  (* x x))

(define (even? x)
  (= 0 (remainder x 2)))
