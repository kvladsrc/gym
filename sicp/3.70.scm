(define (merge-weighted S T weight)
  (cond ((null-stream? S) T)
	((null-stream? T) S)
	(else
	 (let ((s (weight (stream-car S)))
	       (t (weight (stream-car T))))
	   (cond ((> s t)
		  (cons-stream
		   (stream-car T)
		   (merge-weighted S (stream-cdr T) weight)))
		 ((> t s)
		  (cons-stream
		   (stream-car S)
		   (merge-weighted (stream-car S) T weight)))
		 (else
		  (cons-stream
		   (stream-car S)
		   (merge-weighted (stream-cdr S) (stream-cdr T) weight))))))))

(define (weighted-pairs s t proc)
  (cons-stream
   (list (stream-car s) (stream-car t))
   (merge-weighted
    (stream-map (lambda (x) (list (stream-car s) x))
                (stream-cdr t))
    (weighted-pairs (stream-cdr s) (stream-cdr t) proc)
    proc)))

(define (sum-weight s)
  (+ (stream-car s)
     (stream-car (stream-cdr s))))

(define a (weighted-pairs integers integers sum-weight))

(define b (stream-filter
	   (lambda (l)
	     (let ((i (stream-car l))
		   (j (stream-car (stream-cdr l))))
	       (not (or (= (remainder i 2) 0)
			(= (remainder i 3) 0)
			(= (remainder i 5) 0)
			(= (remainder j 2) 0)
			(= (remainder j 3) 0)
			(= (remainder j 5) 0)))))
	   (weighted-pairs
	    integers integers
	    (lambda (l)
	      (let ((i (stream-car l))
		    (j (stream-car (stream-cdr l))))
		(+ (* i 2)
		   (* j 3)
		   (* 5 (* i j))))))))
