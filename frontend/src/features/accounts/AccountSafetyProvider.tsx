import { createContext, useContext, useRef, useState, useCallback, type ReactNode } from 'react';
interface SafetyState {
    username: string;
    version: number;
    fresh: boolean;
}
interface Safety extends SafetyState {
    markUncertain(username: string): void;
    recordRefresh(version: number): void;
    acknowledge(): void;
}
const SafetyContext = createContext<Safety | null>(null);
export function AccountSafetyProvider({ children }: { children: ReactNode }) {
    const current = useRef<SafetyState>({ username: '', version: 0, fresh: false });
    const [state, setState] = useState(current.current);
    const markUncertain = useCallback((username: string) => {
        current.current = { username, version: current.current.version + 1, fresh: false };
        setState(current.current);
    }, []);
    const recordRefresh = useCallback((version: number) => {
        if (current.current.username && version === current.current.version) {
            current.current = { ...current.current, fresh: true };
            setState(current.current);
        }
    }, []);
    const acknowledge = useCallback(() => {
        if (current.current.fresh) {
            current.current = { ...current.current, username: '', fresh: false };
            setState(current.current);
        }
    }, []);
    return (
        <SafetyContext.Provider value={{ ...state, markUncertain, recordRefresh, acknowledge }}>
            {children}
        </SafetyContext.Provider>
    );
}
export function useAccountSafety() {
    const value = useContext(SafetyContext);
    if (!value) throw new Error('AccountSafetyProvider is required.');
    return value;
}
