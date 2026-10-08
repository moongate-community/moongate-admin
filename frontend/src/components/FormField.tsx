import { useId, type ComponentProps } from 'react';
import { Input } from './ui/input';
import { Label } from './ui/label';
export function FormField({ label, error, ...props }: ComponentProps<typeof Input> & { label: string; error?: string }) {
    const id = useId();
    return (
        <div className="space-y-2 min-w-0">
            <Label htmlFor={id}>{label}</Label>
            <Input id={id} aria-invalid={!!error} aria-describedby={error ? id + '-error' : undefined} {...props} />
            {error && (
                <p id={id + '-error'} className="text-sm text-destructive">
                    {error}
                </p>
            )}
        </div>
    );
}
