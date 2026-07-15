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
	((let? exp) (eval (let->combination exp) env))
	((for? exp) (eval (for->combination exp) env))
        ((application? exp)
         (apply (eval (operator exp) env)
                (list-of-values (operands exp) env)))
	(else
	 (error "Неизвестный тип выражения -- EVAL" exp))))

;Let
(define (let? exp) (tagged-list? exp `let))
(define (let-body exp) (caddr exp))
(define (let-variables exp)
  (map car (cadr exp)))
(define (let-exps exp)
  (map cdr (cadr exp)))
(define (let->combination exp)
  (if (let-complex? exp)
      (let-complex->combinarion exp)
      (cons (make-lambda (let-variables exp)
			 (let-body exp))
	    (let-exps exp))))

;Let complex
(define (let-complex? exp) (variable? (cadr exp)))
(define (let-complex-variable exp) (cadr exp))
(define (let-complex-variables exp)
  (map car (caddr exp)))
(define (let-complex-body exp)
  (cadddr exp))
(define (let-complex-exps exp)
  (map cdr (caddr exp)))
(define (let-complex->combination exp)
  (make-lambda
   ()
   (list `begin
	 (list `define
	       (cons (let-complex-variable exp)
		     (let-complex-variables exp))
	       (let-complex-body exp))
	 (cons (let-complex-variable exp)
	       (let-complex-exps exp)))))

;for
(define (for? exp) (tagged-list? exp `for))
(define (for-var exp) (cadr exp))
(define (for-pr exp) (caddr exp))
(define (for-next exp) (cadddr exp))
(define (for-body exp) (car (cdddr exp)))
(define (for->combination exp)
  (list `let
	`loop
	(list (cons `var (for-var exp))
	      (cons `pr (for-pr exp))
	      (cons `next (for-next exp))
	      (cons `body (for-body exp)))
	(make-if `(pr var)
		 `done
		 `(begin (body var)
			 (loop (next var)
			       pr
			       next
			       body)))))
