(define (make-queue)
  (let ((front-ptr `())
        (rear-ptr `()))
    (define (set-front-ptr new-ptr)
      (set! front-ptr new-ptr))
    (define (set-rear-ptr new-ptr)
      (set! rear-ptr new-ptr))
    (define (dispatch m)
      (cond ((eq? m `get-front-ptr)
	     front-ptr)
	    ((eq? m `get-rear-ptr)
	     rear-ptr)
	    ((eq? m `set-rear-ptr)
	     set-rear-ptr)
	    ((eq? m `set-front-ptr)
	     set-front-ptr)
	    (else front-ptr)))
    dispatch))

(define (front-ptr queue)
  (queue `get-front-ptr))

(define (rear-ptr queue)
  (queue `get-rear-ptr))

(define (set-front-ptr! queue item)
  ((queue `set-front-ptr) (list item)))

(define (set-rear-ptr! queue item)
  ((queue `set-rear-ptr) (list item)))

(define (empty-queue? queue)
  (null? (front-ptr queue)))

(define (insert-queue! queue item)
  (let ((new-pair (cons item `())))
    (cond ((empty-queue? queue)
           (set-front-ptr! queue new-pair)
           (set-rear-ptr! queue new-pair)
           (front-ptr queue))
          (else
           (set-cdr! (rear-ptr queue) new-pair)
           (set-rear-ptr! queue new-pair)
           (front-ptr queue)))))

(define (delete-queue! queue)
  (cond ((empty-queue? queue)
         (error "DELETE! вызвана с пустой очередью"))
        (else
         (set-front-ptr! queue (cdr (front-ptr queue)))
	 (front-ptr queue))))
