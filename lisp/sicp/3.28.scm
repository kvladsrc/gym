(define (or-gate a1 a2 output)
  (define (and-action-procedure)
    (let ((new-value
           (logical-or (get-signal a1) (get-signal a2))))
      (after-delay and-gate-delay
                   (lambda ()
                     (set-signal! output new-value)))))
  (add-action! a1 and-action-procedure)
  (add-action! a2 and-action-procedure)
  ’ok)

(define (logical-or signal1 signal2)
  (cond ((= signal1 1) 1)
	((= signal2 1) 1)
	((and (= signal2 0)
	      (= signal1 0))
	 0)
	(else "Error")))
