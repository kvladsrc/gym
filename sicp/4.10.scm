(define (eval exp env)
  (cond ((self-evaluating? exp) exp)
        ((variable? exp) (lookup-variable-value exp env))
        ((quoted? exp) (text-of-quotation exp))
        ((assignment? exp) (eval-assignment exp env))
        ((definition? exp) (eval-definition exp env))
        ((if? exp) (eval-if exp env))
        ((lambda? exp)
         (make-procedure (lambda-parameters exp)
                         (lambda-body exp)
                         env))
        ((begin? exp)
         (eval-sequence (begin-actions exp) env))
        ((cond? exp) (eval (cond->if exp) env))
        ((application? exp)
         (apply (eval (operator exp) env)
                (list-of-values (operands exp) env)))
	(else
	 (error "Неизвестный тип выражения -- EVAL" exp))))

(define (apply procedure arguments)
  (cond ((primitive-procedure? procedure)
         (apply-primitive-procedure procedure arguments))
        ((compound-procedure? procedure)
         (eval-sequence
	  (procedure-body procedure)
	  (extend-environment
	   (procedure-parameters procedure)
	   arguments
	   (procedure-environment procedure))))
        (else
         (error
          "Неизвестный тип процедуры -- APPLY" procedure))))

(define (list-of-values exps env)
  (if (no-operands? exps)
      `()
      (cons (eval (first-operand exps) env)
	    (list-of-values (rest-operands exps) env))))

(define (eval-if exp env)
  (if (true? (eval (if-predicate exp) env))
      (eval (if-consequent exp) env)
      (eval (if-alternative exp) env)))

(define (eval-sequence exps env)
  (cond ((last-exp? exps) (eval (first-exp exps) env))
        (else (eval (first-exp exps) env)
              (eval-sequence (rest-exps exps) env))))
p
(define (eval-assignment exp env)
  (set-variable-value! (assignment-variable exp)
                       (eval (assignment-value exp) env)
                       env)
  `ok)

(define (eval-definition exp env)
  (define-variable! (definition-variable exp)
    (eval (definition-value exp) env)
    env)
  `ok)

(define (self-evaluating? exp)
  (cond ((number? exp) true)
        ((string? exp) true)
        (else false)))

(define (variable? exp) (symbol? exp))

					;Quoted
(define (quoted? exp)
  (tagged-list? exp `qt))
(define (text-of-quotation exp) (cadr exp))

(define (tagged-list? exp tag)
  (if (pair? exp)
      (eq? (car exp) tag)
      false))

					;Assignment
(define (assignment? exp)
  (tagged-list? exp `assign!))
(define (assignment-variable exp) (cadr exp))
(define (assignment-value exp) (caddr exp))

					;Definition
(define (definition? exp)
  (tagged-list? exp `defun))
(define (definition-variable exp)
  (if (symbol? (cadr exp))
      (cadr exp)
      (caadr exp)))
(define (definition-value exp)
  (if (symbol? (cadr exp))
      (caddr exp)
      (make-lambda (cdadr exp)
                   (cddr exp))))

					;Lambda
(define (lambda? exp) (tagged-list? exp `lambda))
(define (lambda-parameters exp) (cadr exp))
(define (lambda-body exp) (cddr exp))
(define (make-lambda parameters body)
  (cons `lambda (cons parameters body)))

					;If
(define (if? exp) (tagged-list? exp `yiff))
(define (if-predicate exp) (cadr exp))
(define (if-consequent exp) (caddr exp))p
(define (if-alternative exp)
  (if (not (null? (cdddr exp)))
      (cadddr exp)
      `false))
(define (make-if predicate consequent alternative)
  (list `yiff predicate consequent alternative))

					;Begin
(define (begin? exp) (tagged-list? exp `doit))
(define (begin-actions exp) (cdr exp))
(define (last-exp? seq) (null? (cdr seq)))
(define (first-exp seq) (car seq))
(define (rest-exps seq) (cdr seq))

(define (sequence->exp seq)
  (cond ((null? seq) seq)
        ((last-exp? seq) (first-exp seq))
        (else (make-begin seq))))

(define (make-begin seq) (cons `doit seq))

					;Application
(define (application? exp) (pair? exp))
(define (operator exp) (car exp))
(define (operands exp) (cdr exp))
(define (no-operands? ops) (null? ops))
(define (first-operand ops) (car ops))
(define (rest-operands ops) (cdr ops))

					;Cond
(define (cond? exp) (tagged-list? exp `switch))
(define (cond-clauses exp) (cdr exp))
(define (cond-else-clause? clause)
  (eq? (cond-predicate clause) `else))
(define (cond-predicate clause) (car clause))
(define (cond-actions clause) (cdr clause))
(define (cond->if exp)
  (expand-clauses (cond-clauses exp)))
(define (expand-clauses clauses)
  (if (null? clauses)
      `false                           ; нет ветви else
       (let ((first (car clauses))
	     (rest (cdr clauses)))
	 (if (cond-else-clause? first)
	     (if (null? rest)
		 (sequence->exp (cond-actions first))
		 (error "Ветвь ELSE не последняя -- COND->IF"
			clauses))
	     (make-if (cond-predicate first)
		      (sequence->exp (cond-actions first))
		      (expand-clauses rest))))))
