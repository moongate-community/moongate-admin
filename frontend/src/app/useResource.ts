import { useEffect, useRef, useState } from 'react';
import { isAbort } from '../lib/api/errors';
export function useResource<T>(load: (signal: AbortSignal) => Promise<T>, keys: readonly unknown[]) {
    const loader = useRef(load);
    loader.current = load;
    const [attempt, setAttempt] = useState(0);
    const [state, setState] = useState<{ loading: boolean; data?: T; error?: unknown }>({ loading: true });
    useEffect(() => {
        const controller = new AbortController();
        setState({ loading: true });
        void loader.current(controller.signal).then(
            (data) => {
                if (!controller.signal.aborted) setState({ loading: false, data });
            },
            (error) => {
                if (!controller.signal.aborted && !isAbort(error)) setState({ loading: false, error });
            },
        );
        return () => controller.abort();
    }, [...keys, attempt]);
    return { ...state, reload: () => setAttempt((x) => x + 1) };
}
