(define (product term a next b)
  (define (iter result a)
    (if (> a b)
	result
	(iter (* result (term a)) (next a))))
  (iter 1 a))

(define (product-rec term a next b)
  (if (> a b)
      1
      (* (term a)
	 (product-rec term (next a) next b))))

(define (factorial pos)
  (define (inc x)
    (+ 1 x))
  (define (loop x) x)
  (product loop 1 inc pos))

(define (pi a)
  (define (inc x)
    (+ x 1))
  (define (pi-term x)
    (if (= (remainder x 2) 0)
	(/ (+ x 2) (+ x 1))
	(/ (+ x 1) (+ x 2))))
  (* 4.0 (product pi-term 1 inc a)))
