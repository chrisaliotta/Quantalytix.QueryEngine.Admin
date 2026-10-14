/**
 * # Proxy-based JavaScript Sandbox
 *
 * Runs a validator function string (arrow function, regular function, etc.)
 * inside a `with (proxy) { ... }` block. The `Proxy`'s `has` trap always
 * returns `true`, so every bare identifier reference inside the user code
 * (`document`, `window`, `eval`, `fetch`, ...) is redirected through the
 * `Proxy`'s `get` trap instead of falling through to the real global scope.
 * Only a small allowlist of safe builtins (`Math`, `JSON`, `Array`, ...) is
 * exposed there; anything else throws.
 *
 * **What is blocked (bare global identifiers):**
 * ```javascript
 * (value) => document.cookie
 * (value) => window.location
 * (value) => fetch('evil.com')
 * (value) => eval('malicious')
 * (value) => new Function('return this')()
 * (value) => localStorage.getItem('x')
 * (value) => value.__proto__
 * (value) => value['__proto__']
 * (value) => value.toString.call(value)
 * (value) => Math.max.apply(null, value)
 * (value) => value.someMethod.bind(value)
 * (value) => this
 * (value) => typeof this
 * (value) => value.constructor
 * (value) => value.constructor.constructor('return 1')()
 * ```
 *
 * **What is ALLOWED (safe helpers):**
 * ```javascript
 * (value) => Number(value)
 * (value) => Array.isArray(value)
 * (value) => value.map(x => x * 2)
 * (value) => Object.keys(value)
 * (value) => Math.abs(value)
 * (value) => JSON.stringify(value)
 * (value) => /^[a-zA-Z]+$/.test(value)
 * (value) => { console.log('Validating:', value); return true; }
 * (value) => { throw new Error('invalid value'); }
 * ```
 *
 * **⚠️ Security note (same caveats as any `with` + `Proxy` sandbox):**
 * - This is a BEST-EFFORT sandbox, not a strong isolation boundary.
 * - It only intercepts *bare identifier* lookups. Property access on a
 *   value already in scope (a function parameter, an array/object literal,
 *   an allowed builtin) is normal property access and is NOT intercepted by
 *   the Proxy - it can still reach real globals through the prototype chain,
 *   including mutating shared builtin prototypes (e.g.
 *   `Object.prototype.evil = 1`, reached once a real `Function` reference is
 *   in hand). Freezing those prototypes to close that hole was tried and
 *   reverted: it is a permanent, process-wide change (there is only one real
 *   `Object.prototype`), and broke unrelated app/library code that relies
 *   on being able to extend them.
 * - The property names `__proto__`, `constructor`, `call`, `apply`, and
 *   `bind` are rejected by a static source check before the code ever runs
 *   (see `DENIED_PROPERTY_PATTERN`), for the same process-wide-side-effect
 *   reason the prototype freeze above was reverted. `constructor` is what
 *   closes the classic escape (`value.constructor.constructor('...')()`,
 *   `[].constructor.constructor('return this')()`) - without it, reaching a
 *   real `Function` reference from an ordinary value has no remaining
 *   textual path, since `Object.getPrototypeOf` only walks *down* the
 *   prototype chain (toward `Object.prototype`), never sideways to a
 *   constructor. This is still only a literal-token check: code that builds
 *   the string dynamically (`value['constr' + 'uctor']`) still gets through.
 * - The bare `this` keyword is rejected the same way (see
 *   `THIS_KEYWORD_PATTERN`). `this` is a reserved word, not an identifier -
 *   `with` cannot intercept it at all, and sloppy-mode substitution would
 *   otherwise resolve it to the real `globalThis`. Only a `.this` property
 *   name (not the keyword) is exempt from this check.
 * - It does NOT protect against infinite loops / heavy CPU (no timeouts),
 *   side-channel leaks, or engine escapes via future language features.
 * - For strong isolation in production, prefer an `<iframe sandbox>` or a
 *   Web Worker with a strict `postMessage` API.
 */

/** Allowlisted globals exposed inside the sandbox. Frozen once at module load. */
const SAFE_BUILTINS: Record<string, unknown> = Object.freeze({
  undefined: undefined,
  NaN: Number.NaN,
  Infinity: Number.POSITIVE_INFINITY,

  Number: Object.freeze(Number),
  String: Object.freeze(String),
  Boolean: Object.freeze(Boolean),
  Object: Object.freeze(Object),
  Array: Object.freeze(Array),
  Math: Object.freeze(Math),
  Date: Object.freeze(Date),
  RegExp: Object.freeze(RegExp),
  JSON: Object.freeze(JSON),
  BigInt,
  Symbol,

  Error,
  TypeError,
  RangeError,
  SyntaxError,
  ReferenceError,
  EvalError,
  URIError,

  parseInt,
  parseFloat,
  isFinite,
  isNaN,
  encodeURI,
  encodeURIComponent,
  decodeURI,
  decodeURIComponent,

  console,
  // Timers, Promise, eval, Function, and browser globals (window, document,
  // fetch, localStorage, ...) are intentionally omitted.
});

// `with` intercepts every identifier the `has` trap claims to own, including
// ones bound in a real enclosing scope (not just true globals). This single
// reserved name is excluded so the call arguments can be threaded in from
// the real closure instead of being swallowed by the proxy.
const ARGS_KEY = '__runSafeJavascriptArgs__';

/**
 * Thrown when sandboxed code tries to access or mutate something outside
 * the allowlist. Kept as its own `ReferenceError` subclass (rather than the
 * generic `Error` used for compilation/runtime failures) so callers can
 * keep relying on `instanceof ReferenceError` to detect sandbox denials,
 * matching the previous static-analysis implementation's error type.
 */
class SandboxAccessDeniedError extends ReferenceError {}

/**
 * Property names that are denied even when accessed on a value already in
 * scope (a function parameter, an object/array literal, an allowed
 * builtin) - property access never passes through the `with`/Proxy trap,
 * only bare identifiers do; see the module doc's security note. This is a
 * static, textual check on the source, matched as a dotted property
 * (`value.call`) or a bracketed/string key (`value['call']`), run before the
 * code ever executes:
 *  - `__proto__` - direct prototype access/mutation.
 *  - `constructor` - the classic sandbox escape
 *    (`value.constructor.constructor('...')()`) walks this property twice to
 *    reach the real `Function` constructor from an ordinary value. Blocking
 *    it removes the only textual path from a value to its constructor,
 *    since `Object.getPrototypeOf` (an allowed builtin) only walks *down*
 *    the prototype chain, never sideways to a constructor reference.
 *  - `call` / `apply` / `bind` - `Function.prototype` methods. These let
 *    code invoke a function with an arbitrary `this`/argument list without a
 *    literal `(...)` call expression, which is one way real escapes chain
 *    together (e.g. reaching `Function.prototype.call` itself, or invoking a
 *    borrowed method against a global).
 * Word boundaries keep this from matching unrelated identifiers that merely
 * contain these names (`callback`, `applyDiscount`, `bindGroup`, ...). It
 * can still be defeated by dynamically building the string (e.g.
 * `value['constr' + 'uctor']`), but it blocks the direct, common cases
 * cheaply and without the process-wide side effects that
 * freezing/overriding these on the real prototypes would have.
 */
const DENIED_PROPERTY_PATTERN =
  /\.(__proto__|constructor|call|apply|bind)\b|\[\s*(['"`])(__proto__|constructor|call|apply|bind)\2\s*\]/;

/**
 * Matches the `this` keyword used as an expression (bare `this`, not a
 * `.this` property name, which the negative lookbehind excludes). `this` is
 * a reserved word, not an identifier, so `with` never intercepts it at all -
 * sloppy-mode `this`-substitution means it resolves to the real
 * `globalThis` inside the sandboxed function regardless of the Proxy. There
 * is no scope-level fix for that (the language doesn't let `with` rebind
 * `this`), so it is rejected the same way as the denied properties above: a
 * static check on the source before it runs.
 */
const THIS_KEYWORD_PATTERN = /(?<!\.)\bthis\b/;

/**
 * Creates the `Proxy` used as the binding object for the `with` statement.
 * Reads fall back from the sandbox's own scope (variables the user code
 * declares) to the allowlist, and throw for anything else. Writes create
 * local variables in the sandbox scope instead of touching real globals.
 */
function createScopeProxy(scopeTarget: Record<string, unknown>): Record<string, unknown> {
  return new Proxy(scopeTarget, {
    has(_target, key): boolean {
      return key !== ARGS_KEY;
    },
    set(_target, key, value): boolean {
      scopeTarget[key as string] = value;
      return true;
    },
    get(_target, key): unknown {
      if (key === Symbol.unscopables) return undefined;
      if (key in SAFE_BUILTINS) return SAFE_BUILTINS[key as string];
      if (key in scopeTarget) return scopeTarget[key as string];
      throw new SandboxAccessDeniedError(`Access to \`${String(key)}\` is denied in sandbox.`);
    },
    getPrototypeOf(): null {
      return null;
    },
    setPrototypeOf(): never {
      throw new SandboxAccessDeniedError('Modifying prototype is denied in sandbox.');
    },
    defineProperty(): never {
      throw new SandboxAccessDeniedError('Defining properties on sandbox is denied.');
    },
    deleteProperty(_target, key): boolean {
      delete scopeTarget[key as string];
      return true;
    },
  });
}

/**
 * Runs `userCode` (a function expression string) inside the Proxy sandbox.
 *
 * @param userCode - Complete function string (arrow function, regular function, etc.)
 * @param value - Values to forward as call arguments to the sandboxed function
 * @returns whatever the user function returns
 * @throws ReferenceError if a denied global/property is accessed
 * @throws Error if compilation fails or the user function throws
 */
function runSafeJavascript(userCode: string, ...value: any) {
  const deniedPropertyMatch = userCode.match(DENIED_PROPERTY_PATTERN);
  if (deniedPropertyMatch) {
    const token = deniedPropertyMatch[1] ?? deniedPropertyMatch[3];
    throw new SandboxAccessDeniedError(`Access to \`${token}\` is denied in sandbox.`);
  }
  if (THIS_KEYWORD_PATTERN.test(userCode)) {
    throw new SandboxAccessDeniedError('Access to `this` is denied in sandbox.');
  }

  const scopeTarget = Object.create(null);
  const sandbox = createScopeProxy(scopeTarget);

  try {
    // Must NOT be strict mode - `with` is a SyntaxError in strict mode.
    const evaluator = new Function(
      'sandbox',
      ARGS_KEY,
      `return (function () {
        with (sandbox) {
          return (${userCode}).apply(null, ${ARGS_KEY});
        }
      }).call(null);`
    );

    return evaluator(sandbox, value);
  } catch (error) {
    if (error instanceof SandboxAccessDeniedError) {
      throw error;
    }
    throw new Error(`Function error: ${(error as Error).message}`);
  }
}

export { runSafeJavascript };
