import type {PreviewState} from './model';

// Graph and timeline subscribers can select their own values from each preview snapshot.
export function createPreviewRuntime() {
  let snapshot: {state: PreviewState | null} = {state: null};
  const listeners = new Set<() => void>();
  return {
    get: () => snapshot,
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => { listeners.delete(listener); };
    },
    publish(state: PreviewState | null) {
      if (snapshot.state === state) return;
      snapshot = {state};
      listeners.forEach(listener => listener());
    },
  };
}
export type PreviewRuntimeStore = ReturnType<typeof createPreviewRuntime>;
