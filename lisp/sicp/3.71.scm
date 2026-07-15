(define ramanujan
  (define (ramanujan-filter S presum)
    (let ((i (caar S))
	  (j (cdr (car S))))
      (sum (+ (cube i) (cube j))))
    (cond ((= presum sum)
	   (stream-cons sum
			(ramanujan-filter
			 (stream-cdr s)
			 presum)))
	  ((< presum sum)
	   (ramanujan-filter (stream-cdr s) sum))))
  (ramanujan-filter
   (weighted-pairs
    integers integers
    (lambda (l)
      (let ((i (car l))
	    (j (cadr l))))
      (+ (cube i)
	 (cube j)))))
  0))
