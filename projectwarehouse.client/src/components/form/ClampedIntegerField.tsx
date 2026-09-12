import {useCallback, useEffect, useRef, useState} from "react";
import type {Ref, RefCallback} from "react";
import type {TextFieldProps} from "@mui/material";
import {
  ClickAwayListener,
  InputAdornment,
  Paper,
  Popper,
  TextField,
  Typography,
} from "@mui/material";

interface ClampedIntegerFieldProps extends Omit<
  TextFieldProps,
  "value" | "onChange" | "onBlur" | "onFocus" | "type"
> {
  value: number;
  min?: number;
  max?: number;
  onCommit: (value: number) => void;
}

type Operator = "+" | "-" | "*" | "/";

interface CalcState {
  left: string;
  // Empty while the tape holds no pending operation — backspacing the operator away
  // has to be possible, otherwise the popover fights the user's edits.
  op: Operator | "";
  right: string;
}

const CALC_EXPRESSION = /^(-?\d*)([+\-*/]?)(\d*)$/;

function isOperator(key: string): key is Operator {
  return key === "+" || key === "-" || key === "*" || key === "/";
}

function applyOperator(a: number, op: Operator, b: number): number {
  switch (op) {
    case "+":
      return a + b;
    case "-":
      return a - b;
    case "*":
      return a * b;
    case "/":
      return a / b;
  }
}

function evaluate({left, op, right}: CalcState): number {
  const a = Number(left);
  if (op === "" || right === "") return a;
  return applyOperator(a, op, Number(right));
}

function formatCalc({left, op, right}: CalcState): string {
  return `${left}${op}${right}`;
}

// Preview shows plain arithmetic, unrounded and unclamped; the commit rounds and clamps.
function previewResult(state: CalcState): string | null {
  if (state.op === "" || state.right === "" || state.left === "") return null;
  const result = evaluate(state);
  if (!Number.isFinite(result)) return null;
  return String(Number(result.toFixed(4)));
}

function assignRef<T>(ref: Ref<T> | undefined, node: T) {
  if (typeof ref === "function") ref(node);
  else if (ref) (ref as {current: T}).current = node;
}

/**
 * Number field that keeps raw keystrokes (including an empty field) uncommitted
 * until blur, so clamping doesn't fight the user while they're typing/clearing it.
 * Typing an operator opens a calculator popover anchored under the field.
 */
export function ClampedIntegerField({
  value,
  min = 1,
  max,
  onCommit,
  slotProps,
  inputRef,
  onKeyDown,
  ...rest
}: ClampedIntegerFieldProps) {
  const [raw, setRaw] = useState(String(value));
  const [calc, setCalc] = useState<CalcState | null>(null);
  const [calcError, setCalcError] = useState(false);
  const focusedRef = useRef(false);
  const calcOpenRef = useRef(false);
  const [anchorEl, setAnchorEl] = useState<HTMLDivElement | null>(null);
  const innerInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (!focusedRef.current && !calcOpenRef.current) setRaw(String(value));
  }, [value]);

  const clamp = (n: number) => {
    const atLeast = Math.max(min, n);
    return max !== undefined ? Math.min(max, atLeast) : atLeast;
  };

  const commit = (n: number) => {
    setRaw(String(n));
    if (n !== value) onCommit(n);
  };

  // Stable identity on purpose: a fresh callback ref would make React re-register
  // the forwarded ref (null, then node) on every render of the surrounding form.
  const forwardedInputRef = useRef(inputRef);
  useEffect(() => {
    forwardedInputRef.current = inputRef;
  }, [inputRef]);

  const setInputRef = useCallback<RefCallback<HTMLInputElement>>((node) => {
    innerInputRef.current = node;
    assignRef(forwardedInputRef.current, node);
  }, []);

  const closeCalc = (refocus: boolean) => {
    calcOpenRef.current = false;
    setCalc(null);
    setCalcError(false);
    if (refocus) innerInputRef.current?.focus();
  };

  const applyCalc = (state: CalcState) => {
    const result = evaluate(state);
    if (!Number.isFinite(result)) {
      setCalcError(true);
      return;
    }
    commit(clamp(Math.round(result)));
    closeCalc(true);
  };

  // Folding on every operator keeps at most one pending operation, so the popover
  // reads as a running tape rather than an expression needing precedence rules.
  const pushOperator = (state: CalcState, op: Operator) => {
    if (state.right === "") return {...state, op};
    const folded = evaluate(state);
    if (!Number.isFinite(folded)) {
      setCalcError(true);
      return state;
    }
    return {left: String(Math.round(folded)), op, right: ""};
  };

  const calcText = calc ? formatCalc(calc) : "";
  const calcPreview = calc && !calcError ? previewResult(calc) : null;
  const calcWidth = Math.max(6, calcText.length + 1 + (calcPreview ? calcPreview.length + 1 : 0));

  return (
    <>
      <TextField
        type="number"
        value={raw}
        onFocus={() => {
          focusedRef.current = true;
        }}
        onChange={(e) => {
          const next = e.target.value;
          if (next !== "" && !/^\d+$/.test(next)) return;
          setRaw(next);
        }}
        onKeyDown={(e) => {
          onKeyDown?.(e);
          if (e.defaultPrevented || !isOperator(e.key)) return;
          e.preventDefault();
          setCalcError(false);
          calcOpenRef.current = true;
          setCalc({left: raw === "" ? String(value) : raw, op: e.key, right: ""});
        }}
        onBlur={() => {
          focusedRef.current = false;
          if (calcOpenRef.current) return;
          commit(clamp(Number(raw) || min));
        }}
        slotProps={{
          ...slotProps,
          htmlInput: {min, ...(max !== undefined ? {max} : {}), ...slotProps?.htmlInput},
        }}
        inputRef={setInputRef}
        {...rest}
        ref={setAnchorEl}
      />
      <Popper
        open={calc !== null}
        anchorEl={anchorEl}
        placement="bottom-start"
        // Portaling to body would put the popover outside a Dialog/Drawer focus trap,
        // which then yanks focus back and makes the calculator impossible to type into.
        disablePortal
        sx={{zIndex: (theme) => theme.zIndex.modal + 1}}
      >
        <ClickAwayListener
          onClickAway={() => {
            commit(clamp(Number(raw) || min));
            closeCalc(false);
          }}
        >
          <Paper elevation={4} sx={{p: 0.75, mt: 0.5}}>
            <TextField
              autoFocus
              size="small"
              value={calcText}
              error={calcError}
              sx={{width: `calc(${calcWidth}ch + 26px)`}}
              slotProps={{
                input: {
                  endAdornment:
                    calcPreview !== null ? (
                      <InputAdornment position="end" sx={{ml: 0.25, color: "text.secondary"}}>
                        ={calcPreview}
                      </InputAdornment>
                    ) : undefined,
                },
              }}
              onChange={(e) => {
                const parsed = CALC_EXPRESSION.exec(e.target.value);
                if (!parsed || !calc) return;
                const [, left, op, right] = parsed;
                setCalcError(false);
                setCalc({left, op: isOperator(op) ? op : "", right});
              }}
              onKeyDown={(e) => {
                if (!calc) return;
                if (e.key === "Escape") {
                  e.preventDefault();
                  closeCalc(true);
                  return;
                }
                if (e.key === "Enter") {
                  e.preventDefault();
                  applyCalc(calc);
                  return;
                }
                if (isOperator(e.key)) {
                  e.preventDefault();
                  setCalc(pushOperator(calc, e.key));
                }
              }}
            />
            {calcError && (
              <Typography
                variant="caption"
                color="error"
                sx={{display: "block", mt: 0.5, whiteSpace: "nowrap"}}
              >
                Некорректное выражение
              </Typography>
            )}
          </Paper>
        </ClickAwayListener>
      </Popper>
    </>
  );
}
