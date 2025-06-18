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
	((or? exp) (eval-or (or-predicats exp) env))
	((and? exp) (eval-and (and-predicats exp)))
        ((application? exp)
         (apply (eval (operator exp) env)
                (list-of-values (operands exp) env)))
	(else
	 (error "Неизвестный тип выражения -- EVAL" exp))))

;And
(define (and? exp)
  (tagged-list? exp `and))
(define (and-predicats exp) (cdr exp))
(define (eval-and exp env)
  (if (null? exp)
      #t
      (if (eval (car exp) env)
	  (eval-and (cdr exp) env)
	  #f)))

;Or
(define (or? exp)
  (tagged-list? exp `or))
(define (or-predicats exp) (cdr exp))
(define (eval-or exp env)
  (if (null? exp)
      #f
      (if (eval (car exp) env)
	  #t
	  (eval-or (cdr exp) env))))
