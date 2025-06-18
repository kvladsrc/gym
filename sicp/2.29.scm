(define (make-mobile left right)
  (list left right))

(define (make-branch length structure)
  (list length structure))

(define (left-branch mobile)
  (car mobile))

(define (right-branch mobile)
  (car (cdr mobile)))

(define (branch-length branch)
  (car branch))

(define (branch-structure branch)
  (car (cdr branch)))

(define (mobile? structure)
  (pair? structure))

(define (total-weight mobile)
  (define (branch-weight branch)
    (if (mobile? (branch-structure branch))
	(total-weight (branch-structure branch))
	(branch-structure branch)))
  (+ (branch-weight (left-branch mobile))
     (branch-weight (right-branch mobile))))

(define (ok? mobile)
  (define (rotation-moment branch)
    (* (branch-length branch)
       (if (mobile? (branch-structure branch))
	   (total-weight (branch-structure branch))
	   (branch-structure branch))))
  (define (branch-ok? branch)
    (if (mobile? (branch-structure branch))
	(ok? (structure-branch branch))
	#t))
  (and (= (rotation-moment (left-branch mobile))
	  (rotation-moment (right-branch mobile)))
       (branch-ok? (left-branch mobile))
       (branch-ok? (right-branch mobile))))
