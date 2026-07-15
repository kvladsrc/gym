(define (list-of-values exps env)
  (if (no-operands? exps)
      `()
      (let ((val (eval (first-operand exps) env)))
	(cons val
	      (list-of-values (rest-operands exps) env)))))

(define (list-of-values exps env)
  (if (no-operands? exps)
      `()
      (let ((val (list-of-values (rest-operands exps)
				 env)))
	(cons (eval (first-operand exps) env)
	      val))))
