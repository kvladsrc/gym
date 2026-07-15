(define (accumulate combiner null-value term a next b)
  (define (iter result a)
    (if (> a b)
	result
	(iter (combiner result (term a)) (next a))))
  (iter null-value a))

(define (accumulate-rec combiner null-value term a next b)
  (if (> a b)
      null-value
      (combiner (term a) (accumulate-rec combiner null-value term (next a) next b))))

(define (sum a b)
  (define (loop x) x)
  (define (inc x) (+ x 1))
  (accumulate + 0 loop a inc b))

(define (sum-rec a b)
  (define (loop x) x)
  (define (inc x) (+ x 1))
  (accumulate-rec + 0 loop a inc b))

(define (factorial a)
  (define (loop a) a)
  (define (inc a) (+ a 1))
  (accumulate * 1 loop 1 inc a))

(define(factorial-rec a)
  (define (loop a) a)
  (define (inc a) (+ a 1))
  (accumulate-rec * 1 loop 1 inc a))
