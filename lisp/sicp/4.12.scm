(define (lookup-variable-value var env)
  (let ((result (search var env)))
    (if result
	(get-val result)
	"Not found")))

(define (set-variable-value! var val env)
  (let ((result (search var env)))
    (if result
	(set-val! result val)
	"Not found")))

(define (define-variable! var val env)
  (let ((result (search var env)))
    (if result
	(set-val! result val)
	(add-binding-to-frame! var val frame))))

(define (search var env)
  (let ((frame (first-frame env)))
    (if (eq? frame the-empty-environment)
	false
	(let scan ((first frame))
	  (cond ((null? first)
		 (search var (enclosing-environment env)))
		((eq? var (get-var first))
		 first)
		(scan (cdr first)))))))

(define (get-var frame)
  (car (car frame)))
(define (get-val frame)
  (cdr (car frame)))
(define (set-val! frame val)
  (set-cdr! (car frame) val))
