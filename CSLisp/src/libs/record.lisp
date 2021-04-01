;; Experimental record support
;; Inspired by SRFI-9



;;
;; Usage examples:

; (define-record-type point 
;   	(make-point x y) 
;   	point? 
;   	(x getx setx!) 
;   	(y gety))

; (set! p (make-point 1 2)) ; => point object
;
; (point? p)      ; => #t
; (point? '(a b)) ; => #f
;
; (getx p)     ; => 1
; (gety p)     ; => 2
;
; (setx! p 42) ; => 42
; (getx p)     ; => 42
;
; ; (sety! p 42) ; => error, this accessor is not defined




;;
;; Implementation


; A record is a vector with a specific structure

; 0: closure that returns its record type
; 1..n: field values for record fields


; A record type is also a vector with a specific structure

; 0: special pointer to record:record-type
; 1: type name (e.g. point)
; 2: list of n field names (e.g. '(x y))


;; First we define our meta-level record:record-type
;; that's also a record type that describes itself

(package-set "record")

(package-export '(
	record?
	define-record-type
	))


;; Helpers that make new record types
;; record:record-type points to the singleton prototype

(define (record-type-vector? v) 
	(and (vector? v) (= (vector-length v) 3)))

(define (make-record-type name field-name-list)
 	(let ((type (make-vector 3)))
 		; this line ensures that the 0th element will be the prototype
 		; this ensures that record types are always identifiable
 		(vector-set! type 0 (or record-type type))
 		;; record type name
 		(vector-set! type 1 name)
 		;; field names
 		(vector-set! type 2 field-name-list)
 		type))


;; our special singleton record type
(define record-type (make-record-type 'record:record-type 0))

(define (record-type? type) 
	(and (record-type-vector? type)
		(= (vector-get type 0) record-type)))

(define (record-type-fields type) 
	(vector-get type 2))

(define (record-type-field-count type) 
	(length (record-type-fields type)))

(define (record-type-field-index type field-name)
	(index-of field-name (record-type-fields type)))


;; Now we can start making new records

(define (record? v)
	(and (vector? v)
		(>= (vector-length v) 1)
		(closure? (vector-get v 0))
		(record-type? ((vector-get v 0)))))

(define (record-get-type rec) ((vector-get rec 0)))
(define (record-type-equals? rec type) (= (record-get-type rec) type))
(define (record-field-count rec) (- (vector-length rec) 1))

(define (record-get rec i) (vector-get rec (+ i 1)))
(define (record-set! rec i value) (vector-set! rec (+ i 1) value))

(define (make-record type)
	(letrec ((field-count (record-type-field-count type))
			 (rec (make-vector (+ field-count 1))))
		(vector-set! rec 0 (lambda () type))
		rec))

(define (make-record-filled type values)
	(letrec ((rec (make-record type))
			 (field-count (record-field-count rec)))
		(dotimes (i field-count)
			(record-set! rec i (nth values i)))
		rec))



;;
;; Now the macro that actually lets us define 
;; a record with fields and accessors

(define (make-getter-defs type args)
	(map (lambda (def) 
			(if (< (length def) 2)
				'()
				(letrec ((field-name (first def))
					 	 (getter-name (second def))
				 		 (index (+ 1 (record-type-field-index type field-name))))
					`(define (,getter-name rec) (vector-get rec ,index)))))
		args))

(define (make-setter-defs type args)
	(map (lambda (def)
			(if (< (length def) 3) 
				'()
				(letrec ((field-name (first def))
					 	 (setter-name (third def))
				 		 (index (+ 1 (record-type-field-index type field-name))))
					`(define (,setter-name rec val) (vector-set! rec ,index val)))))
		args))


(defmacro define-record-type (name constructor predicate . args)
	(letrec ((constructor-name (first constructor))
		 	 (fields (rest constructor))
		  	 (type (make-record-type name fields)))

		`(begin
			; define the constructor
			(define ,constructor (make-record-filled ,type (list ,@fields)))

			; define the predicate
			(define (,predicate rec) (and (record? rec) (record-type-equals? rec ,type)))

			; define accessors
			,@(make-getter-defs type args)
			,@(make-setter-defs type args)

			type)))



;; for testing

; (define-record-type point (make-point x y) point? (x getx setx!) (y gety))
; (set! p (make-point 1 2)) 
; p

