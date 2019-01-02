using CSLisp.Data;
using CSLisp.Error;
using CSLisp.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CSLisp.Core
{
    /// <summary>
    /// Parser reads strings, and spits out s-expressions
    /// </summary>
    public class Parser
    {
        /// <summary> Full list of reserved keywords - no symbol can be named as one of these </summary>
        public static readonly List<string> RESERVED
            = new List<string>() { "quote", "begin", "set!", "if", "if*", "lambda", "defmacro", "." };

        /// <summary> Special "end of stream" constant </summary>
        public static readonly Val EOF = new Val("!eof");

        /// <summary> Internal stream </summary>
        private InputStream _stream = new InputStream();

        /// <summary> Reference to the global packages manager </summary>
        private Packages _packages;

        /// <summary> Global unnamed package, used for symbols like "quote" (convenience reference) </summary>
        private Package _global;

        /// <summary> Optional logger callback </summary>
        private LoggerCallback _logger;

        public Parser (Packages packages, LoggerCallback logger) {
            _packages = packages ?? throw new ParserError("Parser requires a valid packages structure during initialization");
            _global = packages.global;
            _logger = logger;
        }

        /// <summary> Adds a new string to the parse buffer </summary>
        public void AddString (string str) {
            _stream.Add(str);
        }

        /// <summary> Parses and returns all the elements it can from the stream </summary>
        public List<Val> ParseAll () {
            List<Val> results = new List<Val>();
            Val result = ParseNext();
            while (!Val.Equals(result, EOF)) {
                results.Add(result);
                result = ParseNext();
            }
            return results;
        }

        /// <summary> 
        /// Parses the next element out of the stream (just one, if the stream contains more). 
        /// Returns EOF and restores the stream, if no full element has been found.
        /// </summary>
        public Val ParseNext () {
            _stream.Save();
            Val result = Parse(_stream);
            if (!Val.Equals(result, EOF)) {
                if (_logger != null) {
                    _logger("==> ", Val.ToString(result));
                }
                return result;
            }

            _stream.Restore();
            return EOF;
        }

        /// <summary> 
        /// Parses an expression out of the stream. 
        /// If the stream contains more than one expression, stops after the first one.
        /// If the stream did not contain a full expression, returns EOF.
        ///
        /// If backquote is true, we are recursively parsing inside a backquote expression
        /// which changes some of the parse behavior.
        /// </summary>
        private Val Parse (InputStream stream, bool backquote = false) {
            // pull out the first character, we'll dispatch on it 
            if (stream.IsEmpty) {
                return EOF;
            }

            // remove leading whitespace
            ConsumeWhitespace(stream);

            // check for special forms
            Val result = EOF;
            char c = stream.Peek();
            switch (c) {
                case ';':
                    ConsumeToEndOfLine(stream);
                    result = Parse(stream, backquote);
                    break;
                case '(':
                    // this function will take care of the list, including the closing paren
                    result = ParseList(stream, backquote);
                    break;
                case ')':
                    // well that was unexpected
                    throw new ParserError("Unexpected closed parenthesis!");
                case '\"':
                    // this function will take care of the string, including the closing quote
                    result = ParseString(stream);
                    break;
                case '\'':
                    // 'foo => (quote foo)
                    {
                        stream.Read();
                        var body = Parse(stream, backquote);
                        result = Cons.MakeList(_global.Intern("quote"), body);
                    }
                    break;
                case '`':
                    // `foo => (` foo) => converted value
                    {
                        stream.Read();
                        var body = Parse(stream, true);
                        var aslist = Cons.MakeList(_global.Intern("`"), body);
                        result = ConvertBackquote(aslist);
                    }
                    break;
                case ',':
                    // ,foo => (, foo) 
                    // except that 
                    // ,@foo => (,@ foo)
                    {
                        stream.Read();
                        if (!backquote) {
                            throw new ParserError("Unexpected unquote!");
                        }
                        bool atomicUnquote = true;
                        if (stream.Peek() == '@') {
                            stream.Read();
                            atomicUnquote = false;
                        }
                        var body = Parse(stream, false);
                        result = Cons.MakeList(_global.Intern(atomicUnquote ? "," : ",@"), body);
                    }
                    break;
                default:
                    // just a value. pick how to parse
                    result = ParseAtom(stream, backquote);
                    break;
            }

            // consume trailing whitespace
            ConsumeWhitespace(stream);

            return result;
        }

        /// <summary> Is this one of the standard whitespace characters? </summary>
        private bool IsWhitespace (char ch) => char.IsWhiteSpace(ch);

        /// <summary> Eats up whitespace, nom nom </summary>
        private void ConsumeWhitespace (InputStream stream) {
            while (IsWhitespace(stream.Peek())) { stream.Read(); }
        }

        /// <summary> Eats up everything till end of line </summary>
        private void ConsumeToEndOfLine (InputStream stream) {
            char c = stream.Peek();
            while (c != '\n' && c != '\r') {
                stream.Read();
                c = stream.Peek();
            }
        }

        private readonly List<char> SPECIAL_ELEMENTS = new List<char>() { '(', ')', '\"', '\'', '`' };

        /// <summary> Special elements are like whitespace - they interrupt tokenizing </summary>
        private bool IsSpecialElement (char elt, bool insideBackquote)
            => SPECIAL_ELEMENTS.Contains(elt) || (insideBackquote && elt == ',');


        /// <summary> 
        /// Parses a single element (token), based on following rules:
        ///   - if it's #t, it will be converted to a boolean true
        ///   - otherwise if it starts with #, it will be converted to a boolean false
        ///   - otherwise if it starts with +, -, or a digit, it will be converted to a number 
        ///     (int or float, assuming parsing validation passes)
        ///   - otherwise it will be returned as a symbol
        /// </summary>
        private Val ParseAtom (InputStream stream, bool backquote) {

            // tokenizer loop
            StringBuilder sb = new StringBuilder();
            char ch;
            while ((ch = stream.Peek()) != (char)0) {
                if (IsWhitespace(ch) || IsSpecialElement(ch, backquote)) {
                    break; // we're done here, don't touch the special character
                }
                sb.Append(ch);
                stream.Read(); // consume and advance to the next one
            }

            // did we fail?
            if (sb.Length == 0) {
                return EOF;
            }

            string str = sb.ToString();

            // #t => true, #(anything) => false
            char c0 = str[0];
            if (c0 == '#') {
                if (str.Length == 2 && (str[1] == 't' || str[1] == 'T')) {
                    return new Val(true);
                } else {
                    return new Val(false);
                }
            }

            // parse if it starts with -, +, or a digit, but fall back if it causes a parse error
            if (c0 == '-' || c0 == '+' || char.IsDigit(c0)) {
                Val num = ParseNumber(str);
                if (num.IsNotNil) {
                    return num;
                }
            }

            // parse as symbol
            return ParseSymbol(str);
        }

        /// <summary> Parses as a number, an int or a float (the latter if there is a period present) </summary>
        private Val ParseNumber (string val) {
            try {
                var hasPeriod = val.Contains(".");
                if (hasPeriod) {
                    return new Val(float.Parse(val, CultureInfo.InvariantCulture));
                } else {
                    return new Val(int.Parse(val, CultureInfo.InvariantCulture));
                }
            } catch (Exception) {
                return Val.NIL;
            }
        }

        /// <summary> Parses as a symbol, taking into account optional package prefix </summary>
        private Val ParseSymbol (string name) {
            // if this is a reserved keyword, always using global namespace
            if (RESERVED.Contains(name)) { return new Val(_global.Intern(name)); }

            // figure out the package. default to current package.
            Package p = _packages.current;

            // reference to a non-current package - let's look it up there
            int colon = name.IndexOf(":");
            if (colon >= 0) {
                string pkgname = name.Substring(0, colon);
                p = _packages.Intern(pkgname);  // we have a specific package name, look there instead
                if (p == null) {
                    throw new ParserError("Unknown package: " + pkgname);
                }
                name = name.Substring(colon + 1);
            }

            // do we have the symbol anywhere in that package or its imports?
            Symbol result = p.Find(name, true);
            if (result != null) {
                return new Val(result);
            }

            // never seen it before - intern it!
            return new Val(p.Intern(name));
        }

        /// <summary> 
        /// Starting with an opening double-quote, it will consume everything up to and including closing double quote.
        /// Any characters preceded by backslash will be escaped.
        /// </summary>
        private Val ParseString (InputStream stream) {

            StringBuilder sb = new StringBuilder();

            stream.Read(); // consume the opening quote

            while (true) {
                char ch = stream.Read();
                if (ch == (char)0) { throw new ParserError($"string not properly terminated: {sb.ToString()}"); }

                // if we've consumed the closing double-quote, we're done.
                if (ch == '\"') { break; }

                // we got the escape - use the next character instead, whatever it is
                if (ch == '\\') { ch = stream.Read(); }

                sb.Append(ch);
            }

            return new Val(sb.ToString());
        }

        /// <summary> 
        /// Starting with an open paren, recursively parse everything up to the matching closing paren,
        /// and then return it as a sequence of conses.
        /// </summary>
		private Val ParseList (InputStream stream, bool backquote) {

            List<Val> results = new List<Val>();
            char ch = stream.Read(); // consume opening paren
            ConsumeWhitespace(stream);

            while ((ch = stream.Peek()) != ')' && ch != (char)0) {
                Val val = Parse(stream, backquote);
                results.Add(val);
            }

            stream.Read(); // consume the closing paren
            ConsumeWhitespace(stream);

            return Cons.MakeList(results);
        }

        /// <summary>
        /// Converts a backquote expression according to the following rules:
        ///
        /// <pre>
        /// (` e) where e is atomic => (quote e)
        /// (` (, e)) => e
        /// (` (a ...)) => (append [a] ...) but transforming elements:
        ///   [(, a)] => (list a)
        ///   [(,@ a)] => a
        ///   [a] => (list (` a)) transformed further recursively
        ///   
        /// </pre> </summary>
        private Val ConvertBackquote (Cons cons) {
            Symbol first = cons.first.GetAsSymbolOrNull;
            if (first == null || first.name != "`") {
                throw new ParserError($"Unexpected {first} in place of backquote");
            }

            // (` e) where e is atomic => e
            Cons body = cons.second.GetAsConsOrNull;
            if (body == null) {
                return Cons.MakeList(_global.Intern("quote"), cons.second);
            }

            // (` (, e)) => e
            if (IsSymbolWithName(body.first, ",")) {
                return body.second;
            }

            // we didn't match any special forms, just do a list match
            // (` (a ...)) => (append [a] ...) 
            List<Val> forms = new List<Val>();
            Cons c = body;
            while (c != null) {
                forms.Add(ConvertBackquoteElement(c.first));
                c = c.rest.GetAsConsOrNull;
            }

            Cons result = new Cons(_global.Intern("append"), Cons.MakeList(forms));

            // now do a quick optimization: if the result is of the form:
            // (append (list ...) (list ...) ...) where all entries are known to be lists,
            // convert this to (list ... ... ...)
            return TryOptimizeAppend(result);
        }

        /// <summary> 
		/// Performs a single bracket substitution for the backquote:
		/// 
		/// [(, a)] => (list a)
		/// [(,@ a)] => a
		/// [a] => (list (` a))
		/// </summary>
        private Val ConvertBackquoteElement (Val value) {
            var cons = value.GetAsConsOrNull;
            if (cons != null && cons.first.IsSymbol) {
                Symbol sym = cons.first.GetSymbol;
                switch (sym.name) {
                    case ",":
                        // [(, a)] => (list a)
                        return new Val(new Cons(new Val(_global.Intern("list")), cons.rest));
                    case ",@":
                        // [(,@ a)] => a
                        return cons.second;
                }
            }

            // [a] => (list (` a)), recursively
            var body = Cons.MakeList(_global.Intern("`"), value);
            return Cons.MakeList(_global.Intern("list"), ConvertBackquote(body));
        }

        /// <summary> 
        /// If the results form follows the pattern (append (list a b) (list c d) ...)
        /// it will be converted to a simple (list a b c d ...)
        /// </summary>
        private Val TryOptimizeAppend (Cons value) {
            Val original = new Val(value);

            if (!IsSymbolWithName(value.first, "append")) {
                return original;
            }

            List<Val> results = new List<Val>();
            Val rest = value.rest;
            while (rest.IsNotNil) {
                Cons cons = rest.GetAsConsOrNull;
                if (cons == null) {
                    return original; // not a proper list
                }

                Cons maybeList = cons.first.GetAsConsOrNull;
                if (maybeList == null) {
                    return original; // not all elements are lists themselves
                }

                if (!IsSymbolWithName(maybeList.first, "list")) {
                    return original; // not all elements are of the form (list ...)
                }

                Val ops = maybeList.rest;
                while (ops.IsCons) {
                    results.Add(ops.GetCons.first);
                    ops = ops.GetCons.rest;
                }
                rest = cons.rest;
            }

            // we've reassembled the bodies, return them in the form (list ...)
            return new Val(new Cons(_global.Intern("list"), Cons.MakeList(results)));
        }

        /// <summary> Convenience function: checks if the value is of type Symbol, and has the specified name </summary>
        private bool IsSymbolWithName (Val value, string fullName) {
            Symbol symbol = value.GetAsSymbolOrNull;
            return (symbol != null) && (symbol.fullName == fullName);
        }

    }
}