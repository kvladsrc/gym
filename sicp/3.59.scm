(define (integrate-series S)
  (stream-map / S integers))

(define cosine-stream
  (cons-stream 1 (integrate-series (minus sine-series))))
(define sine-series
  (cons-stream 0 (integrate-series cosine-stream))
