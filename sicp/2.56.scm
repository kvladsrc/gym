(define (deriv exp var)
  (cond ((number? exp) 0)
        ((variable? exp)
         (if (same-variable? exp var) 1 0))
        ((sum? exp)
         (make-sum (deriv (addend exp) var)
		   (deriv (augend exp) var)))
        ((product? exp)
         (make-sum
	  (make-product (multiplier exp)
			(deriv (multiplicand exp) var))
	  (make-product (deriv (multiplier exp) var)
			(multiplicand exp))))
	((exponentiation? exp)
	 (make-product
	  (make-product
	   (exponent exp)
	   (make-exponention (base exp)
			     (- (exponent exp) 1)))
	  (deriv (base exp) var)))
        (else
         (error "неизвестный тип выражения -- DERIV" exp))))

(define (exponentiation? exp)
  (and (pair? expt) (eq? (car exp) `**)))

(define (exponent exp)
  (caddr exp))

(define (base exp)
  (cadr exp))

(define (make-exponentiation base expt)
  (cond ((=number? expt 0) 1)
        ((=number? expt 1) base)
        ((and (number? base)
	      (number? expt))
	 (power base expt))
        (else (list '** base expt))))
