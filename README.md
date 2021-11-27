CSLisp
=======

**CSLisp** is a Scheme / Lisp dialect implemented in C#, intended as an embedded scripting language in .NET projects.

It is a _bytecode compiled_ language, and comes with a compiler and a bytecode interpreter. The language includes the typical Lisp-dialect features you'd expect, like proper closures, tail-call optimization, and macros. However, like Scheme, it prefers explicit boolean types, and a single namespace. 

Design goals:
- Easy to embed and use in C# / .NET - no extra dependencies
- Safe - does not expose .NET libraries to user code unless desired
- Fast - or at least fast _enough_ with the use of bytecode compilation :)
- AOT friendly - does not use Reflection.Emit so it can be used in pre-compiled environments (mobile, consoles)
- Extensible - supports macros and primitives, user primops and reflection coming soon 

**CSLisp** is intended to be used as a library, embedded in another host program, and not a standalone executable. The compiler, bytecode interpreter, and runtime environment, are all easy to access and manipulate from host programs. Unit tests and REPL show how to interop with it.

Unlike most .NET Lisp implementations, CSLisp does **not** emit .NET bytecode, it loads text files only, and compiles to its own bytecode. This is intended for compatibility with _ahead-of-time (AOT) compiled_ environments, such as mobile and console games, which do not allow for runtime .NET IL generation or use of Reflection.Emit.

Language implementation should be pretty readable and easy to extend. Compiler and bytecode design are heavily ~cribbed from~ influenced by Quinnec's *"Lisp in Small Pieces"* and Norvig's *"Principles of Artificial Intelligence Programming"* . Standing on the shoulders on giants. :)  

This is very much a work in progress, so please pardon the dust, use at your own risk, and so on. :)



### USAGE

```csharp
Context ctx = new Context();	// make a new vm + compiler
ctx.Execute("(+ 1 2)");         // => List<Val>: [ 3 ]
```


### LANGUAGE DETAILS

Values are of type `Val` and can be of the following types:
-  Nil - a nil value which is the lack of anything else, as well as list terminator
-  Boolean - #t or #f, same as .net bool
-  Int - same as .Net Int32
-  Float - same as .Net Single
-  String - same as .Net String (immutable char sequence in double quotes)
-  Symbol - similar to Scheme: unique symbols interned in packages
-  Cons - pair of values
-  Closure - non-inspectable pair of environment and compiled code sequence
-  ReturnAddress - non-inspectable saved continuation
-  Vector

Small set of reserved keywords - everything else is a valid symbol
-  `begin` - used for a block of expressions, the result of the last one is returned
-  `set!` - destructively reassigns the specified local or global symbol
-  `if` - standard if statement, evaluates a predicate and then/else clauses
-  `if*` - disjunctive test, evaluates a predicate and if the result is false, evaluates the rest
-  `while` - standard while loop, unlike in other lisps this one is promoted to a reserved keyword and produces optimized bytecode
-  `lambda` - standard closure definition
-  `defmacro` - macros which are lisp snippets that are evaluated at compilation time, and produce more code
-  `quote` - a quoted expression evaluates to itself
-  `.`

Tail calls get optimized during compilation, without any language hints
```lisp
  (define (rec x) (if (= x 0) 0 (rec (- x 1))))
  (rec 1000000) ;; look ma, no stack overflow!
```

But of course you can also do standard boring iteration
```lisp
  (define (iter x) (while (> x 0) (set! x (- x 1))))
  (iter 1000000) ;; no malloc, no stack pressure
```

Quotes, quasiquotes and unquotes are supported in the Lisp fashion:
```lisp
  'x                 ;; => 'x
  `x                 ;; => 'x
  `,x                ;; => x
  `(1 ,(list 2 3))   ;; => '(1 (2 3))
  `(1 ,@(list 2 3))  ;; => '(1 2 3)
```

Closures
```lisp
  (set! fn (let ((sum 0)) (lambda (delta) (set! sum (+ sum delta)) sum))) 
  (fn 0)    ;; => 0
  (fn 100)  ;; => 100
  (fn 0)    ;; => 100
```

Macros are more like Lisp than Scheme. 
```lisp
  ;; (let ((x 1) (y 2)) (+ x 1)) => 
  ;;   ((lambda (x y) (+ x y)) 1 2)
  (defmacro let (bindings . body) 
    `((lambda ,(map car bindings) ,@body) 
      ,@(map cadr bindings)))
```

Macroexpansion - single-step and full
```lisp
  (and 1 (or 2 3))         ;; => 2
  (mx1 '(and 1 (or 2 3)))  ;; => (if 1 (core:or 2 3) #f)
  (mx '(and 1 (or 2 3)))   ;; => (if 1 (if* 2 3) #f)
```

Built-in primitives live in the "core" package and can be redefined
```lisp
  (+ 1 2)               ;; => 3
  (set! core:+ core:*)  ;; => [Closure]
  (+ 1 2)               ;; => 2
```

Packages 
```lisp
  (package-set "math")       ;; => "math"
  (package-get)              ;; => "math"
  (package-import ("core"))  ;; => null
  (package-export '(sin cos))
```

Built-in primitives are very bare bones (for now):
-  Functions:
    -  `+ - * / = != < <= > >=`
    -  `const list append length`
    -  `not null? cons? atom? string? number? boolean?`
    -  `car cdr cadr cddr caddr cdddr map`
    -  `mx mx1 trace gensym`
    -  `eval`
    -  `apply`
    -  `package-set package-get package-import package-export`
    -  `nth nth-tail nth-cons`
    -  `first second third after-first after-second after-third rest`
    -  `fold-left fold-right`
    -  `reverse index-of zip`
    -  `trace`
-  Macros
    -  `let let* letrec`
    -  `define`
    -  `and or`
    -  `cond case`
    -  `for dotimes`
    -  `chain chain-list`


### .NET INTEROP

.NET interop is accomplished via several built-in primitive functions: 
  - the `..` operator which uses reflection to dereference 
    the methods/properties/fields by name, and then potentially call them 
    or retrieve their values.
  - the `.!` operator similar to `set!` which sets fields and properties
  - the `.net` operator which creates new instances of types

Interop is a work in progress, and you can find more details in the 
[interop design.txt](interop design.txt) document. Meanwhile, here are some examples:

```lisp
;; simple lookups and function calls
(.. 'System)                    ;; => object representing System namespace
(.. 'System.DateTime)           ;; => object representing type DateTime
(.. "foobar" 'Length)           ;; => 6
(.. "foobar" 'ToUpper)          ;; => "FOOBAR"
(.. 'System.Int32.Parse "123")  ;; => 123
(.. 'System.DateTime.Now)       ;; => [new DateTime object]
(.. (.new 'System.DateTime 1999 12 31) 'ToString "yyyy")    ;; => "1999"

;; create an instance of a type
(.new 'System.DateTime 2021 1 1)        ;; => [new DateTime object]
(.new (.. 'System.DateTime) 2021 1 1)   ;; => [new DateTime object]

;; set field or property
(let ((array (.new 'System.Collections.ArrayList 10)))
  (.! array 'Capacity 100)      ;; call setter on the Capacity property
  array)                        ;; => array with capacity set to 100

;; indexed getter and setter field or property
;; uses the special Item property as defined by .Net
(let ((array (.new 'System.Collections.ArrayList 10)))
  (.. array 'Add 42)            ;; call array.Add(42)
  (.! array 'Item 0 43)         ;; call indexed setter, i.e. array[0] = 43
  (.. array 'Item 0))           ;; => 43, i.e. returns value of array[0]

```

In the near future we'll add an equivalent of `using` statements, 
as well as some means for limiting which types and namespaces are accessible.



### Other Scheme-like goodies

##### VECTORS

*Vectors* are like .Net arrays, except they hold Lisp values and have a specific printed format. But similarly to arrays, they feature constant-time access to indexed members, and they're zero-indexed and non-resizable.

```lisp
  (set! v (make-vector 3))          ;; => [Vector () () ()]   ;; i.e. 3 nil values
  (set! v (make-vector 3 0))        ;; => [Vector 0 0 0]
  (set! v (make-vector '(a b c)))   ;; => [Vector a b c]

  (vector-length v)                 ;; => 3
  (vector-get v 0)                  ;; => a
  (vector-set! v 0 42)              ;; => 42
  v                                 ;; => [Vector 42 b c]
```

##### RECORDS

*Records* are objects with named fields, inspired by SRFI-9. They need to be defined first, 
in terms of fields, field accessors, constructor, and predicate. 

```lisp
  ;; define a new record:

  (define-record-type 
    point                       ;; record name
    (make-point x y)            ;; constructor for 2 fields
    point?                      ;; predicate
    (x getx setx!)              ;; first field: name, getter, setter
    (y gety))                   ;; second field: name, getter, but no setter (field is read only)

  ;; now we can create new record instances:

  (define p (make-point 1 2))   ;; => p
  (point? p)                    ;; => #t
  (point? 1)                    ;; => #f
  (point? '(a b))               ;; => #f

  p                             ;; => [Vector [Closure] 1 2]
  (getx p)                      ;; => 1
  (gety p)  ;; => 2

  (setx! p 42)                  ;; => 42
  p                             ;; => [Vector [Closure] 42 2]
```
  


### TODOS

- Fix bugs, add documentation (hah!)
- Build out the standard library
- Flesh out .NET interop 
  - need to enable blocklisting/permlisting of namespaces and types for security purposes
  - reflection would benefit from type hints to avoid runtime inspection
- Peephole optimizer; also optimize execution of built-in primitives.
- Add better debugging: trace function calls, their args and return values, etc




##### KNOWN BUGS

- Error messages are somewhere between opaque and potentially misleading
- Redefining a known macro as a function will fail silently in weird ways
- Symbol / package resolution - eg. if a symbol "foo" is defined in core 
  but not in the package "bar", then "bar:foo" will resolve to "core:foo" 
  even though it should resolve as undefined.



###  COMPILATION EXAMPLES

Just a few examples of the bytecode produced by the compiler. More can be found 
by running unit tests and inspecting their outputs - they are _quite_ verbose.

Also, see [bytecode design.txt](bytecode design.txt) for more info.

```
Inputs:  (+ 1 2)
Parsed:  (core:+ 1 2)
Compiled:

  CODE BLOCK # 42 ; () => ((+ 1 2))
  0 MAKE_ENV  0 ; ()
  1 PUSH_CONST  1
  2 PUSH_CONST  2
  3 GLOBAL_GET  +
  4 JMP_CLOSURE 2

Inputs:  (+ (+ 1 2) 3)
Parsed:  (core:+ (core:+ 1 2) 3)
Compiled:

  CODE BLOCK # 43 ; () => ((+ (+ 1 2) 3))
  0 MAKE_ENV  0 ; ()
  1 SAVE_RETURN "K0"  6
  2 PUSH_CONST  1
  3 PUSH_CONST  2
  4 GLOBAL_GET  +
  5 JMP_CLOSURE 2
6 LABEL "K0"
  7 PUSH_CONST  3
  8 GLOBAL_GET  +
  9 JMP_CLOSURE 2

Inputs:  ((lambda (a) a) 5)
Parsed:  ((lambda (a) a) 5)

  CODE BLOCK # 69 ; (a) => (a)
  0 MAKE_ENV  1 ; (a)
  1 LOCAL_GET 0 0 ; a
  2 RETURN_VAL

  CODE BLOCK # 70 ; () => (((lambda (a) a) 5))
  0 MAKE_ENV  0 ; ()
  1 PUSH_CONST  5
  2 MAKE_CLOSURE  [Closure] ; #69 : (a)
  3 JMP_CLOSURE 1

Inputs:  (begin (set! incf (lambda (x) (+ x 1))) (incf (incf 5)))
Parsed:  (begin (set! incf (lambda (x) (core:+ x 1))) (incf (incf 5)))
Compiled:

  CODE BLOCK # 66 ; (x) => ((+ x 1))
  0 MAKE_ENV  1 ; (x)
  1 LOCAL_GET 0 0 ; x
  2 PUSH_CONST  1
  3 GLOBAL_GET  +
  4 JMP_CLOSURE 2

  CODE BLOCK # 67 ; () => ((begin (set! incf (lambda (x) (+ x 1))) (incf (incf 5))))
  0 MAKE_ENV  0 ; ()
  1 MAKE_CLOSURE  [Closure] ; #66 : ((+ x 1))
  2 GLOBAL_SET  incf
  3 STACK_POP
  4 SAVE_RETURN "K0"  8
  5 PUSH_CONST  5
  6 GLOBAL_GET  incf
  7 JMP_CLOSURE 1
8 LABEL "K0"
  9 GLOBAL_GET  incf
  10  JMP_CLOSURE 1

```


