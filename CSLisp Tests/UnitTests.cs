using CSLisp.Core;
using CSLisp.Data;
using CSLisp.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CSLisp
{
    [TestClass]
    public class UnitTests
    {
        public enum LogType { None, Console, TempFile };

        /// <summary> Failures count during the last test run. </summary>
        private int _failures = 0;

        /// <summary> Where are we logging unit test results? </summary>
        public static readonly LogType LOG_TARGET = LogType.TempFile; // change as needed

        /// <summary> Logging implementation, could target standard console, or file, or nothing </summary>
        private class Logger : ILogger
        {
            private TextWriter _writer;

            public bool EnableParsingLogging => true;
            public bool EnableInstructionLogging => true;
            public bool EnableStackLogging => true;

            public void OpenLog (string name) {
                switch (LOG_TARGET) {
                    case LogType.TempFile:
                        string testDir = Path.Combine("..", "..", "Test Results");
                        Directory.CreateDirectory(testDir);
                        string filePath = Path.Combine(testDir, $"{name}.txt");
                        _writer = new StreamWriter(new FileStream(filePath, FileMode.Create));
                        _writer.WriteLine($"TEST {name} : " + System.DateTime.Now.ToLongTimeString());
                        break;
                    case LogType.Console:
                        _writer = System.Console.Out;
                        break;
                    default:
                        _writer = null; // don't log
                        break;
                }
            }

            public void Log (params object[] args) {
                if (_writer == null) { return; } // drop on the floor

                var strings = args.Select(obj => (obj == null) ? "null" : obj.ToString());
                var message = string.Join(" ", strings);
                _writer.WriteLine(message);
            }

            public void CloseLog () {
                _writer.Flush();
                _writer.Close();
                _writer = null;
            }
        }

        /// <summary> Logger implementation </summary>
        private readonly Logger _logger = new Logger();

        /// <summary> Simple logger wrapper </summary>
        private void Log (params object[] args) {
            if (_logger != null) {
                _logger.Log(args);
            }
        }

        /// <summary> Checks whether the result is equal to the expected value; if not, logs an info statement </summary>
        private void Check (bool result) => Check(new Val(result), new Val(true));
        //private void Check (Val result) => Check(result, new Val(true));
        private void Check (object result, object expected) => Check(new Val(result), new Val(expected));
        private void Check (Val result, Val expected, System.Func<Val, Val, bool> test = null) {
            Log("test: got", result, " - expected", expected);
            bool equal = (test != null) ? test(result, expected) : Val.Equals(result, expected);
            if (!equal) {
                _failures++;
                string msg = $"*** FAILED TEST: got {result} - expected {expected}";
                Log(msg);
                Assert.Fail(msg);
            }
        }


        [TestMethod]
        public void RunTests () {
            void Run (System.Action fn) {
                _logger.OpenLog(fn.GetMethodInfo().Name);
                _failures = 0;
                fn();
                Log(_failures == 0 ? "SUCCESS" : $"FAILURES: {_failures}");
                _logger.CloseLog();
            }

            Run(TestConsAndAtoms);
            Run(TestPackagesAndSymbols);
            Run(TestEnvironments);
            Run(TestCharStream);
            Run(TestParser);
            Run(PrintSampleCompilations);
            Run(TestVMNoCoreLib);
            Run(TestVMPrimitives);
            Run(TestPackages);
            Run(TestStandardLibs);

            Run(PrintAllStandardLibraries);
        }

        private void DumpCodeBlocks (Context ctx) => Log(ctx.code.DebugPrintAll());

        /// <summary> Tests various internal classes </summary>
        public void TestConsAndAtoms () {

            // test cons
            Check(Val.NIL.IsAtom);
            Check(new Val("foo").IsAtom);
            Check(new Val(5).IsAtom);
            Check(new Val(new Cons(new Val(1), new Val(2))).IsCons);

            {
                Cons list1 = new Cons("foo", new Cons("bar", Val.NIL));
                Check(Cons.IsCons(list1));
                Check(Cons.IsList(list1));
                Check(Cons.Length(list1), 2);
                Check(list1.first, "foo");
                Check(list1.second, "bar");
                Check(list1.afterSecond, Val.NIL);
                Check(list1.first.IsAtom); // "foo"
                Check(list1.rest.IsCons); // ("bar")
                Check(list1.second.IsAtom); // "bar"
                Check(list1.afterSecond.IsAtom); // nil
                Check(list1.afterSecond.IsNil); // null
                Check(Val.Print(list1), "(\"foo\" \"bar\")");
            }

            {
                Cons list2 = Cons.MakeList("foo", "bar");
                Check(Cons.IsCons(list2));
                Check(Cons.IsList(list2));
                Check(Cons.Length(list2), 2);
                Check(list2.first, "foo");
                Check(list2.second, "bar");
                Check(list2.afterSecond, Val.NIL);
                Check(list2.first.IsAtom); // "foo"
                Check(list2.rest.IsCons); // ("bar")
                Check(list2.second.IsAtom); // "bar"
                Check(list2.afterSecond.IsAtom); // null
                Check(list2.afterSecond.IsNil); // null
                Check(Val.Print(list2), "(\"foo\" \"bar\")");
            }

            {
                Cons nonlist = new Cons("foo", "bar");
                Check(Cons.IsCons(nonlist));
                Check(!Cons.IsList(nonlist));
                Check(nonlist.first, "foo");
                Check(nonlist.rest, "bar");
                Check(nonlist.first.IsAtom); // "foo"
                Check(nonlist.rest.IsAtom); // "bar"
                Check(nonlist.rest.IsNotNil);
                Check(Val.Print(nonlist), "(\"foo\" . \"bar\")");
            }
        }

        /// <summary> Test packages and symbols </summary>
        public void TestPackagesAndSymbols () {

            Packages packages = new Packages();
            Package p = packages.global;   // global package

            Symbol foo = p.Intern("foo");
            Check(foo.name, "foo");
            Check(foo.pkg, p);
            Check(foo.fullName, "foo");
            Check(p.Intern("foo") == foo); // make sure interning returns the same instance
            Check(p.Unintern(foo.name));        // first one removes successfully
            Check(!p.Unintern(foo.name));      // but second time there is nothing to remove
            Check(p.Intern("foo") != foo); // since we uninterned, second interning will return a different one

            Package p2 = new Package("fancy"); // some fancy package
            Symbol foo2 = p2.Intern("foo");
            Check(foo2.name, "foo");
            Check(foo2.pkg, p2);
            Check(foo2.fullName, "fancy:foo");

            // test the packages list

            Check(packages.global.name, (string) null); // get the global package
            Check(Val.Print(packages.global.Intern("foo")), "foo"); // check symbol name
            Check(packages.keywords.name, "");      // get the keywords package
            Check(Val.Print(packages.keywords.Intern("foo")), ":foo");  // check symbol name

            Check(packages.Find("fancy"), null);    // make sure the fancy package was not added yet
            Check(packages.Add(p2), p2);            // add our fancy custom package
            Check(packages.Intern("fancy"), p2);    // get the fancy package - should be the same one
            Check(Val.Print(packages.Intern("fancy").Intern("foo")), "fancy:foo");  // check symbol name
            Check(packages.Remove(p2));             // check removal (should only return true the first time)
            Check(!packages.Remove(p2));            // check removal (should only return true the first time)
        }

        public void TestEnvironments () {
            // test environments

            var p = new Package("temp");
            Environment e2 = Environment.Make(Cons.MakeList(p.Intern("env2symbol0")), null);
            // e2.setAt(0, p.intern("env2symbol0"));
            Environment e1 = Environment.Make(Cons.MakeList(p.Intern("env1symbol0"), p.Intern("env1symbol1")), e2);
            // e1.setAt(0, p.intern("env1symbol0"));
            // e1.setAt(1, p.intern("env1symbol1"));
            Environment e0 = Environment.Make(Cons.MakeList(p.Intern("env0symbol0"), p.Intern("env0symbol1")), e1);
            // e0.setAt(0, p.intern("env0symbol0"));
            // e0.setAt(1, p.intern("env0symbol1"));
            Check(Environment.GetVariable(p.Intern("env2symbol0"), e0).frameIndex, 2); // get frame coord
            Check(Environment.GetVariable(p.Intern("env2symbol0"), e0).symbolIndex, 0); // get symbol coord
            Check(Environment.GetVariable(p.Intern("env1symbol1"), e0).frameIndex, 1); // get frame coord
            Check(Environment.GetVariable(p.Intern("env1symbol1"), e0).symbolIndex, 1); // get symbol coord
            Check(Environment.GetVariable(p.Intern("env0symbol0"), e0).frameIndex, 0); // get frame coord
            Check(Environment.GetVariable(p.Intern("env0symbol0"), e0).symbolIndex, 0); // get symbol coord

            var e2s0loc = Environment.GetVariable(p.Intern("env2symbol0"), e0);
            Check(Environment.GetSymbolAt(e2s0loc, e0), p.Intern("env2symbol0"));
            Environment.SetSymbolAt(e2s0loc, p.Intern("NEW_SYMBOL"), e0);
            Check(Environment.GetSymbolAt(e2s0loc, e0), p.Intern("NEW_SYMBOL"));
            Check(Environment.GetVariable(p.Intern("NEW_SYMBOL"), e0).frameIndex, 2); // get frame coord
            Check(Environment.GetVariable(p.Intern("NEW_SYMBOL"), e0).symbolIndex, 0); // get symbol coord
        }


        /// <summary> Tests the character stream </summary>
        public void TestCharStream () {

            // first, test the stream wrapper
            InputStream stream = new InputStream();
            stream.Add("foo");
            stream.Save();
            Check(!stream.IsEmpty);
            Check(stream.Peek(), 'f'); // don't remove
            Check(stream.Read(), 'f'); // remove
            Check(stream.Peek(), 'o'); // don't remove
            Check(stream.Read(), 'o'); // remove
            Check(stream.Read(), 'o'); // remove last one
            Check(stream.Read(), (char) 0);
            Check(stream.IsEmpty);
            Check(stream.Restore());   // make sure we can restore the old save
            Check(stream.Peek(), 'f'); // we're back at the beginning
            Check(!stream.Restore()); // there's nothing left to restore
        }

        /// <summary> Tests the parser part of the system </summary>
        public void TestParser () {

            Packages packages = new Packages();
            Parser p = new Parser(packages, _logger);

            // test parsing simple atoms, check their internal form
            CheckParseRaw(p, "1", 1);
            CheckParseRaw(p, "+1.1", 1.1f);
            CheckParseRaw(p, "-2.0", -2f);
            CheckParseRaw(p, "-2", -2);
            CheckParseRaw(p, "#t", true);
            CheckParseRaw(p, "#f", false);
            CheckParseRaw(p, "#hashwhatever", false);
            CheckParseRaw(p, "a", packages.global.Intern("a"));
            CheckParseRaw(p, "()", Val.NIL);
            CheckParseRaw(p, "\"foo \\\" \"", "foo \" ");

            // now test by comparing their printed form
            CheckParse(p, "(a b c)", "(a b c)");
            CheckParse(p, " (   1.0 2.1   -3  #t   #f   ( ) a  b  c )  ", "(1 2.1 -3 #t #f () a b c)");
            CheckParse(p, "(a (b (c d)) e)", "(a (b (c d)) e)");
            CheckParse(p, "'(foo) '((a b) c) '()", "(quote (foo))", "(quote ((a b) c))", "(quote ())");
            CheckParse(p, "(a b ; c d)\n   e f)", "(a b e f)");

            // now check backquotes 
            CheckParse(p, "foo 'foo `foo `,foo", "foo", "(quote foo)", "(quote foo)", "foo");
            CheckParse(p, "`(foo)", "(list (quote foo))");
            CheckParse(p, "`(foo foo)", "(list (quote foo) (quote foo))");
            CheckParse(p, "`(,foo)", "(list foo)");
            CheckParse(p, "`(,@foo)", "(append foo)");
        }

        /// <summary> Test helper - does equality comparison on the raw parse results </summary>
        private void CheckParseRaw (Parser parser, string input, Val expected) {
            parser.AddString(input);

            List<Val> parsed = parser.ParseAll();
            Check(parsed.Count == 1);

            var result = parsed[0];
            Check(result, expected);
        }

        /// <summary> Test helper - takes parse results, converts them to the canonical string form, and compares to outputs </summary>
        private void CheckParse (Parser parser, string input, params string[] expecteds) {
            parser.AddString(input);

            List<Val> results = parser.ParseAll();
            Check(results.Count == expecteds.Length);

            for (int i = 0; i < results.Count; i++) {
                string result = Val.Print(results[i]);
                string expected = expecteds[i];
                Check(result, expected);
            }
        }



        /// <summary> Compiles some sample scripts and prints them out, without validation. </summary>
        public void PrintSampleCompilations () {
            Context ctx = new Context(false, _logger);

            CompileAndPrint(ctx, "5");
            CompileAndPrint(ctx, "\"foo\"");
            CompileAndPrint(ctx, "#t");
            CompileAndPrint(ctx, "'foo");
            CompileAndPrint(ctx, "(begin 1)");
            CompileAndPrint(ctx, "(begin 1 2 3)");
            CompileAndPrint(ctx, "x");
            CompileAndPrint(ctx, "(set! x (begin 1 2 3))");
            CompileAndPrint(ctx, "(begin (set! x (begin 1 2 3)) x)");
            CompileAndPrint(ctx, "(if p x y)");
            CompileAndPrint(ctx, "(begin (if p x y) z)");
            CompileAndPrint(ctx, "(if 5 x y)");
            CompileAndPrint(ctx, "(if #f x y)");
            CompileAndPrint(ctx, "(if x y)");
            CompileAndPrint(ctx, "(if p x (begin 1 2 x))");
            CompileAndPrint(ctx, "(if (not p) x y)");
            CompileAndPrint(ctx, "(if (if a b c) x y)");
            CompileAndPrint(ctx, "(lambda () 5)");
            CompileAndPrint(ctx, "((lambda () 5))");
            CompileAndPrint(ctx, "(lambda (a) a)");
            CompileAndPrint(ctx, "(lambda (a) (lambda (b) a))");
            CompileAndPrint(ctx, "(set! x (lambda (a) a))");
            CompileAndPrint(ctx, "((lambda (a) a) 5)");
            CompileAndPrint(ctx, "((lambda (x) ((lambda (y z) (f x y z)) 3 x)) 4)");
            CompileAndPrint(ctx, "(if a b (f c))");
            CompileAndPrint(ctx, "(if* (+ 1 2) b)");
            CompileAndPrint(ctx, "(if* #f b)");
            CompileAndPrint(ctx, "(begin (- 2 3) (+ 2 3))");
            //			compileAndPrint(ctx, "(begin (set! sum (lambda (x) (if (<= x 0) 0 (sum (+ 1 (- x 1)))))) (sum 5))");

            //DumpCodeBlocks(ctx);
        }

        /// <summary> Compiles an s-expression and prints the resulting assembly </summary>
        private void CompileAndPrint (Context ctx, string input) {
            Log("COMPILE inputs: ", input);
            ctx.parser.AddString(input);

            var parseds = ctx.parser.ParseAll();
            foreach (var parsed in parseds) {
                var results = ctx.compiler.Compile(parsed);
                Log(ctx.code.DebugPrint(results));
            }
        }




        /// <summary> Front-to-back test of the virtual machine </summary>
        public void TestVMNoCoreLib () {
            // first without the standard library
            Context ctx = new Context(false, _logger);

            // test reserved keywords
            CompileAndRun(ctx, "5", "5");
            CompileAndRun(ctx, "#t", "#t");
            CompileAndRun(ctx, "\"foo\"", "\"foo\"");
            CompileAndRun(ctx, "(begin 1 2 3)", "3");
            CompileAndRun(ctx, "xyz", "()");
            CompileAndRun(ctx, "xyz", "()");
            CompileAndRun(ctx, "(set! x 5)", "5");
            CompileAndRun(ctx, "(begin (set! x 2) x)", "2");
            CompileAndRun(ctx, "(begin (set! x #t) (if x 5 6))", "5");
            CompileAndRun(ctx, "(begin (set! x #f) (if x 5 6))", "6");
            CompileAndRun(ctx, "(begin (if* 5 6))", "5");
            CompileAndRun(ctx, "(begin (if* (if 5 #f) 6))", "6");
            CompileAndRun(ctx, "(begin (if* (+ 1 2) 4) 5)", "5");
            CompileAndRun(ctx, "(begin (if* (if 5 #f) 4) 5)", "5");
            CompileAndRun(ctx, "((lambda (a) a) 5)", "5");
            CompileAndRun(ctx, "((lambda (a . b) b) 5 6 7 8)", "(6 7 8)");
            CompileAndRun(ctx, "((lambda (a) (set! a 6) a) 1)", "6");
            CompileAndRun(ctx, "((lambda (x . rest) (if x 'foo rest)) #t 'a 'b 'c)", "foo");
            CompileAndRun(ctx, "((lambda (x . rest) (if x 'foo rest)) #f 'a 'b 'c)", "(a b c)");
            CompileAndRun(ctx, "(begin (set! x (lambda (a b c) (if a b c))) (x #t 5 6))", "5");
            CompileAndRun(ctx, "(begin (set! x 0) (while (< x 5) (set! x (+ x 1)) x))", "5");
            CompileAndRun(ctx, "(begin (set! x 0) (while (< x 5) (set! x (+ x 1))) x)", "5");

            //DumpCodeBlocks(ctx);
        }

        /// <summary> Front-to-back test of the virtual machine </summary>
        public void TestVMPrimitives () {
            // first without the standard library
            Context ctx = new Context(false, _logger);

            // test primitives
            CompileAndRun(ctx, "(+ 1 2)", "3");
            CompileAndRun(ctx, "(+ (+ 1 2) 3)", "6");
            CompileAndRun(ctx, "(+ 1 2 3 4)", "10");
            CompileAndRun(ctx, "(* 1 2 3 4)", "24");
            CompileAndRun(ctx, "(= 1 1)", "#t");
            CompileAndRun(ctx, "(!= 1 1)", "#f");
            CompileAndRun(ctx, "(cons 1 2)", "(1 . 2)");
            CompileAndRun(ctx, "`(a 1)", "(a 1)");
            CompileAndRun(ctx, "(list)", "()");
            CompileAndRun(ctx, "(list 1)", "(1)");
            CompileAndRun(ctx, "(list 1 2)", "(1 2)");
            CompileAndRun(ctx, "(list 1 2 3)", "(1 2 3)");
            CompileAndRun(ctx, "(length '(a b c))", "3");
            CompileAndRun(ctx, "(append '(1 2) '(3 4) '() '(5))", "(1 2 3 4 5)");
            CompileAndRun(ctx, "(list (append '() '(3 4)) (append '(1 2) '()))", "((3 4) (1 2))");
            CompileAndRun(ctx, "(list #t (not #t) #f (not #f) 1 (not 1) 0 (not 0))", "(#t #f #f #t 1 #f 0 #f)");
            CompileAndRun(ctx, "(list (null? ()) (null? '(a)) (null? 0) (null? 1) (null? #f))", "(#t #f #f #f #f)");
            CompileAndRun(ctx, "(list (cons? ()) (cons? '(a)) (cons? 0) (cons? 1) (cons? #f))", "(#f #t #f #f #f)");
            CompileAndRun(ctx, "(list (atom? ()) (atom? '(a)) (atom? 0) (atom? 1) (atom? #f))", "(#t #f #t #t #t)");
            CompileAndRun(ctx, "(list (number? ()) (number? '(a)) (number? 0) (number? 1) (number? #f))", "(#f #f #t #t #f)");
            CompileAndRun(ctx, "(list (string? ()) (string? '(a)) (string? 0) (string? 1) (string? #f) (string? \"foo\"))", "(#f #f #f #f #f #t)");
            CompileAndRun(ctx, "(begin (set! x '(1 2 3 4 5)) (list (car x) (cadr x) (caddr x)))", "(1 2 3)");
            CompileAndRun(ctx, "(begin (set! x '(1 2 3 4 5)) (list (cdr x) (cddr x) (cdddr x)))", "((2 3 4 5) (3 4 5) (4 5))");
            CompileAndRun(ctx, "(nth '(1 2 3 4 5) 2)", "3");
            CompileAndRun(ctx, "(nth-tail '(1 2 3 4 5) 2)", "(4 5)");
            CompileAndRun(ctx, "(nth-cons '(1 2 3 4 5) 2)", "(3 4 5)");
            CompileAndRun(ctx, "(trace \"foo\" \"bar\")", "()"); // trace outputs text instead
            CompileAndRun(ctx, "(begin (set! first car) (first '(1 2 3)))", "1");
            CompileAndRun(ctx, "(if (< 0 1) 5)", "5");
            CompileAndRun(ctx, "(if (> 0 1) 5)", "()");

            // test quotes and macros
            CompileAndRun(ctx, "`((list 1 2) ,(list 1 2) ,@(list 1 2))", "((list 1 2) (1 2) 1 2)");
            CompileAndRun(ctx, "(begin (set! x 5) (set! y '(a b)) `(x ,x ,y ,@y))", "(x 5 (a b) a b)");
            CompileAndRun(ctx, "(begin (defmacro inc1 (x) `(+ ,x 1)) (inc1 2))", "3");
            CompileAndRun(ctx, "(begin (defmacro foo (op . rest) `(,op ,@(map number? rest))) (foo list 1 #f 'a))", "(#t #f #f)");
            CompileAndRun(ctx, "(begin (defmacro lettest (bindings . body) `((lambda ,(map car bindings) ,@body) ,@(map cadr bindings))) (lettest ((x 1) (y 2)) (+ x y)))", "3");
            CompileAndRun(ctx, "(begin (defmacro inc1 (x) `(+ ,x 1)) (inc1 (inc1 (inc1 1))))", "4");
            CompileAndRun(ctx, "(begin (defmacro add (x y) `(+ ,x ,y)) (mx1 '(add 1 (add 2 3))))", "(core:+ 1 (add 2 3))");

            //DumpCodeBlocks(ctx);
        }

        public void TestPackages () {
            // without the standard library
            Context ctx = new Context(false, _logger);

            // test packages
            CompileAndRun(ctx, "(package-set \"foo\") (package-get)", "\"foo\"", "\"foo\"");
            CompileAndRun(ctx, "(package-set \"foo\") (package-import \"core\") (car '(1 2))", "\"foo\"", "()", "1");
            CompileAndRun(ctx, "(package-set nil) (set! x 5) (package-set \"foo\") (package-import \"core\") (set! x (+ 1 5)) (package-set nil) x", "()", "5", "\"foo\"", "()", "6", "()", "5");
            CompileAndRun(ctx, "(package-set \"foo\") (package-import \"core\") (set! first car) (first '(1 2))", "\"foo\"", "()", "[Closure/core:car]", "1");
            CompileAndRun(ctx, "(package-set \"a\") (package-export '(afoo)) (set! afoo 1) (package-set \"b\") (package-import \"a\") afoo", "\"a\"", "()", "1", "\"b\"", "()", "1");

            // test more integration
            CompileAndRun(ctx, "(package-set \"foo\")", "\"foo\"");
            CompileAndRun(ctx, "(begin (+ (+ 1 2) 3) 4)", "4");
            CompileAndRun(ctx, "(begin (set! incf (lambda (x) (+ x 1))) (incf (incf 5)))", "7");
            CompileAndRun(ctx, "(set! fact (lambda (x) (if (<= x 1) 1 (* x (fact (- x 1)))))) (fact 5)", "[Closure]", "120");
            CompileAndRun(ctx, "(set! fact-helper (lambda (x prod) (if (<= x 1) prod (fact-helper (- x 1) (* x prod))))) (set! fact (lambda (x) (fact-helper x 1))) (fact 5)", "[Closure]", "[Closure]", "120");
            CompileAndRun(ctx, "(begin (set! add +) (add 3 (add 2 1)))", "6");
            CompileAndRun(ctx, "(begin (set! kar car) (set! car cdr) (set! result (car '(1 2 3))) (set! car kar) result)", "(2 3)");
            CompileAndRun(ctx, "((lambda (x) (set! x 5) x) 6)", "5");

            //DumpCodeBlocks(ctx);
        }

        public void TestStandardLibs () {
            // now initialize the standard library
            var ctx = new Context(true, _logger);

            // test some basic functions
            CompileAndRun(ctx, "(map number? '(a 2 \"foo\"))", "(#f #t #f)");

            // test standard library
            CompileAndRun(ctx, "(package-set \"foo\")", "\"foo\"");
            CompileAndRun(ctx, "(mx1 '(let ((x 1)) x))", "((lambda (foo:x) foo:x) 1)");
            CompileAndRun(ctx, "(mx1 '(let ((x 1) (y 2)) (set! y 42) (+ x y)))", "((lambda (foo:x foo:y) (set! foo:y 42) (core:+ foo:x foo:y)) 1 2)");
            CompileAndRun(ctx, "(mx1 '(let* ((x 1) (y 2)) (+ x y)))", "(core:let ((foo:x 1)) (core:let* ((foo:y 2)) (core:+ foo:x foo:y)))");
            CompileAndRun(ctx, "(mx1 '(define x 5))", "(begin (set! foo:x 5) (quote foo:x))");
            CompileAndRun(ctx, "(mx1 '(define (x y) 5))", "(core:define foo:x (lambda (foo:y) 5))");
            CompileAndRun(ctx, "(list (gensym) (gensym) (gensym \"bar_\"))", "(foo:GENSYM-1 foo:GENSYM-2 foo:bar_3)");
            CompileAndRun(ctx, "(let ((x 1)) (+ x 1))", "2");
            CompileAndRun(ctx, "(let ((x 1) (y 2)) (set! y 42) (+ x y))", "43");
            CompileAndRun(ctx, "(let* ((x 1) (y x)) (+ x y))", "2");
            CompileAndRun(ctx, "(let ((x 1)) (let ((y x)) (+ x y)))", "2");
            CompileAndRun(ctx, "(letrec ((x (lambda () y)) (y 1)) (x))", "1");
            CompileAndRun(ctx, "(begin (let ((x 0)) (define (set v) (set! x v)) (define (get) x)) (set 5) (get))", "5");
            CompileAndRun(ctx, "(define x 5) x", "foo:x", "5");
            CompileAndRun(ctx, "(define (x y) y) (x 5)", "foo:x", "5");
            CompileAndRun(ctx, "(cons (first '(1 2 3)) (rest '(1 2 3)))", "(1 2 3)");
            CompileAndRun(ctx, "(list (and 1) (and 1 2) (and 1 2 3) (and 1 #f 2 3))", "(1 2 3 #f)");
            CompileAndRun(ctx, "(list (or 1) (or 2 1) (or (< 1 0) (< 2 0) 3) (or (< 1 0) (< 2 0)))", "(1 2 3 #f)");
            CompileAndRun(ctx, "(cond ((= 1 2) 2) ((= 1 4) 4) 0)", "0");
            CompileAndRun(ctx, "(cond ((= 2 2) 2) ((= 1 4) 4) 0)", "2");
            CompileAndRun(ctx, "(cond ((= 1 2) 2) ((= 4 4) 4) 0)", "4");
            CompileAndRun(ctx, "(case (+ 1 2) (2 #f) (3 #t) 'error)", "#t");
            CompileAndRun(ctx, "(let ((r '())) (for (i 0 (< i 3) (+ i 1)) (set! r (cons i r))) r)", "(2 1 0)");
            CompileAndRun(ctx, "(let ((r '())) (dotimes (i 3) (set! r (cons i r))) r)", "(2 1 0)");
            CompileAndRun(ctx, "(fold-left cons '() '(1 2))", "((() . 1) . 2)");
            CompileAndRun(ctx, "(fold-right cons '() '(1 2))", "(1 2)");
            CompileAndRun(ctx, "(begin (set! x '(1 2 3 4 5)) (list (first x) (second x) (third x)))", "(1 2 3)");
            CompileAndRun(ctx, "(begin (set! x '(1 2 3 4 5)) (list (after-first x) (after-second x) (after-third x)))", "((2 3 4 5) (3 4 5) (4 5))");
            CompileAndRun(ctx, "(set! add (let ((sum 0)) (lambda (delta) (set! sum (+ sum delta)) sum))) (add 0) (add 100) (add 0)", "[Closure]", "0", "100", "100");

            //DumpCodeBlocks(ctx);
        }

        public void PrintAllStandardLibraries () {
            var ctx = new Context(true, _logger);
            DumpCodeBlocks(ctx);
        }

        /// <summary> Compiles an s-expression, runs the resulting code, and checks the output against the expected value </summary>
        private void CompileAndRun (Context ctx, string input, params string[] expecteds) {
            ctx.parser.AddString(input);
            Log("\n\n-------------------------------------------------------------------------");
            Log("\n\nCOMPILE AND RUN inputs: ", input);

            for (int i = 0, count = expecteds.Length; i < count; i++) {
                string expected = expecteds[i];

                Val result = ctx.parser.ParseNext();
                Log("Parsed: ", result);

                var comp = ctx.compiler.Compile(result);
                Log("Compiled:");
                Log(ctx.code.DebugPrint(comp));

                Log("Running...");
                Val output = ctx.vm.Execute(comp.closure);
                string formatted = Val.Print(output);
                Check(new Val(formatted), new Val(expected));
            }
        }
    }
}
