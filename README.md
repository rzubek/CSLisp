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
-  Int - same as .net Int32
-  Float - same as .net Single
-  String - same as .net String (immutable char sequence in double quotes)
-  Symbol - similar to Scheme
-  Cons - pair of values
-  Closure - non-inspectable pair of environment and compiled code sequence
-  ReturnAddress - non-inspectable saved continuation

Small set of reserved keywords - everything else is a valid symbol
-  quote
-  begin
-  set!
-  if
-  if*
-  lambda
-  defmacro
-  .

Tail calls get optimized during compilation, without any language hints
```lisp
  (define (rec x) (if (= x 0) 0 (rec (- x 1))))
  (rec 1000000) ;; look ma, no stack overflow!
```

Quotes, quasiquotes and unquotes are supported in the Lisp fashion:
```
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
  -  const list append length
  -  not null? cons? atom? string? number? boolean?
  -  car cdr cadr cddr caddr cdddr map
  -  mx mx1 trace gensym
  -  package-set package-get package-import package-export
  -  first second third rest
  -  fold-left fold-right
-  Macros
  -  let let* letrec define
  -  and or cond case



##### TODOS

- Fix bugs, add documentation (hah!)
- Build out the standard library
- Flesh out .NET interop - either via an easy FFI or via reflection (but with an eye on security)
- Peephole optimizer; also optimize execution of built-in primitives.
- Add better debugging: trace function calls, their args and return values, etc


##### KNOWN BUGS

- Error messages are somewhere between opaque and potentially misleading
- Redefining a known macro as a function will fail silently in weird ways
- Symbol / package resolution is buggy - eg. if a symbol "foo" is defined in core 
  but not in the package "bar", then "bar:foo" will resolve to "core:foo" 
  even though it should resolve as undefined.



#####  COMPILATION EXAMPLES

Just a few examples of the bytecode produced by the compiler. More can be found by running unit tests and inspecting their outputs - they are _quite_ verbose.

```
inputs:  (+ 1 2)
parsed:  (core:+ 1 2)
  ARGS  0
  CONST 1
  CONST 2
  GVAR  core:+
  CALLJ 2

inputs:  (begin (+ (+ 1 2) 3) 4)
parsed:  (begin (core:+ (core:+ 1 2) 3) 4)
  ARGS  0
  SAVE  "K0"  11
  SAVE  "K1"  7
  CONST 1
  CONST 2
  GVAR  core:+
  CALLJ 2
LABEL "K1"
  CONST 3
  GVAR  core:+
  CALLJ 2
LABEL "K0"
  POP
  CONST 4
  RETURN

inputs:  ((lambda (a) a) 5)
parsed:  ((lambda (a) a) 5)
  ARGS  0
  CONST 5
  FN  [Closure] ; (a)
    ARGS  1
    LVAR  0 0 ; a
    RETURN
  CALLJ 1

inputs:  (begin (set! incf (lambda (x) (+ x 1))) (incf (incf 5)))
parsed:  (begin (set! foo:incf (lambda (foo:x) (core:+ foo:x 1))) (foo:incf (foo:incf 5)))
  ARGS  0
  FN  [Closure] ; ((core:+ foo:x 1))
    ARGS  1
    LVAR  0 0 ; foo:x
    CONST 1
    GVAR  core:+
    CALLJ 2
  GSET  foo:incf
  POP
  SAVE  "K0"  8
  CONST 5
  GVAR  foo:incf
  CALLJ 1
LABEL "K0"
  GVAR  foo:incf
  CALLJ 1
```


