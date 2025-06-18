(define (cc amount kinds-of-coins)
  (cond ((= amount 0) 1)
        ((or (< amount 0) (null? kinds-of-coins)) 0)
        (else (+ (cc amount
                     (cdr kinds-of-coins))
                 (cc (- amount
			(car kinds-of-coins))
                     kinds-of-coins)))))
