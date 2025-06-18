(define (and-gate a1 a2 output)
  (define (and-action-procedure)
    (let ((new-value
           (logical-and (get-signal a1) (get-signal a2))))
      (after-delay and-gate-delay
                   (lambda ()
                     (set-signal! output new-value)))))
  (add-action! a1 and-action-procedure)
  (add-action! a2 and-action-procedure)
  `ok)

(define (or-gate a1 a2 output)
  (define invert-a1 (make-wire))
  (define invert-a2 (make-wire))
  (inverter a1 invert-a1)
  (inverter a2 invert-a2)
  (and-gate a1 invert-a2 output)
  (and-gate invert-a1 a2 output))
