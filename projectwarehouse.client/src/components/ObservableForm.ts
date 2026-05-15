import {makeObservable, observable, toJS, runInAction, reaction} from "mobx";
import {
  type FieldValues,
  type UseFormGetValues,
  type UseFormSetValue,
  type UseFormWatch,
  type FieldPath,
  type UseFormReset,
} from "react-hook-form";
import rdiff from "recursive-diff";

/** react-hook-form methods required by {@link ObservableForm}. */
export type ObservableFormDeps<TFieldValues extends FieldValues> = {
  getValues: UseFormGetValues<TFieldValues>;
  setValue: UseFormSetValue<TFieldValues>;
  reset: UseFormReset<TFieldValues>;
  watch: UseFormWatch<TFieldValues>;
};

/**
 * Bidirectional bridge between a react-hook-form instance and MobX.
 *
 * `_data` is a MobX observable snapshot of the form values. Changes flow in
 * both directions and are protected by `_syncing` to prevent feedback loops:
 *
 * - **RHF → MobX**: `watch()` subscription updates `_data` whenever a field
 *   changes inside react-hook-form (e.g. user input).
 * - **MobX → RHF**: a MobX `reaction` diffs `_data` against current form
 *   values and calls `setValue` for changed fields (or `reset` when the diff
 *   touches the form root).
 *
 * Typical usage in a MobX store:
 * ```ts
 * class MyStore {
 *   form = new ObservableForm<MyFormValues>();
 * }
 *
 * // Inside the component:
 * const rhf = useForm<MyFormValues>();
 * useEffect(() => store.form.init(rhf), []);
 *
 * // Assign values from outside React (e.g. after an API call):
 * store.form.data = apiResponse;
 *
 * // Read values reactively in a MobX observer:
 * const value = store.form.data?.someField;
 * ```
 *
 * @template TFieldValues Shape of the form values object.
 */
export class ObservableForm<TFieldValues extends FieldValues> {
  private _deps: ObservableFormDeps<TFieldValues> | null = null;
  private _data: TFieldValues | null = null;
  /** Prevents re-entrant syncing between the two directions. */
  private _syncing = false;

  constructor() {
    makeObservable<ObservableForm<TFieldValues>, "_data">(this, {
      _data: observable,
    });
  }

  /**
   * Connects the class to a react-hook-form instance.
   *
   * Call this once inside a `useEffect` and return the cleanup function so
   * subscriptions are torn down when the component unmounts.
   *
   * @param deps - The `getValues`, `setValue`, `reset`, and `watch` methods
   *   from `useForm()`.
   * @returns A cleanup function that unsubscribes the `watch` listener and
   *   disposes the MobX reaction.
   */
  init(deps: ObservableFormDeps<TFieldValues>) {
    this._deps = deps;
    runInAction(() => {
      this._data = deps.getValues();
    });

    // RHF → MobX: mirror field changes into _data
    const watchSubscription = deps.watch((_, {name}) => {
      if (this._syncing) return;

      this._syncing = true;
      try {
        runInAction(() => {
          if (name == null) {
            this._data = this._deps!.getValues();
          } else {
            const newValue = this._deps!.getValues(name as FieldPath<TFieldValues>);
            const path = name.split(".");
            let obj: any = this._data;
            for (let i = 0; i < path.length; i++) {
              if (i === path.length - 1) {
                if (obj != null) obj[path[i]] = newValue;
              } else {
                obj = obj?.[path[i]];
              }
            }
          }
        });
      } finally {
        this._syncing = false;
      }
    });

    // MobX → RHF: push _data changes back into the form via setValue / reset
    const dispose = reaction(
      () => (this._data ? toJS(this._data) : null),
      (newValues) => {
        if (newValues == null) return;
        if (this._syncing) return;

        const diff = rdiff.getDiff(this._deps!.getValues(), newValues);
        if (diff.length === 0) return;

        const walkedKeys: string[] = [];

        this._syncing = true;
        try {
          for (const diffElement of diff) {
            let newObj = newValues;

            // Root-level diff (e.g. full object replacement) — use reset to
            // preserve dirty/touched/error state as much as possible.
            if (diffElement.path.length - (diffElement.op === "update" ? 1 : 2) < 0) {
              this._deps!.reset(newObj, {
                keepDirtyValues: true,
                keepErrors: true,
                keepDirty: true,
                keepValues: false,
                keepDefaultValues: false,
                keepIsSubmitted: true,
                keepTouched: true,
                keepIsValid: true,
                keepSubmitCount: true,
              });
              break;
            }

            for (let i = 0; i < diffElement.path.length; i++) {
              if (i === diffElement.path.length - (diffElement.op === "update" ? 1 : 2)) {
                const path = diffElement.path.slice(0, i + 1).join(".") as FieldPath<TFieldValues>;
                if (walkedKeys.includes(path)) break;
                walkedKeys.push(path);
                this._deps!.setValue(path, newObj[diffElement.path[i]]);
              } else {
                newObj = newObj[diffElement.path[i]];
              }
            }
          }
        } finally {
          this._syncing = false;
        }
      },
    );

    return () => {
      watchSubscription.unsubscribe();
      dispose();
    };
  }

  /**
   * Current form values as a MobX observable.
   *
   * Reading this inside a MobX `observer` / `computed` / `reaction` makes it
   * reactive — the observer re-runs whenever any field value changes.
   *
   * Returns `null` before {@link init} has been called.
   */
  get data(): TFieldValues | null {
    return this._data;
  }

  /**
   * Replaces all form values at once (e.g. after loading data from an API).
   *
   * Internally triggers the MobX → RHF sync: changed fields are updated via
   * `setValue`; a root-level replacement falls back to `reset`.
   *
   * @throws Error if called before {@link init}.
   */
  set data(data: TFieldValues) {
    if (!this._deps) throw new Error("ObservableForm is not initialized — call init() first");
    runInAction(() => {
      this._data = data;
    });
  }
}
